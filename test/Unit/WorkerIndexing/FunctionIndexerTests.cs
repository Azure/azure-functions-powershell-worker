//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
using Xunit;

using Microsoft.Azure.Functions.PowerShellWorker.WorkerIndexing;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.WorkerIndexing
{
    public class FunctionIndexerTests
    {
        private static readonly string TestScriptsDir = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "WorkerIndexing", "TestScripts"));

        #region HttpTrigger

        [Fact]
        public void IndexesHttpTriggerFunction()
        {
            var results = FunctionIndexer.IndexFunctionsInFile(
                Path.Combine(TestScriptsDir, "HttpTriggerFunction.psm1"), TestScriptsDir).ToList();

            Assert.Single(results);
            var func = results[0];
            Assert.Equal("HttpExample", func.Name);
            Assert.Equal("HttpExample", func.EntryPoint);
            Assert.Equal("powershell", func.Language);
            Assert.True(func.Bindings.ContainsKey("Request"));

            var triggerBinding = func.Bindings["Request"];
            Assert.Equal("httpTrigger", triggerBinding.Type);
            Assert.Equal(Microsoft.Azure.WebJobs.Script.Grpc.Messages.BindingInfo.Types.Direction.In, triggerBinding.Direction);

            // Should have implicit Response output binding for HTTP
            Assert.True(func.Bindings.ContainsKey("Response"));
            Assert.Equal("http", func.Bindings["Response"].Type);
            Assert.Equal(Microsoft.Azure.WebJobs.Script.Grpc.Messages.BindingInfo.Types.Direction.Out, func.Bindings["Response"].Direction);

            // Verify raw bindings contain authLevel and methods
            Assert.True(func.RawBindings.Any(r => r.Contains("authLevel", StringComparison.OrdinalIgnoreCase)));
            Assert.True(func.RawBindings.Any(r => r.Contains("hello", StringComparison.OrdinalIgnoreCase)));
            Assert.True(func.RawBindings.Any(r => r.Contains("methods", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public void HttpTriggerRawBindingHasCorrectJsonTypes()
        {
            // Validates that authLevel is a JSON string and methods is always a JSON array,
            // which is what the Functions host expects when parsing the raw binding.
            var results = FunctionIndexer.IndexFunctionsInFile(
                Path.Combine(TestScriptsDir, "HttpTriggerFunction.psm1"), TestScriptsDir).ToList();

            Assert.Single(results);
            var func = results[0];

            // Find the httpTrigger raw binding (the one with "httpTrigger")
            var triggerRaw = func.RawBindings.First(r => r.Contains("httpTrigger"));

            // authLevel must be a JSON string value: "authLevel":"anonymous" (not an array)
            Assert.Contains("\"authLevel\":\"", triggerRaw);
            Assert.DoesNotContain("\"authLevel\":[", triggerRaw);

            // methods must be a JSON array: "methods":["GET","POST"] (even for single values)
            Assert.Contains("\"methods\":[", triggerRaw);
            Assert.DoesNotContain("\"methods\":\"", triggerRaw);

            // route must be a JSON string value
            Assert.Contains("\"route\":\"", triggerRaw);
            Assert.DoesNotContain("\"route\":[", triggerRaw);
        }

        #endregion

        #region TimerTrigger

        [Fact]
        public void IndexesTimerTriggerWithNameOverride()
        {
            var results = FunctionIndexer.IndexFunctionsInFile(
                Path.Combine(TestScriptsDir, "TimerTriggerFunction.psm1"), TestScriptsDir).ToList();

            Assert.Single(results);
            var func = results[0];
            Assert.Equal("MyTimerFunc", func.Name);
            Assert.Equal("TimerJob", func.EntryPoint);
            Assert.True(func.Bindings.ContainsKey("Timer"));
            Assert.Equal("timerTrigger", func.Bindings["Timer"].Type);

            Assert.True(func.RawBindings.Any(r => r.Contains("schedule", StringComparison.OrdinalIgnoreCase) && r.Contains("0 */5 * * * *")));
        }

        #endregion

        #region QueueTrigger + Output

        [Fact]
        public void IndexesQueueTriggerWithOutputBinding()
        {
            var results = FunctionIndexer.IndexFunctionsInFile(
                Path.Combine(TestScriptsDir, "QueueTriggerWithOutput.psm1"), TestScriptsDir).ToList();

            Assert.Single(results);
            var func = results[0];
            Assert.Equal("ProcessQueue", func.Name);

            Assert.True(func.Bindings.ContainsKey("QueueItem"));
            Assert.Equal("queueTrigger", func.Bindings["QueueItem"].Type);

            Assert.True(func.Bindings.ContainsKey("OutputQueue"));
            Assert.Equal("queue", func.Bindings["OutputQueue"].Type);
            Assert.Equal(Microsoft.Azure.WebJobs.Script.Grpc.Messages.BindingInfo.Types.Direction.Out,
                func.Bindings["OutputQueue"].Direction);

            // Verify connection string is in raw bindings
            Assert.True(func.RawBindings.Any(r => r.Contains("StorageConn", StringComparison.OrdinalIgnoreCase)));
        }

        #endregion

        #region Multiple Functions In One File

        [Fact]
        public void IndexesMultipleFunctionsFromOneFile()
        {
            var results = FunctionIndexer.IndexFunctionsInFile(
                Path.Combine(TestScriptsDir, "MultipleFunctions.psm1"), TestScriptsDir).ToList();

            Assert.Equal(2, results.Count);
            Assert.Contains(results, f => f.Name == "MultipleFunctions_First");
            Assert.Contains(results, f => f.Name == "MultipleFunctions_Second");
        }

        #endregion

        #region No AzFunction Attribute

        [Fact]
        public void SkipsFunctionsWithoutAzFunctionAttribute()
        {
            var results = FunctionIndexer.IndexFunctionsInFile(
                Path.Combine(TestScriptsDir, "NoAzFunctionAttribute.ps1"), TestScriptsDir).ToList();

            Assert.Empty(results);
        }

        #endregion

        #region Generic Trigger

        [Fact]
        public void IndexesGenericTriggerWithProperties()
        {
            var results = FunctionIndexer.IndexFunctionsInFile(
                Path.Combine(TestScriptsDir, "GenericTriggerFunction.psm1"), TestScriptsDir).ToList();

            Assert.Single(results);
            var func = results[0];
            Assert.Equal("GenericExample", func.Name);
            Assert.Equal("kafkaTrigger", func.Bindings["Message"].Type);

            // Generic Properties should be flattened into raw binding
            Assert.True(func.RawBindings.Any(r => r.Contains("brokerList", StringComparison.OrdinalIgnoreCase)));
            Assert.True(func.RawBindings.Any(r => r.Contains("myTopic", StringComparison.OrdinalIgnoreCase)));
            Assert.True(func.RawBindings.Any(r => r.Contains("KafkaConn", StringComparison.OrdinalIgnoreCase)));
        }

        #endregion

        #region Durable Functions

        [Fact]
        public void IndexesDurableFunctions()
        {
            var results = FunctionIndexer.IndexFunctionsInFile(
                Path.Combine(TestScriptsDir, "DurableFunctions.psm1"), TestScriptsDir).ToList();

            Assert.Equal(3, results.Count);

            var orch = results.First(f => f.Name == "DurableOrchExample");
            Assert.Equal("orchestrationTrigger", orch.Bindings["Context"].Type);

            var activity = results.First(f => f.Name == "DurableActivityExample");
            Assert.Equal("activityTrigger", activity.Bindings["name"].Type);

            var client = results.First(f => f.Name == "DurableClientExample");
            Assert.True(client.Bindings.ContainsKey("Request"));
            Assert.Equal("httpTrigger", client.Bindings["Request"].Type);
            Assert.True(client.Bindings.ContainsKey("starter"));
            Assert.Equal("durableClient", client.Bindings["starter"].Type);
            Assert.Equal(Microsoft.Azure.WebJobs.Script.Grpc.Messages.BindingInfo.Types.Direction.In,
                client.Bindings["starter"].Direction);
        }

        #endregion

        #region Validation: Multiple Triggers

        [Fact]
        public void ThrowsForMultipleTriggerBindings()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FunctionIndexer.IndexFunctionsInFile(
                    Path.Combine(TestScriptsDir, "MultipleTriggers.psm1"), TestScriptsDir).ToList());

            Assert.Contains("multiple trigger", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("BadFunction", ex.Message);
        }

        #endregion

        #region Validation: No Trigger

        [Fact]
        public void ThrowsForNoTriggerBinding()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FunctionIndexer.IndexFunctionsInFile(
                    Path.Combine(TestScriptsDir, "NoTrigger.psm1"), TestScriptsDir).ToList());

            Assert.Contains("no trigger", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("NoTriggerFunc", ex.Message);
        }

        #endregion

        #region Validation: Missing Required Property

        [Fact]
        public void ThrowsForMissingRequiredProperty()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FunctionIndexer.IndexFunctionsInFile(
                    Path.Combine(TestScriptsDir, "MissingRequiredProperty.psm1"), TestScriptsDir).ToList());

            Assert.Contains("Schedule", ex.Message);
            Assert.Contains("MissingSchedule", ex.Message);
            Assert.Contains("timerTrigger", ex.Message);
        }

        [Fact]
        public void ThrowsForMissingMultipleRequiredProperties()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FunctionIndexer.IndexFunctionsInFile(
                    Path.Combine(TestScriptsDir, "MissingMultipleRequiredProperties.psm1"), TestScriptsDir).ToList());

            // Should mention at least one of the missing required properties
            Assert.Contains("MissingCosmosProps", ex.Message);
            Assert.True(
                ex.Message.Contains("DatabaseName") || ex.Message.Contains("ContainerName"),
                $"Expected mention of DatabaseName or ContainerName in: {ex.Message}");
        }

        #endregion

        #region Validation: Duplicate Binding Name

        [Fact]
        public void ThrowsForDuplicateBindingName()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FunctionIndexer.IndexFunctionsInFile(
                    Path.Combine(TestScriptsDir, "DuplicateBindingName.psm1"), TestScriptsDir).ToList());

            Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Request", ex.Message);
        }

        #endregion

        #region Validation: Errors Handled Gracefully by IndexFunctions

        [Fact]
        public void IndexFunctionsSkipsInvalidFunctionsAndContinues()
        {
            // When IndexFunctions (with logger) encounters a validation error,
            // it should skip that file and continue indexing valid files.
            var results = FunctionIndexer.IndexFunctions(TestScriptsDir, logger: null).ToList();

            // Valid functions should still be found (HttpExample, MyTimerFunc, etc.)
            Assert.True(results.Count >= 5, $"Expected at least 5 valid functions, found {results.Count}");

            // None of the invalid functions should be in the results
            Assert.DoesNotContain(results, f => f.Name == "BadFunction");
            Assert.DoesNotContain(results, f => f.Name == "NoTriggerFunc");
            Assert.DoesNotContain(results, f => f.Name == "MissingSchedule");
        }

        #endregion

        #region Explicit Output Overrides Implicit

        [Fact]
        public void ExplicitHttpOutputOverridesImplicitResponse()
        {
            // An [HttpTrigger] normally adds an implicit "Response" output.
            // If the user explicitly adds [HttpOutput()] $Response, the explicit one should win.
            var scriptContent = @"
function ExplicitResponseFunc {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = 'Anonymous')]
        $Request,

        [HttpOutput()]
        $Response
    )
    Push-OutputBinding -Name Response -Value 'test'
}
";
            var tempFile = Path.Combine(Path.GetTempPath(), "ExplicitHttpOutput_" + Guid.NewGuid() + ".psm1");
            try
            {
                File.WriteAllText(tempFile, scriptContent);
                var results = FunctionIndexer.IndexFunctionsInFile(tempFile, Path.GetTempPath()).ToList();

                Assert.Single(results);
                var func = results[0];

                // Should have both Request (trigger) and Response (explicit output) — no duplicate error
                Assert.True(func.Bindings.ContainsKey("Request"));
                Assert.True(func.Bindings.ContainsKey("Response"));
                Assert.Equal("http", func.Bindings["Response"].Type);
                Assert.Equal(Microsoft.Azure.WebJobs.Script.Grpc.Messages.BindingInfo.Types.Direction.Out,
                    func.Bindings["Response"].Direction);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        #endregion

        #region Directory Scanning

        [Fact]
        public void IndexFunctionsScansDirectoryRecursively()
        {
            // IndexFunctions on the TestScripts directory should find all functions across all files
            var results = FunctionIndexer.IndexFunctions(TestScriptsDir, logger: null).ToList();

            // At minimum: HttpExample, MyTimerFunc, ProcessQueue, MultipleFunctions_First,
            // MultipleFunctions_Second, GenericExample, DurableOrchExample, DurableActivityExample, DurableClientExample
            // (invalid test scripts like MultipleTriggers, NoTrigger, MissingRequiredProperty, etc. are skipped)
            Assert.True(results.Count >= 9, $"Expected at least 9 functions, found {results.Count}");

            // Verify no invalid functions leaked through
            Assert.DoesNotContain(results, f => f.Name == "BadFunction");
            Assert.DoesNotContain(results, f => f.Name == "NoTriggerFunc");
            Assert.DoesNotContain(results, f => f.Name == "MissingSchedule");
            Assert.DoesNotContain(results, f => f.Name == "MissingCosmosProps");
            Assert.DoesNotContain(results, f => f.Name == "DuplicateBindingFunc");

            // Verify no duplicates
            var names = results.Select(f => f.Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        [Fact]
        public void ParseErrorsAreHandledGracefully()
        {
            // IndexFunctions should skip files with parse errors and continue
            var results = FunctionIndexer.IndexFunctions(TestScriptsDir, logger: null).ToList();

            // Should still have results from valid files
            Assert.True(results.Count > 0);
            // ParseError.ps1 should not contribute any functions
            Assert.DoesNotContain(results, f => f.Name == "Broken");
        }

        #endregion

        #region FuncIgnore

        [Fact]
        public void GlobToRegexMatchesWildcardPatterns()
        {
            // * matches any chars except path separator
            var pattern = FunctionIndexer.GlobToRegex("*.test.ps1");
            var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            Assert.True(regex.IsMatch("MyFunc.test.ps1"));
            Assert.True(regex.IsMatch("x.test.ps1"));
            Assert.False(regex.IsMatch("MyFunc.ps1"));
            Assert.False(regex.IsMatch("sub/MyFunc.test.ps1")); // * doesn't match /
        }

        [Fact]
        public void GlobToRegexMatchesDoubleStarPatterns()
        {
            var pattern = FunctionIndexer.GlobToRegex("**/test");
            var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            Assert.True(regex.IsMatch("sub/test"));
            Assert.True(regex.IsMatch("a/b/test"));
            Assert.True(regex.IsMatch("test"));
        }

        [Fact]
        public void GlobToRegexMatchesDotGitStar()
        {
            var pattern = FunctionIndexer.GlobToRegex(".git*");
            var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            Assert.True(regex.IsMatch(".git"));
            Assert.True(regex.IsMatch(".gitignore"));
            Assert.True(regex.IsMatch(".github"));
            Assert.False(regex.IsMatch("notgit"));
        }

        [Fact]
        public void LoadFuncIgnoreParsesFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "funcignore_test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllText(Path.Combine(tempDir, ".funcignore"),
                    "# Comment line\n\n*.test.ps1\nlocal.settings.json\n.git*\n");

                var patterns = FunctionIndexer.LoadFuncIgnorePatterns(tempDir);

                Assert.Equal(3, patterns.Count);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void LoadFuncIgnoreReturnsEmptyWhenNoFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "funcignore_empty_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                var patterns = FunctionIndexer.LoadFuncIgnorePatterns(tempDir);
                Assert.Empty(patterns);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void IndexFunctionsRespectsExcludedDirectoriesAndFuncIgnore()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "funcignore_scan_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                var funcContent = @"
function IncludedFunc {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = 'Anonymous')]
        $Request
    )
    Push-OutputBinding -Name Response -Value 'ok'
}
";
                var excludedContent = @"
