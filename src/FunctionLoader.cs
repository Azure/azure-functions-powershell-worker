//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class FunctionLoader
    {
        private readonly MapField<string, AzFunctionInfo> _loadedFunctions = new MapField<string, AzFunctionInfo>();

        internal AzFunctionInfo GetFunctionInfo(string functionId)
        {
            if (_loadedFunctions.TryGetValue(functionId, out AzFunctionInfo funcInfo))
            {
                return funcInfo;
            }

            throw new InvalidOperationException($"Function with the ID '{functionId}' was not loaded.");
        }

        internal void Load(FunctionLoadRequest request)
        {
            // TODO: catch "load" issues at "func start" time.
            // ex. Script doesn't exist, entry point doesn't exist
            _loadedFunctions.Add(request.FunctionId, new AzFunctionInfo(request.Metadata));
        }
    }

    internal enum AzFunctionType
    {
        None = 0,
        RegularFunction = 1,
        OrchestrationFunction = 2,
        ActivityFunction = 3
    }

    internal class AzFunctionInfo
    {
        private const string OrchestrationClient = "orchestrationClient";
        private const string OrchestrationTrigger = "orchestrationTrigger";
        private const string ActivityTrigger = "activityTrigger";

        internal const string TriggerMetadata = "TriggerMetadata";
        internal const string DollarReturn = "$return";

        internal readonly string Directory;
        internal readonly string EntryPoint;
        internal readonly string FunctionName;
        internal readonly string ScriptPath;
        internal readonly string OrchestrationClientBindingName;
        internal readonly AzFunctionType Type;
        internal readonly MapField<string, BindingInfo> AllBindings;
        internal readonly MapField<string, BindingInfo> OutputBindings;

        internal AzFunctionInfo(RpcFunctionMetadata metadata)
        {
            FunctionName = metadata.Name;
            Directory = metadata.Directory;
            EntryPoint = metadata.EntryPoint;
            ScriptPath = metadata.ScriptFile;

            AllBindings = new MapField<string, BindingInfo>();
            OutputBindings = new MapField<string, BindingInfo>();

            foreach (var binding in metadata.Bindings)
            {
                string bindingName = binding.Key;
                BindingInfo bindingInfo = binding.Value;

                AllBindings.Add(bindingName, bindingInfo);

                // PowerShell doesn't support the 'InOut' type binding
                if (bindingInfo.Direction == BindingInfo.Types.Direction.In)
                {
                    switch (bindingInfo.Type)
                    {
                        case OrchestrationTrigger:
                            Type = AzFunctionType.OrchestrationFunction;
                            break;
                        case ActivityTrigger:
                            Type = AzFunctionType.ActivityFunction;
                            break;
                        default:
                            Type = AzFunctionType.RegularFunction;
                            break;
                    }
                    continue;
                }

                if (bindingInfo.Direction == BindingInfo.Types.Direction.Out)
                {
                    if (bindingInfo.Type == OrchestrationClient)
                    {
                        OrchestrationClientBindingName = bindingName;
                    }
                    OutputBindings.Add(bindingName, bindingInfo);
                }
            }
        }
    }
}
