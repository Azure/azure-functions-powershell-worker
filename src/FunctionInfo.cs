//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
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
        private const string OrchestrationClient = "orchestrationClient";
        private const string OrchestrationTrigger = "orchestrationTrigger";
        private const string ActivityTrigger = "activityTrigger";

        internal const string TriggerMetadata = "TriggerMetadata";
        internal const string TraceContext = "TraceContext";
        internal const string DollarReturn = "$return";

        internal readonly bool HasTriggerMetadataParam;
        internal readonly bool HasTraceContextParam;

        internal readonly string FuncDirectory;
        internal readonly string FuncName;
        internal readonly string EntryPoint;
        internal readonly string ScriptPath;
        internal readonly string OrchestrationClientBindingName;
        internal readonly string DeployedPSFuncName;
        internal readonly AzFunctionType Type;
        internal readonly ScriptBlock FuncScriptBlock;
        internal readonly ReadOnlyDictionary<string, PSScriptParamInfo> FuncParameters;
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
            bool entryPointNotDefined = string.IsNullOrEmpty(EntryPoint);
            if (entryPointNotDefined)
            {
                if (isScriptFilePsm1)
                {
                    throw new ArgumentException(PowerShellWorkerStrings.RequireEntryPointForScriptModule);
                }
            }
            else if (!isScriptFilePsm1)
            {
                throw new ArgumentException(PowerShellWorkerStrings.InvalidEntryPointForScriptFile);
            }

            // Get the parameter names of the script or function.
            var psScriptParams = GetParameters(ScriptPath, EntryPoint, out ScriptBlockAst scriptAst);
            FuncParameters = new ReadOnlyDictionary<string, PSScriptParamInfo>(psScriptParams);

            var parametersCopy = new Dictionary<string, PSScriptParamInfo>(psScriptParams, StringComparer.OrdinalIgnoreCase);
            HasTriggerMetadataParam = parametersCopy.Remove(TriggerMetadata);
            HasTraceContextParam = parametersCopy.Remove(TraceContext);

            var allBindings = new Dictionary<string, ReadOnlyBindingInfo>(StringComparer.OrdinalIgnoreCase);
            var inputBindings = new Dictionary<string, ReadOnlyBindingInfo>(StringComparer.OrdinalIgnoreCase);
            var outputBindings = new Dictionary<string, ReadOnlyBindingInfo>(StringComparer.OrdinalIgnoreCase);

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
                    if (bindingInfo.Type == OrchestrationClient)
                    {
                        OrchestrationClientBindingName = bindingName;
                    }
                    
                    outputBindings.Add(bindingName, bindingInfo);
                }
                else
                {
                    // PowerShell doesn't support the 'InOut' type binding
                    throw new InvalidOperationException(string.Format(PowerShellWorkerStrings.InOutBindingNotSupported, bindingName));
                }
            }

            if (inputsMissingFromParams.Count != 0 || parametersCopy.Count != 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach (string inputBindingName in inputsMissingFromParams)
                {
                    stringBuilder.AppendFormat(PowerShellWorkerStrings.MissingParameter, inputBindingName).AppendLine();
                }

                foreach (string param in parametersCopy.Keys)
                {
                    stringBuilder.AppendFormat(PowerShellWorkerStrings.UnknownParameter, param).AppendLine();
                }

                string errorMsg = stringBuilder.ToString();
                throw new InvalidOperationException(errorMsg);
            }

            if (entryPointNotDefined && scriptAst.ScriptRequirements == null)
            {
                // If the function script is a '.ps1' file that doesn't have '#requires' defined,
                // then we get the script block and will deploy it as a PowerShell function in the
                // global scope of each Runspace, so as to avoid hitting the disk every invocation.
                FuncScriptBlock = scriptAst.GetScriptBlock();
                DeployedPSFuncName = $"_{FuncName}_";
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

        private Dictionary<string, PSScriptParamInfo> GetParameters(string scriptFile, string entryPoint, out ScriptBlockAst scriptAst)
        {
            scriptAst = Parser.ParseFile(scriptFile, out _, out ParseError[] errors);
            if (errors != null && errors.Length > 0)
            {
                var stringBuilder = new StringBuilder(15);
                foreach (var error in errors)
                {
                    stringBuilder.AppendLine(error.Message);
                }

                string errorMsg = stringBuilder.ToString();
                throw new ArgumentException(string.Format(PowerShellWorkerStrings.FailToParseScript, scriptFile, errorMsg));
            }

            ReadOnlyCollection<ParameterAst> paramAsts = null;
            if (string.IsNullOrEmpty(entryPoint))
            {
                paramAsts = scriptAst.ParamBlock?.Parameters;
            }
            else
            {
                var asts = scriptAst.FindAll(
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
                        ? string.Format(PowerShellWorkerStrings.CannotFindEntryPoint, entryPoint, scriptFile)
                        : string.Format(PowerShellWorkerStrings.MultipleEntryPointFound, entryPoint, scriptFile);
                    throw new ArgumentException(errorMsg);
                }
            }

            var parameters = new Dictionary<string, PSScriptParamInfo>(StringComparer.OrdinalIgnoreCase);
            if (paramAsts != null)
            {
                foreach (var paramAst in paramAsts)
                {
                    var psParamInfo = new PSScriptParamInfo(paramAst);
                    parameters.Add(psParamInfo.ParamName, psParamInfo);
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
    /// Represent the metadata of a parameter declared in the PowerShell script.
    /// </summary>
    internal class PSScriptParamInfo
    {
        internal readonly string ParamName;
        internal readonly Type ParamType;

        internal PSScriptParamInfo(ParameterAst paramAst)
        {
            ParamName = paramAst.Name.VariablePath.UserPath;
            ParamType = paramAst.StaticType;
        }
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
