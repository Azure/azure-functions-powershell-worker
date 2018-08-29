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
        public string Directory {get; set;}
        public string HttpOutputName {get; set;}
        public string Name {get; set;}
        public MapField<string, BindingInfo> Bindings { get; } = new MapField<string, BindingInfo>();
        public MapField<string, BindingInfo> OutputBindings { get; } = new MapField<string, BindingInfo>();

        public FunctionInfo() { }

        public FunctionInfo(RpcFunctionMetadata metadata)
        {
            Name = metadata.Name;
            Directory = metadata.Directory;
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
