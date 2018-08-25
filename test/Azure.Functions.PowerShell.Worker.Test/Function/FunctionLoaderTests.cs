using System;
using Microsoft.Azure.Functions.PowerShellWorker;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace Azure.Functions.PowerShell.Worker.Test
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

            var functionLoader = new FunctionLoader();
            functionLoader.Load(functionId, metadata);

            (string scriptPathResult, string entryPointResult) = functionLoader.GetFunc(functionId);

            Assert.Equal(scriptPathExpected, scriptPathResult);
            Assert.Equal("", entryPointResult);
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

            var functionLoader = new FunctionLoader();
            functionLoader.Load(functionId, metadata);

            (string scriptPathResult, string entryPointResult) = functionLoader.GetFunc(functionId);

            Assert.Equal(scriptPathExpected, scriptPathResult);
            Assert.Equal(entryPointExpected, entryPointResult);
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

            var infoExpected = new FunctionInfo
            {
                Directory = directory,
                HttpOutputName = "",
                Name = name
            };
            infoExpected.Bindings.Add("req", new BindingInfo
            {
                Direction = BindingInfo.Types.Direction.In,
                Type = "httpTrigger"
            });
            infoExpected.Bindings.Add("res", new BindingInfo
            {
                Direction = BindingInfo.Types.Direction.Out,
                Type = "http"
            });

            infoExpected.OutputBindings.Add("res", new BindingInfo
            {
                Direction = BindingInfo.Types.Direction.Out,
                Type = "http"
            });

            var functionLoader = new FunctionLoader();
            functionLoader.Load(functionId, metadata);

            var infoResult = functionLoader.GetInfo(functionId);

            Assert.Equal(directory, infoResult.Directory);
            Assert.Equal("res", infoResult.HttpOutputName);
            Assert.Equal(name, infoResult.Name);
            Assert.Equal(infoExpected.Bindings.Count, infoResult.Bindings.Count);
            Assert.Equal(infoExpected.OutputBindings.Count, infoResult.OutputBindings.Count);
        }
    }
}
