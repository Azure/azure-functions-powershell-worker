//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class WorkerIndexingHelper
    {
        internal static IEnumerable<RpcFunctionMetadata> IndexFunctions()
        {
            List<RpcFunctionMetadata> rpcFunctionMetadatas = new List<RpcFunctionMetadata>();

            RpcFunctionMetadata firstFunction = new RpcFunctionMetadata();

            BindingInfo requestInfo = new BindingInfo();
            requestInfo.Direction = BindingInfo.Types.Direction.In;
            requestInfo.Type = "httpTrigger";

            BindingInfo responseInfo = new BindingInfo();
            responseInfo.Direction = BindingInfo.Types.Direction.Out;
            responseInfo.Type = "http";

            firstFunction.Bindings.Add("Request", requestInfo);
            firstFunction.Bindings.Add("Response", responseInfo);
            
            firstFunction.Directory = "C:\\Users\\t-anstaples\\source\\powershell\\apat2";
            firstFunction.ScriptFile = "HttpTrigger1\\run.ps1";
            firstFunction.EntryPoint = "";
            firstFunction.Name = "HttpTrigger1";
            //StatusResult functionStatusResult = new StatusResult();
            //functionStatusResult.Status = StatusResult.Types.Status.Failure;
            //firstFunction.Status = functionStatusResult;

            firstFunction.FunctionId = Guid.NewGuid().ToString();

            firstFunction.RawBindings.Add("{\"authLevel\":\"function\",\"type\":\"httpTrigger\",\"direction\":\"in\",\"name\":\"Request\",\"methods\":[\"get\",\"post\"]}");
            firstFunction.RawBindings.Add("{\"type\":\"http\",\"direction\":\"out\",\"name\":\"Response\"}");

            rpcFunctionMetadatas.Add(firstFunction);

            return rpcFunctionMetadatas;
        }
    }
}