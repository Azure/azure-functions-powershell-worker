//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    public class FunctionLoaderTests : IDisposable
    {
        private readonly string _functionDirectory;
        private readonly FunctionLoadRequest _functionLoadRequest;

        public FunctionLoaderTests()
        {
            _functionDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestScripts", "Function");

            var functionId = Guid.NewGuid().ToString();
            var metadata = new RpcFunctionMetadata
            {
                Name = "MyHttpTrigger",
                Directory = _functionDirectory,
                Bindings =
                {
                    { "req", new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "httpTrigger" } },
                    { "inputBlob", new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "blobTrigger" } },
                    { "res", new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "http" } }
                }
            };

            _functionLoadRequest = new FunctionLoadRequest
            {
                FunctionId = functionId,
                Metadata = metadata
            };
        }

        private FunctionLoadRequest GetFuncLoadRequest(string scriptFile, string entryPoint)
        {
            var functionLoadRequest = _functionLoadRequest.Clone();
            functionLoadRequest.Metadata.ScriptFile = scriptFile;
            functionLoadRequest.Metadata.EntryPoint = entryPoint;
            return functionLoadRequest;
        }

        public void Dispose()
        {
            FunctionLoader.ClearLoadedFunctions();
        }

        [Fact]
        public void TestFunctionLoaderGetFunc()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "BasicFuncScript.ps1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            FunctionLoader.LoadFunction(functionLoadRequest);
            var funcInfo = FunctionLoader.GetFunctionInfo(functionLoadRequest.FunctionId);

            Assert.Equal(scriptFileToUse, funcInfo.ScriptPath);
            Assert.Equal(string.Empty, funcInfo.EntryPoint);

            Assert.NotNull(funcInfo.FuncScriptBlock);

            Assert.Equal(2, funcInfo.FuncParameters.Count);
            Assert.True(funcInfo.FuncParameters.ContainsKey("req"));
            Assert.True(funcInfo.FuncParameters.ContainsKey("inputBlob"));

            Assert.Equal(3, funcInfo.AllBindings.Count);
            Assert.Equal(2, funcInfo.InputBindings.Count);
            Assert.Single(funcInfo.OutputBindings);
        }

        [Fact]
        public void TestFunctionLoaderGetFuncWithRequires()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithRequires.ps1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            FunctionLoader.LoadFunction(functionLoadRequest);
            var funcInfo = FunctionLoader.GetFunctionInfo(functionLoadRequest.FunctionId);

            Assert.Equal(scriptFileToUse, funcInfo.ScriptPath);
            Assert.Equal(string.Empty, funcInfo.EntryPoint);

            Assert.Null(funcInfo.FuncScriptBlock);

            Assert.Equal(2, funcInfo.FuncParameters.Count);
            Assert.True(funcInfo.FuncParameters.ContainsKey("req"));
            Assert.True(funcInfo.FuncParameters.ContainsKey("inputBlob"));

            Assert.Equal(3, funcInfo.AllBindings.Count);
            Assert.Equal(2, funcInfo.InputBindings.Count);
            Assert.Single(funcInfo.OutputBindings);
        }

        [Fact]
        public void TestFunctionLoaderGetFuncWithTriggerMetadataParam()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "BasicFuncScriptWithTriggerMetadata.ps1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            FunctionLoader.LoadFunction(functionLoadRequest);
            var funcInfo = FunctionLoader.GetFunctionInfo(functionLoadRequest.FunctionId);

            Assert.Equal(scriptFileToUse, funcInfo.ScriptPath);
            Assert.Equal(string.Empty, funcInfo.EntryPoint);

            Assert.NotNull(funcInfo.FuncScriptBlock);

            Assert.Equal(3, funcInfo.FuncParameters.Count);
            Assert.True(funcInfo.FuncParameters.ContainsKey("req"));
            Assert.True(funcInfo.FuncParameters.ContainsKey("inputBlob"));
            Assert.True(funcInfo.FuncParameters.ContainsKey("TriggerMetadata"));

            Assert.Equal(3, funcInfo.AllBindings.Count);
            Assert.Equal(2, funcInfo.InputBindings.Count);
            Assert.Single(funcInfo.OutputBindings);
        }

        [Fact]
        public void TestFunctionLoaderGetFuncWithEntryPoint()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithEntryPoint.psm1");
            var entryPointToUse = "Run";
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            FunctionLoader.LoadFunction(functionLoadRequest);
            var funcInfo = FunctionLoader.GetFunctionInfo(functionLoadRequest.FunctionId);

            Assert.Equal(scriptFileToUse, funcInfo.ScriptPath);
            Assert.Equal(entryPointToUse, funcInfo.EntryPoint);

            Assert.Null(funcInfo.FuncScriptBlock);

            Assert.Equal(2, funcInfo.FuncParameters.Count);
            Assert.True(funcInfo.FuncParameters.ContainsKey("req"));
            Assert.True(funcInfo.FuncParameters.ContainsKey("inputBlob"));

            Assert.Equal(3, funcInfo.AllBindings.Count);
            Assert.Equal(2, funcInfo.InputBindings.Count);
            Assert.Single(funcInfo.OutputBindings);
        }

        [Fact]
        public void EntryPointIsSupportedWithPsm1FileOnly()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "BasicFuncScript.ps1");
            var entryPointToUse = "Run";
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            var exception = Assert.Throws<ArgumentException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("entryPoint", exception.Message);
            Assert.Contains("(.psm1)", exception.Message);
        }

        [Fact]
        public void Psm1IsSupportedWithEntryPointOnly()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithEntryPoint.psm1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            var exception = Assert.Throws<ArgumentException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("entryPoint", exception.Message);
            Assert.Contains("(.psm1)", exception.Message);
        }

        [Fact]
        public void ParseErrorInScriptFileShouldBeDetected()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithParseError.ps1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            var exception = Assert.Throws<ArgumentException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("parsing errors", exception.Message);
        }

        [Fact]
        public void EntryPointFunctionShouldExist()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithEntryPoint.psm1");
            var entryPointToUse = "CallMe";
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            var exception = Assert.Throws<ArgumentException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("CallMe", exception.Message);
            Assert.Contains("FuncWithEntryPoint.psm1", exception.Message);
        }

        [Fact]
        public void MultipleEntryPointFunctionsShouldBeDetected()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithMultiEntryPoints.psm1");
            var entryPointToUse = "Run";
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            var exception = Assert.Throws<ArgumentException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("Run", exception.Message);
            Assert.Contains("FuncWithMultiEntryPoints.psm1", exception.Message);
        }

        [Fact]
        public void ParametersShouldMatchInputBinding()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "BasicFuncScript.ps1");
            var entryPointToUse = string.Empty;

            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);
            functionLoadRequest.Metadata.Bindings.Add("inputTable", new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "tableTrigger" });
            functionLoadRequest.Metadata.Bindings.Remove("inputBlob");

            var exception = Assert.Throws<InvalidOperationException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("inputTable", exception.Message);
            Assert.Contains("inputBlob", exception.Message);
        }

        [Fact]
        public void ParametersShouldMatchInputBindingWithTriggerMetadataParam()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "BasicFuncScriptWithTriggerMetadata.ps1");
            var entryPointToUse = string.Empty;

            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);
            functionLoadRequest.Metadata.Bindings.Add("inputTable", new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "tableTrigger" });
            functionLoadRequest.Metadata.Bindings.Remove("inputBlob");

            var exception = Assert.Throws<InvalidOperationException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("inputTable", exception.Message);
            Assert.Contains("inputBlob", exception.Message);
        }

        [Fact]
        public void EntryPointParametersShouldMatchInputBinding()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithEntryPoint.psm1");
            var entryPointToUse = "Run";

            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);
            functionLoadRequest.Metadata.Bindings.Add("inputTable", new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "tableTrigger" });
            functionLoadRequest.Metadata.Bindings.Remove("inputBlob");

            var exception = Assert.Throws<InvalidOperationException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("inputTable", exception.Message);
            Assert.Contains("inputBlob", exception.Message);
        }

        [Fact]
        public void EntryPointParametersShouldMatchInputBindingWithTriggerMetadataParam()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithEntryPointAndTriggerMetadata.psm1");
            var entryPointToUse = "Run";

            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);
            functionLoadRequest.Metadata.Bindings.Add("inputTable", new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "tableTrigger" });
            functionLoadRequest.Metadata.Bindings.Remove("inputBlob");

            var exception = Assert.Throws<InvalidOperationException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("inputTable", exception.Message);
            Assert.Contains("inputBlob", exception.Message);
        }

        [Fact]
        public void InOutBindingIsNotSupported()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "BasicFuncScript.ps1");
            var entryPointToUse = string.Empty;

            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);
            functionLoadRequest.Metadata.Bindings.Add("inoutBinding", new BindingInfo { Direction = BindingInfo.Types.Direction.Inout, Type = "queue" });

            var exception = Assert.Throws<InvalidOperationException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("inoutBinding", exception.Message);
            Assert.Contains("InOut", exception.Message);
        }

        [Fact]
        public void ScriptNeedToHaveParameters()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncHasNoParams.ps1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            var exception = Assert.Throws<InvalidOperationException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("req", exception.Message);
            Assert.Contains("inputBlob", exception.Message);
        }

        [Fact]
        public void EntryPointNeedToHaveParameters()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithEntryPoint.psm1");
            var entryPointToUse = "Zoo";
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            var exception = Assert.Throws<InvalidOperationException>(
                () => FunctionLoader.LoadFunction(functionLoadRequest));
            Assert.Contains("req", exception.Message);
            Assert.Contains("inputBlob", exception.Message);
        }
    }
}
