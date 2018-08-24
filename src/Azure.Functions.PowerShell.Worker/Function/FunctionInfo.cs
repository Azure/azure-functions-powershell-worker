//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class FunctionInfo
    {
        public MapField<string, BindingInfo> Bindings {get; private set;}
        public string Directory {get; private set;}
        public string HttpOutputName {get; private set;}
        public string Name {get; private set;}
        public MapField<string, BindingInfo> OutputBindings {get; private set;}

        public FunctionInfo(RpcFunctionMetadata metadata)
        {
            Name = metadata.Name;
            Directory = metadata.Directory;
            Bindings = new MapField<string, BindingInfo>();
            OutputBindings = new MapField<string, BindingInfo>();
            HttpOutputName = "";

            foreach (var binding in metadata.Bindings)
            {
                Bindings.Add(binding.Key, binding.Value);

                // Only add Out and InOut bindings to the OutputBindings
                if (binding.Value.Direction != BindingInfo.Types.Direction.In)
                {
                    if(binding.Value.Type == "http")
                    {
                        HttpOutputName = binding.Key;
                    }
                    OutputBindings.Add(binding.Key, binding.Value);
                }
            }
        }
    }
}