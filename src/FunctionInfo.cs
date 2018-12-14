//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// This type represents the metadata of an Azure PowerShell Function.
    /// </summary>
    internal class AzFunctionInfo
    {
        private const string OrchestrationTrigger = "orchestrationTrigger";
        private const string ActivityTrigger = "activityTrigger";

        internal const string TriggerMetadata = "TriggerMetadata";
        internal const string DollarReturn = "$return";

        internal readonly string FuncDirectory;
        internal readonly string FuncName;
        internal readonly string EntryPoint;
        internal readonly string ScriptPath;
        internal readonly HashSet<string> FuncParameters;
        internal readonly AzFunctionType Type;
        internal readonly ReadOnlyDictionary<string, ReadOnlyBindingInfo> AllBindings;
        internal readonly ReadOnlyDictionary<string, ReadOnlyBindingInfo> InputBindings;
        internal readonly ReadOnlyDictionary<string, ReadOnlyBindingInfo> OutputBindings;

        /// <summary>
        /// Construct an object of AzFunctionInfo from the 'RpcFunctionMetadata'.
        /// Necessary validations are done on the metadata and script.
        /// </summary>
        internal AzFunctionInfo(RpcFunctionMetadata metadata)
        {
            FuncName = metadata.Name;
            FuncDirectory = metadata.Directory;
            EntryPoint = metadata.EntryPoint;
            ScriptPath = metadata.ScriptFile;

            // Support 'entryPoint' only if 'scriptFile' is a .psm1 file;
            // Support .psm1 'scriptFile' only if 'entryPoint' is specified.
            bool isScriptFilePsm1 = ScriptPath.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(EntryPoint))
            {
                if (isScriptFilePsm1)
                {
                    throw new ArgumentException($"The 'entryPoint' property needs to be specified when 'scriptFile' points to a PowerShell module script file (.psm1).");
                }
            }
            else if (!isScriptFilePsm1)
            {
                throw new ArgumentException($"The 'entryPoint' property is supported only if 'scriptFile' points to a PowerShell module script file (.psm1).");
            }

            // Get the parameter names of the script or function.
            FuncParameters = GetParameters(ScriptPath, EntryPoint);
            var parametersCopy = new HashSet<string>(FuncParameters, StringComparer.OrdinalIgnoreCase);
            parametersCopy.Remove(TriggerMetadata);

            var allBindings = new Dictionary<string, ReadOnlyBindingInfo>();
            var inputBindings = new Dictionary<string, ReadOnlyBindingInfo>();
            var outputBindings = new Dictionary<string, ReadOnlyBindingInfo>();

            var inputsMissingFromParams = new List<string>();
            foreach (var binding in metadata.Bindings)
            {
                string bindingName = binding.Key;
                var bindingInfo = new ReadOnlyBindingInfo(binding.Value);

                allBindings.Add(bindingName, bindingInfo);

                if (bindingInfo.Direction == BindingInfo.Types.Direction.In)
                {
                    Type = GetAzFunctionType(bindingInfo);
                    inputBindings.Add(bindingName, bindingInfo);

                    // If the input binding name is in the set, we remove it;
                    // otherwise, the binding name is missing from the params.
                    if (!parametersCopy.Remove(bindingName))
                    {
                        inputsMissingFromParams.Add(bindingName);
                    }
                }
                else if (bindingInfo.Direction == BindingInfo.Types.Direction.Out)
                {
                    outputBindings.Add(bindingName, bindingInfo);
                }
                else
                {
                    // PowerShell doesn't support the 'InOut' type binding
                    throw new InvalidOperationException($"The binding '{bindingName}' is declared with 'InOut' direction, which is not supported by PowerShell functions.");
                }
            }

            if (inputsMissingFromParams.Count != 0 || parametersCopy.Count != 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach (string inputBindingName in inputsMissingFromParams)
                {
                    stringBuilder.AppendLine($"No parameter defined in the script or function for the input binding '{inputBindingName}'.");
                }

                foreach (string param in parametersCopy)
                {
                    stringBuilder.AppendLine($"No input binding defined for the parameter '{param}' that is declared in the script or function.");
                }

                string errorMsg = stringBuilder.ToString();
                throw new InvalidOperationException(errorMsg);
            }

            AllBindings = new ReadOnlyDictionary<string, ReadOnlyBindingInfo>(allBindings);
            InputBindings = new ReadOnlyDictionary<string, ReadOnlyBindingInfo>(inputBindings);
            OutputBindings = new ReadOnlyDictionary<string, ReadOnlyBindingInfo>(outputBindings);
        }

        private AzFunctionType GetAzFunctionType(ReadOnlyBindingInfo bindingInfo)
        {
            switch (bindingInfo.Type)
            {
                case OrchestrationTrigger:
                    return AzFunctionType.OrchestrationFunction;
                case ActivityTrigger:
                    return AzFunctionType.ActivityFunction;
                default:
                    // All other triggers are considered regular functions
                    return AzFunctionType.RegularFunction;
            }
        }

        private HashSet<string> GetParameters(string scriptFile, string entryPoint)
        {
            ScriptBlockAst sbAst = Parser.ParseFile(scriptFile, out _, out ParseError[] errors);
            if (errors != null && errors.Length > 0)
            {
                var stringBuilder = new StringBuilder(15);
                foreach (var error in errors)
                {
                    stringBuilder.AppendLine(error.Message);
                }

                string errorMsg = stringBuilder.ToString();
                throw new ArgumentException($"The script file '{scriptFile}' has parsing errors:\n{errorMsg}");
            }

            ReadOnlyCollection<ParameterAst> paramAsts = null;
            if (string.IsNullOrEmpty(entryPoint))
            {
                paramAsts = sbAst.ParamBlock?.Parameters;
            }
            else
            {
                var asts = sbAst.FindAll(
                    ast => ast is FunctionDefinitionAst func && entryPoint.Equals(func.Name, StringComparison.OrdinalIgnoreCase),
                    searchNestedScriptBlocks: false).ToList();

                if (asts.Count == 1)
                {
                    var funcAst = (FunctionDefinitionAst) asts[0];
                    paramAsts = funcAst.Parameters ?? funcAst.Body.ParamBlock?.Parameters;
                }
                else
                {
                    string errorMsg = asts.Count == 0
                        ? $"Cannot find the function '{entryPoint}' defined in '{scriptFile}'"
                        : $"More than one functions named '{entryPoint}' are found in '{scriptFile}'";
                    throw new ArgumentException(errorMsg);
                }
            }

            HashSet<string> parameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (paramAsts != null)
            {
                foreach (var paramAst in paramAsts)
                {
                    parameters.Add(paramAst.Name.VariablePath.UserPath);
                }
            }

            return parameters;
        }
    }

    /// <summary>
    /// Type of the Azure Function.
    /// </summary>
    internal enum AzFunctionType
    {
        None = 0,
        RegularFunction = 1,
        OrchestrationFunction = 2,
        ActivityFunction = 3
    }

    /// <summary>
    /// A read-only type that represents a BindingInfo.
    /// </summary>
    public class ReadOnlyBindingInfo
    {
        internal ReadOnlyBindingInfo(BindingInfo bindingInfo)
        {
            Type = bindingInfo.Type;
            Direction = bindingInfo.Direction;
        }

        /// <summary>
        /// The type of the binding.
        /// </summary>
        public readonly string Type;

        /// <summary>
        /// The direction of the binding.
        /// </summary>
        public readonly BindingInfo.Types.Direction Direction; 
    }
}
