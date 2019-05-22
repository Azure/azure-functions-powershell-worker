//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    using System.Management.Automation;

    internal class TestUtils
    {
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

    public class PowerShellManagerTests : IDisposable
    {
        private const string TestInputBindingName = "req";
        private const string TestOutputBindingName = "res";
        private const string TestStringData = "Foo";

        private readonly static string s_funcDirectory;
        private readonly static FunctionLoadRequest s_functionLoadRequest;

        private readonly static ConsoleLogger s_testLogger;
        private readonly static List<ParameterBinding> s_testInputData;

        static PowerShellManagerTests()
        {
            s_funcDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "TestScripts", "PowerShell");
            s_testLogger = new ConsoleLogger();
            s_testInputData = new List<ParameterBinding>
            {
                new ParameterBinding
                {
                    Name = TestInputBindingName,
                    Data = new TypedData { String = TestStringData }
                }
            };

            var rpcFunctionMetadata = new RpcFunctionMetadata()
            {
                Name = "TestFuncApp",
                Directory = s_funcDirectory,
                Bindings =
                {
                    { TestInputBindingName , new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "httpTrigger" } },
                    { TestOutputBindingName, new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "http" } }
                }
            };

            s_functionLoadRequest = new FunctionLoadRequest { FunctionId = "FunctionId", Metadata = rpcFunctionMetadata };
            FunctionLoader.SetupWellKnownPaths(s_functionLoadRequest);
        }

        // Have a single place to get a PowerShellManager for testing.
        // This is to guarantee that the well known paths are setup before calling the constructor of PowerShellManager.
        internal static PowerShellManager NewTestPowerShellManager(ConsoleLogger logger, PowerShell pwsh = null)
        {
            return pwsh != null ? new PowerShellManager(logger, pwsh) : new PowerShellManager(logger, id: 2);
        }

        private static (AzFunctionInfo, PowerShellManager) PrepareFunction(string scriptFile, string entryPoint)
        {
            s_functionLoadRequest.Metadata.ScriptFile = scriptFile;
            s_functionLoadRequest.Metadata.EntryPoint = entryPoint;
            s_functionLoadRequest.Metadata.Directory = Path.GetDirectoryName(scriptFile);

            FunctionLoader.LoadFunction(s_functionLoadRequest);
            var funcInfo = FunctionLoader.GetFunctionInfo(s_functionLoadRequest.FunctionId);
            var psManager = NewTestPowerShellManager(s_testLogger);

            return (funcInfo, psManager);
        }

        public void Dispose()
        {
            FunctionLoader.ClearLoadedFunctions();
            s_testLogger.FullLog.Clear();
        }

        [Fact]
        public void InvokeBasicFunctionWorks()
        {
            string path = Path.Join(s_funcDirectory, "testBasicFunction.ps1");
            var (functionInfo, testManager) = PrepareFunction(path, string.Empty);

            try
            {
                FunctionMetadata.RegisterFunctionMetadata(testManager.InstanceId, functionInfo);
                Hashtable result = testManager.InvokeFunction(functionInfo, null, s_testInputData);

                // The outputBinding hashtable for the runspace should be cleared after 'InvokeFunction'
                Hashtable outputBindings = FunctionMetadata.GetOutputBindingHashtable(testManager.InstanceId);
                Assert.Empty(outputBindings);

                // A PowerShell function should be created fro the Az function.
                string expectedResult = $"{TestStringData},{functionInfo.DeployedPSFuncName}";
                Assert.Equal(expectedResult, result[TestOutputBindingName]);
            }
            finally
            {
                FunctionMetadata.UnregisterFunctionMetadata(testManager.InstanceId);
            }
        }

        [Fact]
        public void InvokeFunctionWithSpecialVariableWorks()
        {
            string path = Path.Join(s_funcDirectory, "testBasicFunctionSpecialVariables.ps1");
            var (functionInfo, testManager) = PrepareFunction(path, string.Empty);

            try
            {
                FunctionMetadata.RegisterFunctionMetadata(testManager.InstanceId, functionInfo);
                Hashtable result = testManager.InvokeFunction(functionInfo, null, s_testInputData);

                // The outputBinding hashtable for the runspace should be cleared after 'InvokeFunction'
                Hashtable outputBindings = FunctionMetadata.GetOutputBindingHashtable(testManager.InstanceId);
                Assert.Empty(outputBindings);

                // A PowerShell function should be created fro the Az function.
                string expectedResult = $"{s_funcDirectory},{path},{functionInfo.DeployedPSFuncName}";
                Assert.Equal(expectedResult, result[TestOutputBindingName]);
            }
            finally
            {
                FunctionMetadata.UnregisterFunctionMetadata(testManager.InstanceId);
            }
        }

        [Fact]
        public void InvokeBasicFunctionWithRequiresWorks()
        {
            string path = Path.Join(s_funcDirectory, "testBasicFunctionWithRequires.ps1");
            var (functionInfo, testManager) = PrepareFunction(path, string.Empty);

            try
            {
                FunctionMetadata.RegisterFunctionMetadata(testManager.InstanceId, functionInfo);
                Hashtable result = testManager.InvokeFunction(functionInfo, null, s_testInputData);

                // The outputBinding hashtable for the runspace should be cleared after 'InvokeFunction'
                Hashtable outputBindings = FunctionMetadata.GetOutputBindingHashtable(testManager.InstanceId);
                Assert.Empty(outputBindings);

                // When function script has #requires, not PowerShell function will be created for the Az function,
                // and the invocation uses the file path directly.
                string expectedResult = $"{TestStringData},ThreadJob,testBasicFunctionWithRequires.ps1";
                Assert.Equal(expectedResult, result[TestOutputBindingName]);
            }
            finally
            {
                FunctionMetadata.UnregisterFunctionMetadata(testManager.InstanceId);
            }
        }

        [Fact]
        public void InvokeBasicFunctionWithTriggerMetadataWorks()
        {
            string path = Path.Join(s_funcDirectory, "testBasicFunctionWithTriggerMetadata.ps1");
            var (functionInfo, testManager) = PrepareFunction(path, string.Empty);

            Hashtable triggerMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase)
            {
                { TestInputBindingName, TestStringData }
            };

            try
            {
                FunctionMetadata.RegisterFunctionMetadata(testManager.InstanceId, functionInfo);
                Hashtable result = testManager.InvokeFunction(functionInfo, triggerMetadata, s_testInputData);

                // The outputBinding hashtable for the runspace should be cleared after 'InvokeFunction'
                Hashtable outputBindings = FunctionMetadata.GetOutputBindingHashtable(testManager.InstanceId);
                Assert.Empty(outputBindings);

                // A PowerShell function should be created fro the Az function.
                string expectedResult = $"{TestStringData},{functionInfo.DeployedPSFuncName}";
                Assert.Equal(expectedResult, result[TestOutputBindingName]);
            }
            finally
            {
                FunctionMetadata.UnregisterFunctionMetadata(testManager.InstanceId);
            }
        }

        [Fact]
        public void InvokeFunctionWithEntryPointWorks()
        {
            string path = Path.Join(s_funcDirectory, "testFunctionWithEntryPoint.psm1");
            var (functionInfo, testManager) = PrepareFunction(path, "Run");

            try
            {
                FunctionMetadata.RegisterFunctionMetadata(testManager.InstanceId, functionInfo);
                Hashtable result = testManager.InvokeFunction(functionInfo, null, s_testInputData);

                // The outputBinding hashtable for the runspace should be cleared after 'InvokeFunction'
                Hashtable outputBindings = FunctionMetadata.GetOutputBindingHashtable(testManager.InstanceId);
                Assert.Empty(outputBindings);

                string expectedResult = $"{TestStringData},Run";
                Assert.Equal(expectedResult, result[TestOutputBindingName]);
            }
            finally
            {
                FunctionMetadata.UnregisterFunctionMetadata(testManager.InstanceId);
            }
        }

        [Fact]
        public void FunctionShouldCleanupVariableTable()
        {
            string path = Path.Join(s_funcDirectory, "testFunctionCleanup.ps1");
            var (functionInfo, testManager) = PrepareFunction(path, string.Empty);

            try
            {
                FunctionMetadata.RegisterFunctionMetadata(testManager.InstanceId, functionInfo);

                Hashtable result1 = testManager.InvokeFunction(functionInfo, null, s_testInputData);
                Assert.Equal("is not set", result1[TestOutputBindingName]);

                // the value should not change if the variable table is properly cleaned up.
                Hashtable result2 = testManager.InvokeFunction(functionInfo, null, s_testInputData);
                Assert.Equal("is not set", result2[TestOutputBindingName]);
            }
            finally
            {
                FunctionMetadata.UnregisterFunctionMetadata(testManager.InstanceId);
            }
        }

        [Fact]
        public void ModulePathShouldBeSetCorrectly()
        {
            string workerModulePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            string funcAppModulePath = Path.Join(FunctionLoader.FunctionAppRootPath, "Modules");
            string expectedPath = $"{funcAppModulePath}{Path.PathSeparator}{workerModulePath}";
            Assert.Equal(expectedPath, Environment.GetEnvironmentVariable("PSModulePath"));
        }

        [Fact]
        public void RegisterAndUnregisterFunctionMetadataShouldWork()
        {
            string path = Path.Join(s_funcDirectory, "testBasicFunction.ps1");
            var (functionInfo, testManager) = PrepareFunction(path, string.Empty);

            FunctionMetadata.RegisterFunctionMetadata(testManager.InstanceId, functionInfo);
            var outBindingMap = FunctionMetadata.GetOutputBindingInfo(testManager.InstanceId);
            Assert.Single(outBindingMap);
            Assert.Equal(TestOutputBindingName, outBindingMap.First().Key);

            FunctionMetadata.UnregisterFunctionMetadata(testManager.InstanceId);
            outBindingMap = FunctionMetadata.GetOutputBindingInfo(testManager.InstanceId);
            Assert.Null(outBindingMap);
        }

        [Fact]
        public void ProfileShouldWork()
        {
            var profilePath = Path.Join(s_funcDirectory, "ProfileBasic", "profile.ps1");
            var testManager = NewTestPowerShellManager(s_testLogger);

            // Clear log stream
            s_testLogger.FullLog.Clear();
            testManager.InvokeProfile(profilePath);

            Assert.Single(s_testLogger.FullLog);
            Assert.Equal("Information: INFORMATION: Hello PROFILE", s_testLogger.FullLog[0]);
        }

        [Fact]
        public void ProfileWithTerminatingError()
        {
            var profilePath = Path.Join(s_funcDirectory, "ProfileWithTerminatingError", "profile.ps1");
            var testManager = NewTestPowerShellManager(s_testLogger);

            // Clear log stream
            s_testLogger.FullLog.Clear();

            Assert.Throws<CmdletInvocationException>(() => testManager.InvokeProfile(profilePath));
            Assert.Single(s_testLogger.FullLog);
            Assert.Matches("Error: Fail to run profile.ps1. See logs for detailed errors. Profile location: ", s_testLogger.FullLog[0]);
        }

        [Fact]
        public void ProfileWithNonTerminatingError()
        {
            var profilePath = Path.Join(s_funcDirectory, "ProfileWithNonTerminatingError", "Profile.ps1");
            var testManager = NewTestPowerShellManager(s_testLogger);

            // Clear log stream
            s_testLogger.FullLog.Clear();
            testManager.InvokeProfile(profilePath);

            Assert.Equal(2, s_testLogger.FullLog.Count);
            Assert.Equal("Error: ERROR: help me!", s_testLogger.FullLog[0]);
            Assert.Matches("Error: Fail to run profile.ps1. See logs for detailed errors. Profile location: ", s_testLogger.FullLog[1]);
        }

        [Fact]
        public void PSManagerCtorRunsProfileByDefault()
        {
            // Clear log stream
            s_testLogger.FullLog.Clear();
            NewTestPowerShellManager(s_testLogger);

            Assert.Single(s_testLogger.FullLog);
            Assert.Equal($"Trace: No 'profile.ps1' is found at the FunctionApp root folder: {FunctionLoader.FunctionAppRootPath}.", s_testLogger.FullLog[0]);
        }

        [Fact]
        public void PSManagerCtorDoesNotRunProfileIfDelayInit()
        {
            // Clear log stream
            s_testLogger.FullLog.Clear();
            NewTestPowerShellManager(s_testLogger, Utils.NewPwshInstance());

            Assert.Empty(s_testLogger.FullLog);
        }
    }
}
