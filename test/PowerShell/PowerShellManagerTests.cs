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
            var key = "res";
            var data = "Foo";
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);

            manager.InitializeRunspace();

            List<ParameterBinding> inputData = new List<ParameterBinding> {
                new ParameterBinding {
                    Name = "req",
                    Data = new TypedData {
                        String = data
                    }
                }
            };

                string path = System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "PowerShell/TestScripts/testBasicFunction.ps1");
            Hashtable result = manager.InvokeFunction(path, "", null, inputData);

            Assert.Equal(data, result[key]);
        }

        [Fact]
        public void InvokeBasicFunctionWithTriggerMetadataWorks()
        {
            var inKey = "req";
            var outKey = "res";
            var data = "Foo";
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);

            manager.InitializeRunspace();

            List<ParameterBinding> inputData = new List<ParameterBinding> {
                new ParameterBinding {
                    Name = inKey,
                    Data = new TypedData {
                        String = data
                    }
                }
            };

            string path = System.IO.Path.Join(
            AppDomain.CurrentDomain.BaseDirectory,
            "PowerShell/TestScripts/testBasicFunctionWithTriggerMetadata.ps1");
            Hashtable triggerMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase)
            {
                { inKey, data }
            };

            Hashtable result = manager.InvokeFunction(path, "", triggerMetadata, inputData);

            Assert.Equal(data, result[outKey]);
        }

        [Fact]
        public void InvokeFunctionWithEntryPointWorks()
        {
            var key = "res";
            var data = "Foo";
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);

            manager.InitializeRunspace();

            List<ParameterBinding> inputData = new List<ParameterBinding> {
                new ParameterBinding {
                    Name = "req",
                    Data = new TypedData {
                        String = data
                    }
                }
            };

            string path = System.IO.Path.Join(
            AppDomain.CurrentDomain.BaseDirectory,
            "PowerShell/TestScripts/testFunctionWithEntryPoint.ps1");
            Hashtable result = manager.InvokeFunction(path, "Run", null, inputData);

            Assert.Equal(data, result[key]);
        }

        [Fact]
        public void FunctionShouldCleanupVariableTable()
        {
            var key = "res";
            var data = "Foo";
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);

            manager.InitializeRunspace();

            List<ParameterBinding> inputData = new List<ParameterBinding> {
                new ParameterBinding {
                    Name = "req",
                    Data = new TypedData {
                        String = data
                    }
                }
            };

            string path = System.IO.Path.Join(
            AppDomain.CurrentDomain.BaseDirectory,
            "PowerShell/TestScripts/testFunctionCleanup.ps1");

            Hashtable result1 = manager.InvokeFunction(path, "", null, inputData);
            Assert.Equal("is not set", result1[key]);

            // the value shoould not change if the variable table is properly cleaned up.
            Hashtable result2 = manager.InvokeFunction(path, "", null, inputData);
            Assert.Equal("is not set", result2[key]);
        }
    }
}
