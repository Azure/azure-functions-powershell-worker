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


        [Fact]
        public void InitializeRunspaceSuccess()
        {
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);
            manager.InitializeRunspace();

            Assert.Single(logger.FullLog);
            Assert.Equal("Warning: Required environment variables to authenticate to Azure were not present", logger.FullLog[0]);
        }

        [Fact]
        public void InvokeBasicFunctionWorks()
        {
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);

            manager.InitializeRunspace();

            string path = System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "PowerShell/TestScripts/testBasicFunction.ps1");
            Hashtable result = manager.InvokeFunction(path, "", null, TestInputData);

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
            "PowerShell/TestScripts/testBasicFunctionWithTriggerMetadata.ps1");
            Hashtable triggerMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase)
            {
                { TestInputBindingName, TestStringData }
            };

            Hashtable result = manager.InvokeFunction(path, "", triggerMetadata, TestInputData);

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
            "PowerShell/TestScripts/testFunctionWithEntryPoint.ps1");
            Hashtable result = manager.InvokeFunction(path, "Run", null, TestInputData);

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
            "PowerShell/TestScripts/testFunctionCleanup.ps1");

            Hashtable result1 = manager.InvokeFunction(path, "", null, TestInputData);
            Assert.Equal("is not set", result1[TestOutputBindingName]);

            // the value shoould not change if the variable table is properly cleaned up.
            Hashtable result2 = manager.InvokeFunction(path, "", null, TestInputData);
            Assert.Equal("is not set", result2[TestOutputBindingName]);
        }
    }
}
