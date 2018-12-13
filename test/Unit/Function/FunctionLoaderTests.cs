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
    public class FunctionLoaderTests
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

        [Fact]
        public void TestFunctionLoaderGetFunc()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "BasicFuncScript.ps1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            var functionLoader = new FunctionLoader();
            functionLoader.LoadFunction(functionLoadRequest);

            var funcInfo = functionLoader.GetFunctionInfo(functionLoadRequest.FunctionId);

            Assert.Equal(scriptFileToUse, funcInfo.ScriptPath);
            Assert.Equal(string.Empty, funcInfo.EntryPoint);

            Assert.Equal(2, funcInfo.FuncParameters.Count);
            Assert.Contains("req", funcInfo.FuncParameters);
            Assert.Contains("inputBlob", funcInfo.FuncParameters);

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

            var functionLoader = new FunctionLoader();
            functionLoader.LoadFunction(functionLoadRequest);

            var funcInfo = functionLoader.GetFunctionInfo(functionLoadRequest.FunctionId);

            Assert.Equal(scriptFileToUse, funcInfo.ScriptPath);
            Assert.Equal(string.Empty, funcInfo.EntryPoint);

            Assert.Equal(3, funcInfo.FuncParameters.Count);
            Assert.Contains("req", funcInfo.FuncParameters);
            Assert.Contains("inputBlob", funcInfo.FuncParameters);
            Assert.Contains("TriggerMetadata", funcInfo.FuncParameters);

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

            var functionLoader = new FunctionLoader();
            functionLoader.LoadFunction(functionLoadRequest);

            var funcInfo = functionLoader.GetFunctionInfo(functionLoadRequest.FunctionId);

            Assert.Equal(scriptFileToUse, funcInfo.ScriptPath);
            Assert.Equal(entryPointToUse, funcInfo.EntryPoint);

            Assert.Equal(2, funcInfo.FuncParameters.Count);
            Assert.Contains("req", funcInfo.FuncParameters);
            Assert.Contains("inputBlob", funcInfo.FuncParameters);

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

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            Assert.Contains("EntryPoint", exception.Message);
            Assert.Contains("(.psm1)", exception.Message);
        }

        [Fact]
        public void Psm1IsSupportedWithEntryPointOnly()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithEntryPoint.psm1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            Assert.Contains("EntryPoint", exception.Message);
            Assert.Contains("(.psm1)", exception.Message);
        }

        [Fact]
        public void ParseErrorInScriptFileShouldBeDetected()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithParseError.ps1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            Assert.Contains("parsing errors", exception.Message);
        }

        [Fact]
        public void EntryPointFunctionShouldExist()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithEntryPoint.psm1");
            var entryPointToUse = "CallMe";
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            Assert.Contains("CallMe", exception.Message);
            Assert.Contains("FuncWithEntryPoint.psm1", exception.Message);
        }

        [Fact]
        public void MultipleEntryPointFunctionsShouldBeDetected()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithMultiEntryPoints.psm1");
            var entryPointToUse = "Run";
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
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

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
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

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
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

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
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

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
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

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("inoutBinding", exception.Message);
            Assert.Contains("InOut", exception.Message);
        }

        [Fact]
        public void ScriptNeedToHaveParameters()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncHasNoParams.ps1");
            var entryPointToUse = string.Empty;
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("req", exception.Message);
            Assert.Contains("inputBlob", exception.Message);
        }

        [Fact]
        public void EntryPointNeedToHaveParameters()
        {
            var scriptFileToUse = Path.Join(_functionDirectory, "FuncWithEntryPoint.psm1");
            var entryPointToUse = "Zoo";
            var functionLoadRequest = GetFuncLoadRequest(scriptFileToUse, entryPointToUse);

            Exception exception = null;
            var functionLoader = new FunctionLoader();
            try
            {
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("req", exception.Message);
            Assert.Contains("inputBlob", exception.Message);
        }
    }
}
