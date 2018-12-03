//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;

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

<<<<<<< HEAD
            Assert.Empty(logger.FullLog);
=======
            Assert.Single(logger.FullLog);
            Assert.Equal("Warning: Required module to authenticate, Az.Profile, was not present on the PSModulePath", logger.FullLog[0]);
>>>>>>> ca97b495ff7b3c41cc744f3e3023924f7f36fc37
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
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);
            var data = "/some/unknown/directory";

            string modulePathBefore = Environment.GetEnvironmentVariable("PSModulePath");
            manager.PrependToPSModulePath(data);
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
    }
}
