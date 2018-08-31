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
        private readonly MapField<string, FunctionInfo> _loadedFunctions = new MapField<string, FunctionInfo>();

        internal FunctionInfo GetFunctionInfo(string functionId)
        {
            if (_loadedFunctions.TryGetValue(functionId, out FunctionInfo funcInfo))
            {
                return funcInfo;
            }

            throw new InvalidOperationException($"Function with the ID '{functionId}' was not loaded.");
        }

        internal void Load(FunctionLoadRequest request)
        {
            // TODO: catch "load" issues at "func start" time.
            // ex. Script doesn't exist, entry point doesn't exist
            _loadedFunctions.Add(request.FunctionId, new FunctionInfo(request.Metadata));
        }
    }

    internal class FunctionInfo
    {
        internal readonly string Directory;
        internal readonly string EntryPoint;
        internal readonly string FunctionName;
        internal readonly string ScriptPath;
        internal readonly MapField<string, BindingInfo> AllBindings;
        internal readonly MapField<string, BindingInfo> OutputBindings;

        public FunctionInfo(RpcFunctionMetadata metadata)
        {
            FunctionName = metadata.Name;
            Directory = metadata.Directory;
            EntryPoint = metadata.EntryPoint;
            ScriptPath = metadata.ScriptFile;

            AllBindings = new MapField<string, BindingInfo>();
            OutputBindings = new MapField<string, BindingInfo>();

            foreach (var binding in metadata.Bindings)
            {
                AllBindings.Add(binding.Key, binding.Value);

                // PowerShell doesn't support the 'InOut' type binding
                if (binding.Value.Direction == BindingInfo.Types.Direction.Out)
                {
                    OutputBindings.Add(binding.Key, binding.Value);
                }
            }
        }
    }
}
