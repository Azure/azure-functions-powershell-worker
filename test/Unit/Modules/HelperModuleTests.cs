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

    public class HelperModuleTests : IDisposable
    {
        private const string Response = "response";
        private const string Queue = "queue";
        private const string Foo = "Foo";
        private const string Bar = "Bar";
        private const string Food = "Food";

        private readonly static AzFunctionInfo s_funcInfo;
        private readonly static PowerShell s_pwsh;

        static HelperModuleTests()
        {
            var funcDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "TestScripts", "PowerShell");
            var rpcFuncMetadata = new RpcFunctionMetadata()
            {
                Name = "TestFuncApp",
                Directory = funcDirectory,
                ScriptFile = Path.Join(funcDirectory, "testBasicFunction.ps1"),
                EntryPoint = string.Empty,
                Bindings =
                {
                    { "req" ,     new BindingInfo { Direction = BindingInfo.Types.Direction.In, Type = "httpTrigger" } },
                    { Response,   new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "http" } },
                    { Queue,      new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "queue" } },
                    { Foo,        new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "new" } },
                    { Bar,        new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "new" } },
                    { Food,       new BindingInfo { Direction = BindingInfo.Types.Direction.Out, Type = "new" } }
                }
            };

            var funcLoadReq = new FunctionLoadRequest { FunctionId = "FunctionId", Metadata = rpcFuncMetadata };
            FunctionLoader.SetupWellKnownPaths(funcLoadReq, managedDependenciesPath: null);
            var initialSessionStateProvider = new InitialSessionStateProvider();
            s_pwsh = Utils.NewPwshInstance(initialSessionStateProvider.GetInstance);
            s_funcInfo = new AzFunctionInfo(rpcFuncMetadata);
        }

        public HelperModuleTests()
        {
            FunctionMetadata.RegisterFunctionMetadata(s_pwsh.Runspace.InstanceId, s_funcInfo.OutputBindings);
        }

        public void Dispose()
        {
            FunctionMetadata.UnregisterFunctionMetadata(s_pwsh.Runspace.InstanceId);
            s_pwsh.AddScript("Get-OutputBinding -Purge").InvokeAndClearCommands();
        }

        [Fact]
        public void BasicPushGetValueTests()
        {
            // The first item added to 'queue' is the value itself
            s_pwsh.AddScript("Push-OutputBinding -Name queue -Value 5").InvokeAndClearCommands();
            var results = s_pwsh.AddScript("Get-OutputBinding").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.Equal(5, results[0][Queue]);

            // The second item added to 'queue' will make it a list
            s_pwsh.AddScript("Push-OutputBinding -Name queue -Value 6").InvokeAndClearCommands();
            results = s_pwsh.AddScript("Get-OutputBinding").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.IsType<List<object>>(results[0][Queue]);

            var list = (List<object>)results[0][Queue];
            Assert.Equal(2, list.Count);
            Assert.Equal(5, list[0]);
            Assert.Equal(6, list[1]);

            // The array added to 'queue' will get unraveled
            var array = new object[] { 7, 8 };
            s_pwsh.AddScript("Push-OutputBinding -Name queue -Value @(7, 8)").InvokeAndClearCommands();
            results = s_pwsh.AddScript("Get-OutputBinding -Purge").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.IsType<List<object>>(results[0][Queue]);

            list = (List<object>)results[0][Queue];
            Assert.Equal(4, list.Count);
            Assert.Equal(5, list[0]);
            Assert.Equal(6, list[1]);
            Assert.Equal(7, list[2]);
            Assert.Equal(8, list[3]);

            // The array gets unraveled and added to a list
            s_pwsh.AddScript("Push-OutputBinding -Name queue -Value @(1, 2)").InvokeAndClearCommands();
            results = s_pwsh.AddScript("Get-OutputBinding -Purge").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.IsType<List<object>>(results[0][Queue]);

            list = (List<object>)results[0][Queue];
            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(2, list[1]);
        }

        [Fact]
        public void BindingNameWithDifferentCaseShouldWork()
        {
            s_pwsh.AddScript("Push-OutputBinding -Name RESPONSE -Value 'UpperCase'").InvokeAndClearCommands();
            s_pwsh.AddScript("Push-OutputBinding -Name QUeue -Value 'MixedCase'").InvokeAndClearCommands();
            var results = s_pwsh.AddScript("Get-OutputBinding -Purge").InvokeAndClearCommands<Hashtable>();

            Assert.Single(results);
            Assert.Equal(2, results[0].Count);
            Assert.Equal("UpperCase", results[0][Response]);
            Assert.Equal("MixedCase", results[0][Queue]);
        }

        [Fact]
        public void PushOutBindingShouldWorkWithPipelineInput()
        {
            s_pwsh.AddScript("'Baz' | Push-OutputBinding -Name response").InvokeAndClearCommands();
            s_pwsh.AddScript("'item1', 'item2', 'item3' | Push-OutputBinding -Name queue").InvokeAndClearCommands();
            var results = s_pwsh.AddScript("Get-OutputBinding -Purge").InvokeAndClearCommands<Hashtable>();

            Assert.Single(results);
            Assert.Equal(2, results[0].Count);
            Assert.Equal("Baz", results[0][Response].ToString());

            Assert.IsType<List<object>>(results[0][Queue]);
            var list = (List<object>)results[0][Queue];
            Assert.Equal(3, list.Count);
            Assert.Equal("item1", list[0].ToString());
            Assert.Equal("item2", list[1].ToString());
            Assert.Equal("item3", list[2].ToString());
        }

        [Fact]
        public void PushToHttpResponseTwiceShouldFail()
        {
            s_pwsh.AddScript("Push-OutputBinding -Name response -Value res").InvokeAndClearCommands();
            var results = s_pwsh.AddScript("Get-OutputBinding").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.Equal("res", results[0][Response]);

            s_pwsh.AddScript("Push-OutputBinding -Name response -Value baz").Invoke();
            Assert.Single(s_pwsh.Streams.Error);

            var error = s_pwsh.Streams.Error[0];
            Assert.IsType<InvalidOperationException>(error.Exception);
            Assert.Contains("http", error.Exception.Message);
            Assert.Contains("-Clobber", error.Exception.Message);
        }

        [Fact]
        public void PushWithNonExistingBindingNameShouldThrow()
        {
            s_pwsh.AddScript("Push-OutputBinding nonExist baz").Invoke();
            Assert.Single(s_pwsh.Streams.Error);

            var error = s_pwsh.Streams.Error[0];
            Assert.IsType<InvalidOperationException>(error.Exception);
            Assert.Contains("nonExist", error.Exception.Message);
        }

        [Fact]
        public void OverwritingShouldWork()
        {
            s_pwsh.AddScript("Push-OutputBinding response 5").InvokeAndClearCommands();
            var results = s_pwsh.AddScript("Get-OutputBinding").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.Equal(5, results[0][Response]);

            // Overwrite the old value for the for 'response' output binding with -Clobber.
            s_pwsh.AddScript("Push-OutputBinding response 6 -Clobber").InvokeAndClearCommands();
            results = s_pwsh.AddScript("Get-OutputBinding -Purge").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.Equal(6, results[0][Response]);

            s_pwsh.AddScript("Push-OutputBinding queue 1").InvokeAndClearCommands();
            results = s_pwsh.AddScript("Get-OutputBinding").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.Equal(1, results[0][Queue]);

            // Even queue output binding accept multiple values, when -Clobber is specified, the old value is overwritten.
            s_pwsh.AddScript("Push-OutputBinding queue 2 -Clobber").InvokeAndClearCommands();
            results = s_pwsh.AddScript("Get-OutputBinding").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.Equal(2, results[0][Queue]);

            // Overwrite with an array will make the value a list that contains the items from the array.
            s_pwsh.AddScript("Push-OutputBinding queue @(3, 4) -Clobber").InvokeAndClearCommands();
            results = s_pwsh.AddScript("Get-OutputBinding -Purge").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.IsType<List<object>>(results[0][Queue]);
            var list = (List<object>)results[0][Queue];
            Assert.Equal(2, list.Count);
            Assert.Equal(3, list[0]);
            Assert.Equal(4, list[1]);
        }

        [Fact]
        public void GetOutputBindingShouldWork()
        {
            s_pwsh.AddScript("Push-OutputBinding -Name Foo 1").InvokeAndClearCommands();
            s_pwsh.AddScript("Push-OutputBinding Bar -Value Baz").InvokeAndClearCommands();
            s_pwsh.AddScript("Push-OutputBinding -Name Food -Value apple").InvokeAndClearCommands();

            // No name specified
            var results = s_pwsh.AddScript("Get-OutputBinding").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Equal(3, results[0].Count);

            Assert.Equal(1, results[0][Foo]);
            Assert.Equal("Baz", results[0][Bar]);
            Assert.Equal("apple", results[0][Food]);

            // Specify the name
            results = s_pwsh.AddScript("Get-OutputBinding -Name Foo").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Single(results[0]);
            Assert.Equal(1, results[0][Foo]);

            // Explicit name specified that does not exist
            results = s_pwsh.AddScript("Get-OutputBinding -Name DoesNotExist").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Empty(results[0]);

            // Wildcard name specified
            results = s_pwsh.AddScript("Get-OutputBinding -Name F*").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Equal(2, results[0].Count);

            Assert.Equal(1, results[0][Foo]);
            Assert.Equal("apple", results[0][Food]);

            // User -Purge should clear the output binding values
            results = s_pwsh.AddScript("Get-OutputBinding -Purge").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Equal(3, results[0].Count);

            Assert.Equal(1, results[0][Foo]);
            Assert.Equal("Baz", results[0][Bar]);
            Assert.Equal("apple", results[0][Food]);

            // Values should have been cleared.
            results = s_pwsh.AddScript("Get-OutputBinding").InvokeAndClearCommands<Hashtable>();
            Assert.Single(results);
            Assert.Empty(results[0]);
        }

        [Fact]
        public void TracePipelineObjectShouldWork()
        {
            string script = @"
    $cmd = Get-Command -Name Get-Command
    function Write-TestObject {
        foreach ($i in 1..20) {
            Write-Output $cmd
        }
        Write-Information '__LAST_INFO_MSG__'
    }";

            s_pwsh.AddScript(script).InvokeAndClearCommands();

            var outStringResults = s_pwsh.AddScript("Write-TestObject | Out-String -Stream").InvokeAndClearCommands<string>();
            var results = s_pwsh.AddScript("Write-TestObject | Trace-PipelineObject").Invoke<CmdletInfo>();
            Assert.Equal(20, results.Count);
            foreach (var item in results)
            {
                Assert.Equal("Get-Command", item.Name);
            }

            Assert.Equal(outStringResults.Count + 1, s_pwsh.Streams.Information.Count);

            int lastNonWhitespaceItem = outStringResults.Count - 1;
            while (string.IsNullOrWhiteSpace(outStringResults[lastNonWhitespaceItem])) {
                lastNonWhitespaceItem --;
            }

            for (int i = 0; i <= lastNonWhitespaceItem; i++) {
                Assert.Equal(outStringResults[i], s_pwsh.Streams.Information[i].MessageData.ToString());
                Assert.Single(s_pwsh.Streams.Information[i].Tags);
                Assert.Equal("__PipelineObject__", s_pwsh.Streams.Information[i].Tags[0]);
            }

            Assert.Equal("__LAST_INFO_MSG__", s_pwsh.Streams.Information[lastNonWhitespaceItem + 1].MessageData.ToString());
            Assert.Empty(s_pwsh.Streams.Information[lastNonWhitespaceItem + 1].Tags);
        }
    }
}
