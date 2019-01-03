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
    internal class TestUtils
    {
        internal const string TestInputBindingName = "req";
        internal const string TestOutputBindingName = "res";

        internal static readonly string FunctionDirectory;
        internal static readonly RpcFunctionMetadata RpcFunctionMetadata;
        internal static readonly FunctionLoadRequest FunctionLoadRequest;

        static TestUtils()
        {
            FunctionDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "TestScripts", "PowerShell");
            RpcFunctionMetadata = new RpcFunctionMetadata()
            {
                Name = "TestFuncApp",
                Directory = FunctionDirectory,
                Bindings =
                {
                    { TestInputBindingName , new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "httpTrigger" } },
                    { TestOutputBindingName, new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "http" } }
                }
            };

            FunctionLoadRequest = new FunctionLoadRequest {FunctionId = "FunctionId", Metadata = RpcFunctionMetadata};
            FunctionLoader.SetupWellKnownPaths(FunctionLoadRequest);
        }

        // Have a single place to get a PowerShellManager for testing.
        // This is to guarantee that the well known paths are setup before calling the constructor of PowerShellManager.
        internal static PowerShellManager NewTestPowerShellManager(ConsoleLogger logger)
        {
            return new PowerShellManager(logger);
        }

        internal static AzFunctionInfo NewAzFunctionInfo(string scriptFile, string entryPoint)
        {
            RpcFunctionMetadata.ScriptFile = scriptFile;
            RpcFunctionMetadata.EntryPoint = entryPoint;
            RpcFunctionMetadata.Directory = Path.GetDirectoryName(scriptFile);
            return new AzFunctionInfo(RpcFunctionMetadata);
        }

        // Helper method to wait for debugger to attach and set a breakpoint.
        internal static void Break()
        {
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(200);
            }
            System.Diagnostics.Debugger.Break();
        }
    }

    public class PowerShellManagerTests
    {
        private const string TestStringData = "Foo";

        private readonly ConsoleLogger _testLogger;
        private readonly PowerShellManager _testManager;
        private readonly List<ParameterBinding> _testInputData;

        public PowerShellManagerTests()
        {
            _testLogger = new ConsoleLogger();
            _testManager = TestUtils.NewTestPowerShellManager(_testLogger);

            _testInputData = new List<ParameterBinding>
            {
                new ParameterBinding
                {
                    Name = TestUtils.TestInputBindingName,
                    Data = new TypedData
                    {
                        String = TestStringData
                    }
                }
            };
        }

        [Fact]
        public void InvokeBasicFunctionWorks()
        {
            string path = Path.Join(TestUtils.FunctionDirectory, "testBasicFunction.ps1");

            var functionInfo = TestUtils.NewAzFunctionInfo(path, string.Empty);
            Hashtable result = _testManager.InvokeFunction(functionInfo, null, _testInputData);

            Assert.Equal(TestStringData, result[TestUtils.TestOutputBindingName]);
        }

        [Fact]
        public void InvokeBasicFunctionWithTriggerMetadataWorks()
        {
            string path = Path.Join(TestUtils.FunctionDirectory, "testBasicFunctionWithTriggerMetadata.ps1");
            Hashtable triggerMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase)
            {
                { TestUtils.TestInputBindingName, TestStringData }
            };

            var functionInfo = TestUtils.NewAzFunctionInfo(path, string.Empty);
            Hashtable result = _testManager.InvokeFunction(functionInfo, triggerMetadata, _testInputData);

            Assert.Equal(TestStringData, result[TestUtils.TestOutputBindingName]);
        }

        [Fact]
        public void InvokeFunctionWithEntryPointWorks()
        {
            string path = Path.Join(TestUtils.FunctionDirectory, "testFunctionWithEntryPoint.psm1");
            var functionInfo = TestUtils.NewAzFunctionInfo(path, "Run");
            Hashtable result = _testManager.InvokeFunction(functionInfo, null, _testInputData);

            Assert.Equal(TestStringData, result[TestUtils.TestOutputBindingName]);
        }

        [Fact]
        public void FunctionShouldCleanupVariableTable()
        {
            string path = Path.Join(TestUtils.FunctionDirectory, "testFunctionCleanup.ps1");
            var functionInfo = TestUtils.NewAzFunctionInfo(path, string.Empty);

            Hashtable result1 = _testManager.InvokeFunction(functionInfo, null, _testInputData);
            Assert.Equal("is not set", result1[TestUtils.TestOutputBindingName]);

            // the value should not change if the variable table is properly cleaned up.
            Hashtable result2 = _testManager.InvokeFunction(functionInfo, null, _testInputData);
            Assert.Equal("is not set", result2[TestUtils.TestOutputBindingName]);
        }

        [Fact]
        public void ModulePathShouldBeSetCorrectly()
        {
            string workerModulePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            string workerManagedDependenciesPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "ManagedDependencies");
            string funcAppModulePath = Path.Join(FunctionLoader.FunctionAppRootPath, "Modules");
            string expectedPath = $"{funcAppModulePath}{Path.PathSeparator}" +
                                  $"{workerModulePath}{Path.PathSeparator}" +
                                  $"{workerManagedDependenciesPath}";
            Assert.Equal(expectedPath, Environment.GetEnvironmentVariable("PSModulePath"));
        }

        [Fact]
        public void RegisterAndUnregisterFunctionMetadataShouldWork()
        {
            string path = Path.Join(TestUtils.FunctionDirectory, "testBasicFunction.ps1");
            var functionInfo = TestUtils.NewAzFunctionInfo(path, string.Empty);

            Assert.Empty(FunctionMetadata.OutputBindingCache);
            FunctionMetadata.RegisterFunctionMetadata(_testManager.InstanceId, functionInfo);
            Assert.Single(FunctionMetadata.OutputBindingCache);
            FunctionMetadata.UnregisterFunctionMetadata(_testManager.InstanceId);
            Assert.Empty(FunctionMetadata.OutputBindingCache);
        }

        [Fact]
        public void ProfileShouldWork()
        {
            //initialize fresh log
            _testLogger.FullLog.Clear();
            var profilePath = Path.Join(TestUtils.FunctionDirectory, "ProfileBasic", "profile.ps1");
            _testManager.InvokeProfile(profilePath);

            Assert.Single(_testLogger.FullLog);
            Assert.Equal("Information: INFORMATION: Hello PROFILE", _testLogger.FullLog[0]);
        }

        [Fact]
        public void ProfileDoesNotExist()
        {
            //initialize fresh log
            _testLogger.FullLog.Clear();
            _testManager.InvokeProfile(null);

            Assert.Single(_testLogger.FullLog);
            Assert.Matches("Trace: No 'profile.ps1' is found at the FunctionApp root folder: ", _testLogger.FullLog[0]);
        }

        [Fact]
        public void ProfileWithTerminatingError()
        {
            //initialize fresh log
            _testLogger.FullLog.Clear();
            var profilePath = Path.Join(TestUtils.FunctionDirectory, "ProfileWithTerminatingError", "profile.ps1");

            Assert.Throws<CmdletInvocationException>(() => _testManager.InvokeProfile(profilePath));
            Assert.Single(_testLogger.FullLog);
            Assert.Matches("Error: Fail to run profile.ps1. See logs for detailed errors. Profile location: ", _testLogger.FullLog[0]);
        }

        [Fact]
        public void ProfileWithNonTerminatingError()
        {
            //initialize fresh log
            _testLogger.FullLog.Clear();
            var profilePath = Path.Join(TestUtils.FunctionDirectory, "ProfileWithNonTerminatingError", "Profile.ps1");
            _testManager.InvokeProfile(profilePath);

            Assert.Equal(2, _testLogger.FullLog.Count);
            Assert.Equal("Error: ERROR: help me!", _testLogger.FullLog[0]);
            Assert.Matches("Error: Fail to run profile.ps1. See logs for detailed errors. Profile location: ", _testLogger.FullLog[1]);
        }
    }
}
