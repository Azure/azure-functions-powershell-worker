//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.Azure.Functions.PowerShellWorker;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    public class FunctionLoaderTests
    {
        [Fact]
        public void TestFunctionLoaderGetFunc()
        {
            var functionId = Guid.NewGuid().ToString();
            var directory = "/Users/tylerleonhardt/Desktop/Tech/PowerShell/AzureFunctions/azure-functions-powershell-worker/examples/PSCoreApp/MyHttpTrigger";
            var scriptPathExpected = $"{directory}/run.ps1";
            var metadata = new RpcFunctionMetadata
            {
                Name = "MyHttpTrigger",
                EntryPoint = "",
                Directory = directory,
                ScriptFile = scriptPathExpected
            };
            metadata.Bindings.Add("req", new BindingInfo
            {
                Direction = BindingInfo.Types.Direction.In,
                Type = "httpTrigger"
            });
            metadata.Bindings.Add("res", new BindingInfo
            {
                Direction = BindingInfo.Types.Direction.Out,
                Type = "http"
            });

            var functionLoadRequest = new FunctionLoadRequest{
                FunctionId = functionId,
                Metadata = metadata
            };

            var functionLoader = new FunctionLoader();
            functionLoader.Load(functionLoadRequest);

            var funcInfo = functionLoader.GetFunctionInfo(functionId);

            Assert.Equal(scriptPathExpected, funcInfo.ScriptPath);
            Assert.Equal("", funcInfo.EntryPoint);
        }

        [Fact]
        public void TestFunctionLoaderGetFuncWithEntryPoint()
        {
            var functionId = Guid.NewGuid().ToString();
            var directory = "/Users/tylerleonhardt/Desktop/Tech/PowerShell/AzureFunctions/azure-functions-powershell-worker/examples/PSCoreApp/MyHttpTrigger";
            var scriptPathExpected = $"{directory}/run.ps1";
            var entryPointExpected = "Foo";
            var metadata = new RpcFunctionMetadata
            {
                Name = "MyHttpTrigger",
                EntryPoint = entryPointExpected,
                Directory = directory,
                ScriptFile = scriptPathExpected
            };
            metadata.Bindings.Add("req", new BindingInfo
            {
                Direction = BindingInfo.Types.Direction.In,
                Type = "httpTrigger"
            });
            metadata.Bindings.Add("res", new BindingInfo
            {
                Direction = BindingInfo.Types.Direction.Out,
                Type = "http"
            });

            var functionLoadRequest = new FunctionLoadRequest{
                FunctionId = functionId,
                Metadata = metadata
            };

            var functionLoader = new FunctionLoader();
            functionLoader.Load(functionLoadRequest);

            var funcInfo = functionLoader.GetFunctionInfo(functionId);

            Assert.Equal(scriptPathExpected, funcInfo.ScriptPath);
            Assert.Equal(entryPointExpected, funcInfo.EntryPoint);
        }

        [Fact]
        public void TestFunctionLoaderGetInfo()
        {
            var functionId = Guid.NewGuid().ToString();
            var directory = "/Users/tylerleonhardt/Desktop/Tech/PowerShell/AzureFunctions/azure-functions-powershell-worker/examples/PSCoreApp/MyHttpTrigger";
            var scriptPathExpected = $"{directory}/run.ps1";
            var name = "MyHttpTrigger";
            var metadata = new RpcFunctionMetadata
            {
                Name = name,
                EntryPoint = "",
                Directory = directory,
                ScriptFile = scriptPathExpected
            };
            metadata.Bindings.Add("req", new BindingInfo
            {
                Direction = BindingInfo.Types.Direction.In,
                Type = "httpTrigger"
            });
            metadata.Bindings.Add("res", new BindingInfo
            {
                Direction = BindingInfo.Types.Direction.Out,
                Type = "http"
            });

            var functionLoadRequest = new FunctionLoadRequest{
                FunctionId = functionId,
                Metadata = metadata
            };

            var functionLoader = new FunctionLoader();
            functionLoader.Load(functionLoadRequest);

            var funcInfo = functionLoader.GetFunctionInfo(functionId);

            Assert.Equal(directory, funcInfo.Directory);
            Assert.Equal(name, funcInfo.FunctionName);
            Assert.Equal(2, funcInfo.AllBindings.Count);
            Assert.Single(funcInfo.OutputBindings);
        }
    }
}