function ExcludedFunc {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = 'Anonymous')]
        $Request
    )
    Push-OutputBinding -Name Response -Value 'excluded'
}
";

                // Write a .funcignore that excludes the 'ignored' directory
                File.WriteAllText(Path.Combine(tempDir, ".funcignore"), "ignored\n");

                // Create included file at root
                File.WriteAllText(Path.Combine(tempDir, "Functions.psm1"), funcContent);

                // Create excluded file in 'ignored' directory
                var ignoredDir = Path.Combine(tempDir, "ignored");
                Directory.CreateDirectory(ignoredDir);
                File.WriteAllText(Path.Combine(ignoredDir, "Excluded.psm1"), excludedContent);

                var results = FunctionIndexer.IndexFunctions(tempDir, logger: null).ToList();

                Assert.Single(results);
                Assert.Equal("IncludedFunc", results[0].Name);
                Assert.DoesNotContain(results, f => f.Name == "ExcludedFunc");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void IndexFunctionsExcludesFilesByFuncIgnorePattern()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "funcignore_files_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                var funcContent = @"
function IncludedFunc {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = 'Anonymous')]
        $Request
    )
    Push-OutputBinding -Name Response -Value 'ok'
}
";
                var testContent = @"
function TestFunc {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = 'Anonymous')]
        $Request
    )
    Push-OutputBinding -Name Response -Value 'test'
}
";

                // .funcignore excludes test files
                File.WriteAllText(Path.Combine(tempDir, ".funcignore"), "*.test.psm1\n");

                File.WriteAllText(Path.Combine(tempDir, "Functions.psm1"), funcContent);
                File.WriteAllText(Path.Combine(tempDir, "Functions.test.psm1"), testContent);

                var results = FunctionIndexer.IndexFunctions(tempDir, logger: null).ToList();

                Assert.Single(results);
                Assert.Equal("IncludedFunc", results[0].Name);
                Assert.DoesNotContain(results, f => f.Name == "TestFunc");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        #endregion

        #region E2E App Dump

        [Fact]
        public void DumpV2E2EAppIndexedMetadata()
        {
            // BaseDirectory is test/Unit/bin/Debug/net10.0/
            // Go up 3 to test/Unit/, then up 1 to test/, then into E2E/TestFunctionAppV2
            var e2eAppDir = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "E2E", "TestFunctionAppV2"));

            Assert.True(Directory.Exists(e2eAppDir), $"E2E app dir not found: {e2eAppDir}");

            var results = FunctionIndexer.IndexFunctions(e2eAppDir, logger: null).ToList();

            // Emit as a JSON array for easy inspection
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < results.Count; i++)
            {
                var func = results.OrderBy(f => f.Name).ElementAt(i);
                sb.AppendLine("  {");
                sb.AppendLine($"    \"name\": \"{func.Name}\",");
                sb.AppendLine($"    \"entryPoint\": \"{func.EntryPoint}\",");
                sb.AppendLine($"    \"scriptFile\": \"{Path.GetFileName(func.ScriptFile)}\",");
                sb.AppendLine($"    \"bindings\": {{");
                var bindingPairs = func.Bindings.Select(b => $"      \"{b.Key}\": {{ \"type\": \"{b.Value.Type}\", \"direction\": \"{b.Value.Direction}\" }}");
                sb.AppendLine(string.Join(",\n", bindingPairs));
                sb.AppendLine("    },");
                sb.AppendLine($"    \"rawBindings\": [");
                var rawItems = func.RawBindings.Select(rb => $"      {rb}");
                sb.AppendLine(string.Join(",\n", rawItems));
                sb.AppendLine("    ]");
                sb.Append(i < results.Count - 1 ? "  },\n" : "  }\n");
            }
            sb.AppendLine("]");
            Console.WriteLine(sb.ToString());

            Assert.True(results.Count > 0, "No functions indexed");
        }

        #endregion
    }
}
