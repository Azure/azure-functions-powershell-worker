//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    public class PowerShellManagerTests
    {
        private const string TestInputBindingName = "req";
        private const string TestOutputBindingName = "res";
        private const string TestStringData = "Foo";

        private readonly string _functionDirectory;
        private readonly ConsoleLogger _testLogger;
        private readonly PowerShellManager _testManager;
        private readonly List<ParameterBinding> _testInputData;
        private readonly RpcFunctionMetadata _rpcFunctionMetadata;
        private readonly FunctionLoadRequest _functionLoadRequest;

        public PowerShellManagerTests()
        {
            _functionDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "TestScripts", "PowerShell");
            _rpcFunctionMetadata = new RpcFunctionMetadata()
            {
                Name = "TestFuncApp",
                Directory = _functionDirectory,
                Bindings =
                {
                    { TestInputBindingName , new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "httpTrigger" } },
                    { TestOutputBindingName, new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "http" } }
                }
            };
            _functionLoadRequest = new FunctionLoadRequest {FunctionId = "FunctionId", Metadata = _rpcFunctionMetadata};
            FunctionLoader.SetupWellKnownPaths(_functionLoadRequest);

            _testLogger = new ConsoleLogger();
            _testManager = new PowerShellManager(_testLogger);
            _testManager.PerformWorkerLevelInitialization();

            _testInputData = new List<ParameterBinding>
            {
                new ParameterBinding
                {
                    Name = TestInputBindingName,
                    Data = new TypedData
                    {
                        String = TestStringData
                    }
                }
            };
        }

        private AzFunctionInfo GetAzFunctionInfo(string scriptFile, string entryPoint)
        {
            _rpcFunctionMetadata.ScriptFile = scriptFile;
            _rpcFunctionMetadata.EntryPoint = entryPoint;
            return new AzFunctionInfo(_rpcFunctionMetadata);
        }

        [Fact]
        public void InvokeBasicFunctionWorks()
        {
            string path = Path.Join(_functionDirectory, "testBasicFunction.ps1");

            var functionInfo = GetAzFunctionInfo(path, string.Empty);
            Hashtable result = _testManager.InvokeFunction(functionInfo, null, _testInputData);

            Assert.Equal(TestStringData, result[TestOutputBindingName]);
        }

        [Fact]
        public void InvokeBasicFunctionWithTriggerMetadataWorks()
        {
            string path = Path.Join(_functionDirectory, "testBasicFunctionWithTriggerMetadata.ps1");
            Hashtable triggerMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase)
            {
                { TestInputBindingName, TestStringData }
            };

            var functionInfo = GetAzFunctionInfo(path, string.Empty);
            Hashtable result = _testManager.InvokeFunction(functionInfo, triggerMetadata, _testInputData);

            Assert.Equal(TestStringData, result[TestOutputBindingName]);
        }

        [Fact]
        public void InvokeFunctionWithEntryPointWorks()
        {
            string path = Path.Join(_functionDirectory, "testFunctionWithEntryPoint.psm1");
            var functionInfo = GetAzFunctionInfo(path, "Run");
            Hashtable result = _testManager.InvokeFunction(functionInfo, null, _testInputData);

            Assert.Equal(TestStringData, result[TestOutputBindingName]);
        }

        [Fact]
        public void FunctionShouldCleanupVariableTable()
        {
            string path = Path.Join(_functionDirectory, "testFunctionCleanup.ps1");
            var functionInfo = GetAzFunctionInfo(path, string.Empty);

            Hashtable result1 = _testManager.InvokeFunction(functionInfo, null, _testInputData);
            Assert.Equal("is not set", result1[TestOutputBindingName]);

            // the value shoould not change if the variable table is properly cleaned up.
            Hashtable result2 = _testManager.InvokeFunction(functionInfo, null, _testInputData);
            Assert.Equal("is not set", result2[TestOutputBindingName]);
        }

        [Fact]
        public void ModulePathShouldBeSetByWorkerLevelInitialization()
        {
            string workerModulePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            string funcAppModulePath = Path.Join(FunctionLoader.FunctionAppRootPath, "Modules");
            string expectedPath = $"{funcAppModulePath}{Path.PathSeparator}{workerModulePath}";
            Assert.Equal(expectedPath, Environment.GetEnvironmentVariable("PSModulePath"));
        }

        [Fact]
        public void RegisterAndUnregisterFunctionMetadataShouldWork()
        {
            string path = Path.Join(_functionDirectory, "testBasicFunction.ps1");
            var functionInfo = GetAzFunctionInfo(path, string.Empty);

            Assert.Empty(FunctionMetadata.OutputBindingCache);
            _testManager.RegisterFunctionMetadata(functionInfo);
            Assert.Single(FunctionMetadata.OutputBindingCache);
            _testManager.UnregisterFunctionMetadata();
            Assert.Empty(FunctionMetadata.OutputBindingCache);
        }

        [Fact]
        public void ProfileShouldWork()
        {
            //initialize fresh log
            _testLogger.FullLog.Clear();
            var funcLoadReq = _functionLoadRequest.Clone();
            funcLoadReq.Metadata.Directory = Path.Join(_functionDirectory, "ProfileBasic", "Func1");

            try
            {
                FunctionLoader.SetupWellKnownPaths(funcLoadReq);
                _testManager.PerformRunspaceLevelInitialization();

                Assert.Single(_testLogger.FullLog);
                Assert.Equal("Information: INFORMATION: Hello PROFILE", _testLogger.FullLog[0]);
            }
            finally
            {
                FunctionLoader.SetupWellKnownPaths(_functionLoadRequest);
            }
        }

        [Fact]
        public void ProfileDoesNotExist()
        {
            //initialize fresh log
            _testLogger.FullLog.Clear();
            var funcLoadReq = _functionLoadRequest.Clone();
            funcLoadReq.Metadata.Directory = AppDomain.CurrentDomain.BaseDirectory;

            try
            {
                FunctionLoader.SetupWellKnownPaths(funcLoadReq);
                _testManager.PerformRunspaceLevelInitialization();

                Assert.Single(_testLogger.FullLog);
                Assert.Matches("Trace: No 'profile.ps1' is found at the FunctionApp root folder: ", _testLogger.FullLog[0]);
            }
            finally
            {
                FunctionLoader.SetupWellKnownPaths(_functionLoadRequest);
            }
        }

        [Fact]
        public void ProfileWithTerminatingError()
        {
            //initialize fresh log
            _testLogger.FullLog.Clear();
            var funcLoadReq = _functionLoadRequest.Clone();
            funcLoadReq.Metadata.Directory = Path.Join(_functionDirectory, "ProfileWithTerminatingError", "Func1");

            try
            {
                FunctionLoader.SetupWellKnownPaths(funcLoadReq);

                Assert.Throws<CmdletInvocationException>(() => _testManager.PerformRunspaceLevelInitialization());
                Assert.Single(_testLogger.FullLog);
                Assert.Matches("Error: Fail to run profile.ps1. See logs for detailed errors. Profile location: ", _testLogger.FullLog[0]);
            }
            finally
            {
                FunctionLoader.SetupWellKnownPaths(_functionLoadRequest);
            }
        }

        [Fact]
        public void ProfileWithNonTerminatingError()
        {
            //initialize fresh log
            _testLogger.FullLog.Clear();
            var funcLoadReq = _functionLoadRequest.Clone();
            funcLoadReq.Metadata.Directory = Path.Join(_functionDirectory, "ProfileWithNonTerminatingError", "Func1");

            try
            {
                FunctionLoader.SetupWellKnownPaths(funcLoadReq);
                _testManager.PerformRunspaceLevelInitialization();

                Assert.Equal(2, _testLogger.FullLog.Count);
                Assert.Equal("Error: ERROR: help me!", _testLogger.FullLog[0]);
                Assert.Matches("Error: Fail to run profile.ps1. See logs for detailed errors. Profile location: ", _testLogger.FullLog[1]);
            }
            finally
            {
                FunctionLoader.SetupWellKnownPaths(_functionLoadRequest);
            }
        }
    }
}
