//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    public class PowerShellManagerTests
    {
        public const string TestInputBindingName = "req";
        public const string TestOutputBindingName = "res";
        public const string TestStringData = "Foo";

        internal static ConsoleLogger defaultTestLogger = new ConsoleLogger();
        internal static PowerShellManager defaultTestManager = new PowerShellManager(defaultTestLogger);

        public readonly List<ParameterBinding> TestInputData = new List<ParameterBinding> {
            new ParameterBinding {
                Name = TestInputBindingName,
                Data = new TypedData {
                    String = TestStringData
                }
            }
        };
        public readonly RpcFunctionMetadata rpcFunctionMetadata = new RpcFunctionMetadata();

        private AzFunctionInfo GetAzFunctionInfo(string scriptFile, string entryPoint)
        {
            rpcFunctionMetadata.ScriptFile = scriptFile;
            rpcFunctionMetadata.EntryPoint = entryPoint;
            return new AzFunctionInfo(rpcFunctionMetadata);
        }

        [Fact]
        public void InitializeRunspaceSuccess()
        {
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);
            manager.InitializeRunspace();

            Assert.Empty(logger.FullLog);
        }

        [Fact]
        public void InvokeBasicFunctionWorks()
        {
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);

            manager.InitializeRunspace();

            string path = System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "Unit/PowerShell/TestScripts/testBasicFunction.ps1");

            var functionInfo = GetAzFunctionInfo(path, string.Empty);
            Hashtable result = manager.InvokeFunction(functionInfo, null, TestInputData);

            Assert.Equal(TestStringData, result[TestOutputBindingName]);
        }

        [Fact]
        public void InvokeBasicFunctionWithTriggerMetadataWorks()
        {
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);

            manager.InitializeRunspace();

            string path = System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "Unit/PowerShell/TestScripts/testBasicFunctionWithTriggerMetadata.ps1");

            Hashtable triggerMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase)
            {
                { TestInputBindingName, TestStringData }
            };

            var functionInfo = GetAzFunctionInfo(path, string.Empty);
            Hashtable result = manager.InvokeFunction(functionInfo, triggerMetadata, TestInputData);

            Assert.Equal(TestStringData, result[TestOutputBindingName]);
        }

        [Fact]
        public void InvokeFunctionWithEntryPointWorks()
        {
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);

            manager.InitializeRunspace();

            string path = System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "Unit/PowerShell/TestScripts/testFunctionWithEntryPoint.ps1");

            var functionInfo = GetAzFunctionInfo(path, "Run");
            Hashtable result = manager.InvokeFunction(functionInfo, null, TestInputData);

            Assert.Equal(TestStringData, result[TestOutputBindingName]);
        }

        [Fact]
        public void FunctionShouldCleanupVariableTable()
        {
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);

            manager.InitializeRunspace();

            string path = System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "Unit/PowerShell/TestScripts/testFunctionCleanup.ps1");

            var functionInfo = GetAzFunctionInfo(path, string.Empty);
            Hashtable result1 = manager.InvokeFunction(functionInfo, null, TestInputData);
            Assert.Equal("is not set", result1[TestOutputBindingName]);

            // the value shoould not change if the variable table is properly cleaned up.
            Hashtable result2 = manager.InvokeFunction(functionInfo, null, TestInputData);
            Assert.Equal("is not set", result2[TestOutputBindingName]);
        }

        [Fact]
        public void PrependingToPSModulePathShouldWork()
        {
            var data = "/some/unknown/directory";

            string modulePathBefore = Environment.GetEnvironmentVariable("PSModulePath");
            defaultTestManager.PrependToPSModulePath(data);
            try
            {
                // the data path should be ahead of anything else
                Assert.Equal($"{data}{System.IO.Path.PathSeparator}{modulePathBefore}", Environment.GetEnvironmentVariable("PSModulePath"));
            }
            finally
            {
                // Set the PSModulePath back to what it was before
                Environment.SetEnvironmentVariable("PSModulePath", modulePathBefore);
            }
        }

        [Fact]
        public void RegisterAndUnregisterFunctionMetadataShouldWork()
        {
            var functionInfo = GetAzFunctionInfo("dummy-path", string.Empty);

            Assert.Empty(FunctionMetadata.OutputBindingCache);
            defaultTestManager.RegisterFunctionMetadata(functionInfo);
            Assert.Single(FunctionMetadata.OutputBindingCache);
            defaultTestManager.UnregisterFunctionMetadata();
            Assert.Empty(FunctionMetadata.OutputBindingCache);
        }

        [Fact]
        public void ProfileShouldWork()
        {
            //initialize fresh log
            defaultTestLogger.FullLog.Clear();

            CleanupFunctionLoaderStaticPaths();
            FunctionLoader.SetupWellKnownPaths(System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "Unit/PowerShell/TestScripts/ProfileBasic"));
            
            defaultTestManager.InvokeProfile();

            Assert.Single(defaultTestLogger.FullLog);
            Assert.Equal("Information: INFORMATION: Hello PROFILE", defaultTestLogger.FullLog[0]);
        }

        [Fact]
        public void ProfileDoesNotExist()
        {
            //initialize fresh log
            defaultTestLogger.FullLog.Clear();

            CleanupFunctionLoaderStaticPaths();
            FunctionLoader.SetupWellKnownPaths(AppDomain.CurrentDomain.BaseDirectory);
            
            defaultTestManager.InvokeProfile();

            Assert.Single(defaultTestLogger.FullLog);
            Assert.Matches("Trace: No 'profile.ps1' is found at the FunctionApp root folder: ", defaultTestLogger.FullLog[0]);
        }

        [Fact]
        public void ProfileWithTerminatingError()
        {
            //initialize fresh log
            defaultTestLogger.FullLog.Clear();

            CleanupFunctionLoaderStaticPaths();
            FunctionLoader.SetupWellKnownPaths(System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "Unit/PowerShell/TestScripts/ProfileWithTerminatingError"));
            
            Assert.Throws<CmdletInvocationException>(() => defaultTestManager.InvokeProfile());
            Assert.Single(defaultTestLogger.FullLog);
            Assert.Matches("Error: Fail to run profile.ps1. See logs for detailed errors. Profile location: ", defaultTestLogger.FullLog[0]);
        }

        [Fact]
        public void ProfileWithNonTerminatingError()
        {
            //initialize fresh log
            defaultTestLogger.FullLog.Clear();

            CleanupFunctionLoaderStaticPaths();
            FunctionLoader.SetupWellKnownPaths(System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "Unit/PowerShell/TestScripts/ProfileWithNonTerminatingError"));
            
            defaultTestManager.InvokeProfile();

            Assert.Equal(2, defaultTestLogger.FullLog.Count);
            Assert.Equal("Error: ERROR: help me!", defaultTestLogger.FullLog[0]);
            Assert.Matches("Error: Fail to run profile.ps1. See logs for detailed errors. Profile location: ", defaultTestLogger.FullLog[1]);
        }

        // Helper function that sets all the well-known paths in the Function Loader back to null.
        private void CleanupFunctionLoaderStaticPaths()
        {
            FunctionLoader.FunctionAppRootPath = null;
            FunctionLoader.FunctionAppProfilePath = null;
            FunctionLoader.FunctionAppModulesPath = null;
        }
    }
}
