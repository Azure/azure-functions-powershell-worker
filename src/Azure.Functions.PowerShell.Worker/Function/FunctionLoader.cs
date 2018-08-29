//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class FunctionLoader
    {
        readonly MapField<string, Function> _LoadedFunctions = new MapField<string, Function>();
        
        public (string ScriptPath, string EntryPoint) GetFunc(string functionId) => 
            (_LoadedFunctions[functionId].ScriptPath, _LoadedFunctions[functionId].EntryPoint);
        
        public FunctionInfo GetInfo(string functionId) => _LoadedFunctions[functionId].Info;

        public void Load(string functionId, RpcFunctionMetadata metadata)
        {
            // TODO: catch "load" issues at "func start" time.
            // ex. Script doesn't exist, entry point doesn't exist
            _LoadedFunctions.Add(functionId, new Function
            {
                Info = new FunctionInfo(metadata),
                ScriptPath = metadata.ScriptFile,
                EntryPoint = metadata.EntryPoint
            });
        }
    }

    internal class Function
    {
        public string EntryPoint {get; internal set;}
        public FunctionInfo Info {get; internal set;}
        public string ScriptPath {get; internal set;}
    }
}
