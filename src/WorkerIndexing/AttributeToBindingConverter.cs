//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Reflection;

using Microsoft.Azure.Functions.PowerShellWorker.Attributes;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker.WorkerIndexing
{
    /// <summary>
    /// Converts PowerShell attribute AST nodes to Azure Functions BindingInfo and raw binding JSON.
    /// Binding metadata is auto-discovered from attribute classes via reflection — the converter
    /// has no hardcoded knowledge of specific binding types.
    /// </summary>
    internal static class AttributeToBindingConverter
    {
        private const string AzFunctionTypeName = "AzFunction";

        // Generic attribute names that need special handling (Type comes from AST, not reflection)
        private static readonly HashSet<string> GenericAttributeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GenericTrigger",
            "GenericInputBinding",
            "GenericOutputBinding",
        };

        // AST named-argument keys to exclude from raw binding serialization (handled separately)
        private static readonly HashSet<string> ExcludedPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Type",       // Generic bindings: becomes the "type" field
            "Properties", // Generic bindings: flattened into the raw binding
        };

        private enum BindingDirection { Trigger, In, Out }

        private sealed class BindingRegistration
        {
            public string BindingType { get; }
            public BindingDirection Direction { get; }
            public string ImplicitOutputType { get; }
            public HashSet<string> ArrayPropertyNames { get; }
            public HashSet<string> RequiredPropertyNames { get; }

            public BindingRegistration(string bindingType, BindingDirection direction, string implicitOutputType = null, string arrayProperties = null, string requiredProperties = null)
            {
                BindingType = bindingType;
                Direction = direction;
                ImplicitOutputType = implicitOutputType;
                ArrayPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(arrayProperties))
                {
                    foreach (var name in arrayProperties.Split(','))
                    {
                        var trimmed = name.Trim();
                        if (trimmed.Length > 0) ArrayPropertyNames.Add(trimmed);
                    }
                }
                RequiredPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(requiredProperties))
                {
                    foreach (var name in requiredProperties.Split(','))
                    {
                        var trimmed = name.Trim();
                        if (trimmed.Length > 0) RequiredPropertyNames.Add(trimmed);
                    }
                }
            }
        }

        // Populated at static init by reflecting over the assembly's attribute classes
        private static readonly Dictionary<string, BindingRegistration> RegisteredBindings
            = new Dictionary<string, BindingRegistration>(StringComparer.OrdinalIgnoreCase);

        static AttributeToBindingConverter()
        {
            DiscoverBindingAttributes();
        }

        /// <summary>
        /// Scans the worker assembly for concrete attribute classes deriving from the binding base classes.
        /// Each class self-describes its binding type via the abstract BindingType property.
        /// </summary>
        private static void DiscoverBindingAttributes()
        {
            var assembly = typeof(TriggerBindingBaseAttribute).Assembly;

            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                var shortName = StripAttributeSuffix(type.Name);

                try
                {
                    if (typeof(TriggerBindingBaseAttribute).IsAssignableFrom(type))
                    {
                        var instance = (TriggerBindingBaseAttribute)Activator.CreateInstance(type);
                        if (!string.IsNullOrEmpty(instance.BindingType))
                        {
                            RegisteredBindings[shortName] = new BindingRegistration(
                                instance.BindingType, BindingDirection.Trigger, instance.ImplicitOutputBindingType,
                                instance.ArrayProperties, instance.RequiredProperties);
                        }
                    }
                    else if (typeof(InputBindingBaseAttribute).IsAssignableFrom(type))
                    {
                        var instance = (InputBindingBaseAttribute)Activator.CreateInstance(type);
                        if (!string.IsNullOrEmpty(instance.BindingType))
                        {
                            RegisteredBindings[shortName] = new BindingRegistration(
                                instance.BindingType, BindingDirection.In,
                                arrayProperties: instance.ArrayProperties, requiredProperties: instance.RequiredProperties);
                        }
                    }
                    else if (typeof(OutputBindingBaseAttribute).IsAssignableFrom(type))
                    {
                        var instance = (OutputBindingBaseAttribute)Activator.CreateInstance(type);
                        if (!string.IsNullOrEmpty(instance.BindingType))
                        {
                            RegisteredBindings[shortName] = new BindingRegistration(
                                instance.BindingType, BindingDirection.Out,
                                arrayProperties: instance.ArrayProperties, requiredProperties: instance.RequiredProperties);
                        }
                    }
                }
                catch
                {
                    // Skip types that can't be instantiated (e.g., generic attributes with null BindingType)
                }
            }
        }

        #region Public API

        internal static bool IsAzFunctionAttribute(AttributeAst attributeAst)
        {
            return GetAttributeTypeName(attributeAst).Equals(AzFunctionTypeName, StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetFunctionNameOverride(AttributeAst attributeAst)
        {
            return GetNamedArgumentValue(attributeAst, "Name");
        }

        internal static bool IsTriggerAttribute(AttributeAst attributeAst)
        {
            var reg = ResolveRegistration(attributeAst);
            if (reg != null)
            {
                return reg.Direction == BindingDirection.Trigger;
            }
            // GenericTrigger is not registered but is still a trigger
            return GetAttributeTypeName(attributeAst).Equals("GenericTrigger", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBindingAttribute(AttributeAst attributeAst)
        {
            return ResolveRegistration(attributeAst) != null || IsGenericAttribute(attributeAst);
        }

        internal static BindingInfo ConvertToBindingInfo(AttributeAst attributeAst)
        {
            ResolveBindingTypeAndDirection(attributeAst, out string bindingType, out BindingDirection direction);
            return new BindingInfo
            {
                Type = bindingType,
                Direction = ToGrpcDirection(direction)
            };
        }

        internal static string ConvertToRawBinding(AttributeAst attributeAst, string parameterName)
        {
            ResolveBindingTypeAndDirection(attributeAst, out string bindingType, out BindingDirection direction);

            // Resolve array property names from the binding registration
            var reg = ResolveRegistration(attributeAst);
            var arrayPropertyNames = reg?.ArrayPropertyNames;

            var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "name", parameterName },
                { "type", bindingType },
                { "direction", DirectionToString(direction) }
            };

            // Generically extract ALL named arguments from the AST
            AddAllNamedArguments(attributeAst, properties, arrayPropertyNames);

            // For generic bindings, flatten the Properties string ("key=value; key=value" pairs)
            if (IsGenericAttribute(attributeAst))
            {
                FlattenGenericProperties(attributeAst, properties);
            }

            return SerializeToJson(properties);
        }

        internal static IEnumerable<(string name, BindingInfo info, string rawBinding)> GetImplicitOutputBindings(string triggerTypeName)
        {
            var shortName = StripAttributeSuffix(triggerTypeName);
            if (RegisteredBindings.TryGetValue(shortName, out var reg) && reg.ImplicitOutputType != null)
            {
                var bindingName = "Response";
                var bindingInfo = new BindingInfo
                {
                    Type = reg.ImplicitOutputType,
                    Direction = BindingInfo.Types.Direction.Out
                };

                var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "name", bindingName },
                    { "type", reg.ImplicitOutputType },
                    { "direction", "out" }
                };

                yield return (bindingName, bindingInfo, SerializeToJson(properties));
            }
        }

        /// <summary>
        /// Returns the names of required properties that are missing from the given attribute AST.
        /// </summary>
        internal static IEnumerable<string> GetMissingRequiredProperties(AttributeAst attributeAst)
        {
            var reg = ResolveRegistration(attributeAst);
            if (reg == null || reg.RequiredPropertyNames.Count == 0)
            {
                yield break;
            }

            var providedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (attributeAst.NamedArguments != null)
            {
                foreach (var namedArg in attributeAst.NamedArguments)
                {
                    providedNames.Add(namedArg.ArgumentName);
                }
            }

            foreach (var required in reg.RequiredPropertyNames)
            {
                if (!providedNames.Contains(required))
                {
                    yield return required;
                }
            }
        }

        /// <summary>
        /// Gets the binding type string for the given attribute AST.
        /// </summary>
        internal static string GetBindingType(AttributeAst attributeAst)
        {
            var reg = ResolveRegistration(attributeAst);
            return reg?.BindingType;
        }

        #endregion

        #region Resolution helpers

        private static BindingRegistration ResolveRegistration(AttributeAst attributeAst)
        {
            var shortName = GetAttributeTypeName(attributeAst);
            RegisteredBindings.TryGetValue(shortName, out var reg);
            return reg;
        }

        private static bool IsGenericAttribute(AttributeAst attributeAst)
        {
            return GenericAttributeNames.Contains(GetAttributeTypeName(attributeAst));
        }

        private static void ResolveBindingTypeAndDirection(
            AttributeAst attributeAst,
            out string bindingType,
            out BindingDirection direction)
        {
            // Try registered (built-in) bindings first
            var reg = ResolveRegistration(attributeAst);
            if (reg != null)
            {
                bindingType = reg.BindingType;
                direction = reg.Direction;
                return;
            }

            // Handle generic bindings — read Type from AST
            var shortName = GetAttributeTypeName(attributeAst);
            if (GenericAttributeNames.Contains(shortName))
            {
                bindingType = GetNamedArgumentValue(attributeAst, "Type")
                    ?? throw new InvalidOperationException(
                        string.Format(PowerShellWorkerStrings.UnknownBindingAttribute, shortName + " (missing Type)"));

                if (shortName.Equals("GenericTrigger", StringComparison.OrdinalIgnoreCase))
                    direction = BindingDirection.Trigger;
                else if (shortName.Equals("GenericInputBinding", StringComparison.OrdinalIgnoreCase))
                    direction = BindingDirection.In;
                else
                    direction = BindingDirection.Out;
                return;
            }

            throw new InvalidOperationException(
                string.Format(PowerShellWorkerStrings.UnknownBindingAttribute, shortName));
        }

        #endregion

        #region Generic property extraction

        /// <summary>
        /// Extracts ALL named arguments from the attribute AST and adds them to the properties dict
        /// using camelCase keys. Skips keys already present (name, type, direction) and excluded keys.
        /// Properties listed in arrayPropertyNames are always serialized as arrays.
        /// </summary>
        private static void AddAllNamedArguments(AttributeAst attributeAst, Dictionary<string, object> properties, HashSet<string> arrayPropertyNames = null)
        {
            if (attributeAst.NamedArguments == null) return;

            foreach (var namedArg in attributeAst.NamedArguments)
            {
                var argName = namedArg.ArgumentName;

                // Skip framework-internal properties and already-set keys
                if (ExcludedPropertyNames.Contains(argName)) continue;

                var camelKey = ToCamelCase(argName);
                if (properties.ContainsKey(camelKey)) continue;

                bool forceArray = arrayPropertyNames != null && arrayPropertyNames.Contains(argName);

                // Try to extract the value as the appropriate type
                var arrayVal = ExtractArrayValue(namedArg.Argument);
                if (arrayVal != null)
                {
                    properties[camelKey] = arrayVal;
                    continue;
                }

                var boolVal = ExtractBooleanValue(namedArg.Argument);
                if (!forceArray && boolVal.HasValue)
                {
                    properties[camelKey] = boolVal.Value;
                    continue;
                }

                var intVal = ExtractIntegerValue(namedArg.Argument);
                if (!forceArray && intVal.HasValue)
                {
                    properties[camelKey] = intVal.Value;
                    continue;
                }

                var strVal = ExtractConstantStringValue(namedArg.Argument);
                if (strVal != null)
                {
                    // Split on commas if the string contains them, or if this property
                    // is declared as an array property (always emit as JSON array).
                    if (strVal.Contains(',') || forceArray)
                    {
                        properties[camelKey] = strVal.Split(',')
                            .Select(v => v.Trim())
                            .Where(v => v.Length > 0)
                            .ToArray();
                    }
                    else
                    {
                        properties[camelKey] = strVal;
                    }
                }
            }
        }

        /// <summary>
        /// For generic bindings, flattens Properties = "key1=val1; key2=val2"
        /// into individual raw binding properties.
        /// </summary>
        private static void FlattenGenericProperties(AttributeAst attributeAst, Dictionary<string, object> properties)
        {
            var propsString = GetNamedArgumentValue(attributeAst, "Properties");
            if (string.IsNullOrWhiteSpace(propsString)) return;

            foreach (var entry in propsString.Split(';'))
            {
                var trimmed = entry.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = trimmed.Substring(0, eqIndex).Trim();
                    var value = trimmed.Substring(eqIndex + 1).Trim();
                    if (!properties.ContainsKey(key))
                    {
                        properties[key] = value;
                    }
                }
            }
        }

        #endregion

        #region AST value extraction

        internal static string GetNamedArgumentValue(AttributeAst attributeAst, string argumentName)
        {
            if (attributeAst.NamedArguments == null) return null;

            foreach (var namedArg in attributeAst.NamedArguments)
            {
                if (namedArg.ArgumentName.Equals(argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractConstantStringValue(namedArg.Argument);
                }
            }
            return null;
        }

        internal static string[] GetNamedArgumentArrayValue(AttributeAst attributeAst, string argumentName)
        {
            if (attributeAst.NamedArguments == null) return null;

            foreach (var namedArg in attributeAst.NamedArguments)
            {
                if (namedArg.ArgumentName.Equals(argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractArrayValue(namedArg.Argument);
                }
            }
            return null;
        }

        private static string ExtractConstantStringValue(ExpressionAst expressionAst)
        {
            switch (expressionAst)
            {
                case StringConstantExpressionAst stringAst:
                    return stringAst.Value;
                case ConstantExpressionAst constantAst when constantAst.Value is string s:
                    return s;
                case ConstantExpressionAst constantAst:
                    return constantAst.Value?.ToString();
                default:
                    return null;
            }
        }

        private static bool? ExtractBooleanValue(ExpressionAst expressionAst)
        {
            // PowerShell $true/$false parse as VariableExpressionAst or ConstantExpressionAst
            if (expressionAst is VariableExpressionAst varAst)
            {
                if (varAst.VariablePath.UserPath.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (varAst.VariablePath.UserPath.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (expressionAst is ConstantExpressionAst constAst && constAst.Value is bool b)
            {
                return b;
            }
            // Named argument with ExpressionOmitted means [Attr(Flag)] which is $true
            return expressionAst == null ? true : (bool?)null;
        }

        private static int? ExtractIntegerValue(ExpressionAst expressionAst)
        {
            if (expressionAst is ConstantExpressionAst constAst && constAst.Value is int i)
                return i;
            // Handle long constants that fit in int
            if (expressionAst is ConstantExpressionAst longConst && longConst.Value is long l && l >= int.MinValue && l <= int.MaxValue)
                return (int)l;
            return null;
        }

        private static string[] ExtractArrayValue(ExpressionAst expressionAst)
        {
            switch (expressionAst)
            {
                case ArrayLiteralAst arrayAst:
                    return arrayAst.Elements
                        .Select(ExtractConstantStringValue)
                        .Where(v => v != null)
                        .ToArray();

                case ArrayExpressionAst arrayExprAst:
                    var elements = new List<string>();
                    foreach (var statement in arrayExprAst.SubExpression.Statements)
                    {
                        if (statement is PipelineAst pipeline)
                        {
                            foreach (var command in pipeline.PipelineElements)
                            {
                                if (command is CommandExpressionAst cmdExpr)
                                {
                                    if (cmdExpr.Expression is ArrayLiteralAst innerArray)
                                    {
                                        elements.AddRange(innerArray.Elements
                                            .Select(ExtractConstantStringValue)
                                            .Where(v => v != null));
                                    }
                                    else
                                    {
                                        var val = ExtractConstantStringValue(cmdExpr.Expression);
                                        if (val != null) elements.Add(val);
                                    }
                                }
                            }
                        }
                    }
                    return elements.Count > 0 ? elements.ToArray() : null;

                // Only match actual array-typed AST nodes, not plain strings.
                // Plain strings are handled by ExtractConstantStringValue.
                default:
                    return null;
            }
        }

        #endregion

        #region Serialization / Utility

        private static string GetAttributeTypeName(AttributeAst attributeAst)
        {
            return StripAttributeSuffix(attributeAst.TypeName.Name);
        }

        private static string StripAttributeSuffix(string name)
        {
            if (name.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - "Attribute".Length);
            return name;
        }

        private static string ToCamelCase(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase)) return pascalCase;
            if (char.IsLower(pascalCase[0])) return pascalCase;
            return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
        }

        private static BindingInfo.Types.Direction ToGrpcDirection(BindingDirection direction)
        {
            switch (direction)
            {
                case BindingDirection.Trigger: return BindingInfo.Types.Direction.In;
                case BindingDirection.In: return BindingInfo.Types.Direction.In;
                case BindingDirection.Out: return BindingInfo.Types.Direction.Out;
                default: return BindingInfo.Types.Direction.In;
            }
        }

        private static string DirectionToString(BindingDirection direction)
        {
            switch (direction)
            {
                case BindingDirection.Trigger: return "in";
                case BindingDirection.In: return "in";
                case BindingDirection.Out: return "out";
                default: return "in";
            }
        }

        private static string SerializeToJson(Dictionary<string, object> properties)
        {
            var parts = new List<string>();
            foreach (var kvp in properties)
            {
                if (kvp.Value is string[] arrayVal)
                {
                    var items = string.Join(",", arrayVal.Select(v => $"\"{EscapeJsonString(v)}\""));
                    parts.Add($"\"{EscapeJsonString(kvp.Key)}\":[{items}]");
                }
                else if (kvp.Value is bool boolVal)
                {
                    parts.Add($"\"{EscapeJsonString(kvp.Key)}\":{(boolVal ? "true" : "false")}");
                }
                else if (kvp.Value is int intVal)
                {
                    parts.Add($"\"{EscapeJsonString(kvp.Key)}\":{intVal}");
                }
                else if (kvp.Value is string strVal)
                {
                    parts.Add($"\"{EscapeJsonString(kvp.Key)}\":\"{EscapeJsonString(strVal)}\"");
                }
                else if (kvp.Value != null)
                {
                    parts.Add($"\"{EscapeJsonString(kvp.Key)}\":\"{EscapeJsonString(kvp.Value.ToString())}\"");
                }
            }
            return "{" + string.Join(",", parts) + "}";
        }

        private static string EscapeJsonString(string value)
        {
            return value?.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        #endregion
    }
}
