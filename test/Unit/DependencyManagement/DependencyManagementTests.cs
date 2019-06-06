//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;
using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    using System.Management.Automation;

    public class DependencyManagementTests : IDisposable
    {
        private readonly string _dependencyManagementDirectory;
        private readonly string _functionId;
        private const string ManagedDependenciesFolderName = "ManagedDependencies";
        private const string AzureFunctionsFolderName = "AzureFunctions";
        private readonly ConsoleLogger _testLogger;

        public DependencyManagementTests()
        {
            _dependencyManagementDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "TestScripts", "DependencyManagement");
            _functionId = Guid.NewGuid().ToString();
            _testLogger = new ConsoleLogger();
        }

        public void Dispose()
        {
            _testLogger.FullLog.Clear();
        }

        private FunctionLoadRequest GetFuncLoadRequest(string functionAppRoot, bool managedDependencyEnabled)
        {
            var metadata = new RpcFunctionMetadata
            {
                Name = "MyHttpTrigger",
                Directory = functionAppRoot
            };

            var functionLoadRequest = new FunctionLoadRequest
            {
                FunctionId = _functionId,
                Metadata = metadata
            };

            functionLoadRequest.ManagedDependencyEnabled = managedDependencyEnabled;
            return functionLoadRequest;
        }

        private string GetManagedDependenciesPath(string functionAppRootPath)
        {
            string functionAppName = Path.GetFileName(functionAppRootPath);
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            string managedDependenciesFolderPath = Path.Combine(appDataFolder, AzureFunctionsFolderName, functionAppName, ManagedDependenciesFolderName);
            return managedDependenciesFolderPath;
        }

        private void TestCaseCleanup()
        {
            // We run a test case clean up to reset DependencyManager.Dependencies and DependencyManager.DependenciesPath
            var functionFolderPath = Path.Combine(_dependencyManagementDirectory, "DirectoryThatDoesNotExist");
            var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

            try
            {
                var dependencyManager = new DependencyManager();
                dependencyManager.Initialize(functionLoadRequest);
            }
            catch
            {
                // It is ok to ignore the exception here.
            }
        }

        [Fact]
        public void TestManagedDependencyBasicRequirements()
        {
            try
            {
                // Test case setup.
                var requirementsDirectoryName = "BasicRequirements";
                var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
                var functionAppRoot = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName);
                var managedDependenciesFolderPath = GetManagedDependenciesPath(functionAppRoot);

                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                var dependencyManager = new DependencyManager();
                dependencyManager.Initialize(functionLoadRequest);

                // Validate that DependencyManager.DependenciesPath and DependencyManager.Dependencies are set correctly.
                var dependenciesPathIsValid = managedDependenciesFolderPath.Equals(DependencyManager.DependenciesPath,
                    StringComparison.CurrentCultureIgnoreCase);
                Assert.True(dependenciesPathIsValid);

                // Dependencies.Count should be 1.
                Assert.Single(DependencyManager.Dependencies);
            }
            finally
            {
                TestCaseCleanup();
            }
        }

        [Fact]
        public void TestManagedDependencyEmptyHashtableRequirement()
        {
            try
            {
                // Test case setup.
                var requirementsDirectoryName = "EmptyHashtableRequirement";
                var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
                var functionAppRoot = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName);
                var managedDependenciesFolderPath = GetManagedDependenciesPath(functionAppRoot);
                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                var dependencyManager = new DependencyManager();
                dependencyManager.Initialize(functionLoadRequest);

                // Validate that DependencyManager.DependenciesPath and DependencyManager.Dependencies are set correctly.
                var dependenciesPathIsValid = managedDependenciesFolderPath.Equals(DependencyManager.DependenciesPath,
                    StringComparison.CurrentCultureIgnoreCase);
                Assert.True(dependenciesPathIsValid);

                // Dependencies.Count should be 0 since requirements.psd1 is an empty hashtable.
                Assert.True(DependencyManager.Dependencies.Count == 0);
            }
            finally
            {
                TestCaseCleanup();
            }
        }

        [Fact]
        public void TestManagedDependencyNoHashtableRequirementShouldThrow()
        {
            // Test case setup.
            var requirementsDirectoryName = "NoHashtableRequirements";
            var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
            var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

            // Trying to set the functionApp dependencies should throw since requirements.psd1 is not a hash table.
            var exception = Assert.Throws<DependencyInstallationException>(
                () => new DependencyManager().Initialize(functionLoadRequest));
            Assert.Contains("The PowerShell data file", exception.Message);
            Assert.Contains("requirements.psd1", exception.Message);
            Assert.Contains("is invalid since it cannot be evaluated into a Hashtable object", exception.Message);
        }

        [Fact]
        public void TestManagedDependencyInvalidRequirementsFormatShouldThrow()
        {
            // Test case setup.
            var requirementsDirectoryName = "InvalidRequirementsFormat";
            var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
            var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

            // Trying to set the functionApp dependencies should throw since the module version
            // in requirements.psd1 is not in a valid format.
            var exception = Assert.Throws<DependencyInstallationException>(
                () => new DependencyManager().Initialize(functionLoadRequest));
            Assert.Contains("Version is not in the correct format.", exception.Message);
            Assert.Contains("Please use the following notation:", exception.Message);
            Assert.Contains("MajorVersion.*", exception.Message);
        }

        [Fact]
        public void TestManagedDependencyNoRequirementsFileShouldThrow()
        {
            // Test case setup.
            var requirementsDirectoryName = "ModuleThatDoesNotExist";
            var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
            var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

            // Trying to set the functionApp dependencies should throw since no
            // requirements.psd1 is found at the function app root.
            var exception = Assert.Throws<DependencyInstallationException>(
                () => new DependencyManager().Initialize(functionLoadRequest));
            Assert.Contains("No 'requirements.psd1'", exception.Message);
            Assert.Contains("is found at the FunctionApp root folder", exception.Message);
        }

        [Fact]
        public void TestManagedDependencySuccessfulModuleDownload()
        {
            string filePath = null;
            try
            {
                // Test case setup.
                var requirementsDirectoryName = "BasicRequirements";
                var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName,
                    "FunctionDirectory");
                var functionAppRoot = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName);
                var managedDependenciesFolderPath = GetManagedDependenciesPath(functionAppRoot);
                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                var dependencyManager = new TestDependencyManager();
                dependencyManager.Initialize(functionLoadRequest);

                // Install the function app dependencies.
                dependencyManager.InstallFunctionAppDependencies(null, _testLogger);

                // Here we will get two logs: one that says that we are installing the dependencies, and one for a successful download.
                bool correctLogCount = (_testLogger.FullLog.Count == 2);
                Assert.True(correctLogCount);

                // The first log should say "Installing FunctionApp dependent modules."
                Assert.Contains(PowerShellWorkerStrings.InstallingFunctionAppDependentModules, _testLogger.FullLog[0]);

                // In the overwritten RunSaveModuleCommand method, we write a text file with module name and version.
                // Read the file content of the generated file.
                filePath = Path.Join(DependencyManager.DependenciesPath, dependencyManager.TestFileName);
                string fileContent = File.ReadAllText(filePath);

                // After running save module, we write a log with the module name and version.
                // This should match was is written in the log.
                Assert.Contains(fileContent, _testLogger.FullLog[1]);

                // Lastly, DependencyError should be null since the module was downloaded successfully.
                Assert.Null(dependencyManager.DependencyError);
            }
            finally
            {
                TestCaseCleanup();
                if (filePath != null && File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }
             }
        }

        [Fact]
        public void TestManagedDependencyRetryLogic()
        {
            try
            {
                // Test case setup
                var requirementsDirectoryName = "BasicRequirements";
                var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
                var functionAppRoot = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName);
                var managedDependenciesFolderPath = GetManagedDependenciesPath(functionAppRoot);

                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                var dependencyManager = new TestDependencyManager();
                dependencyManager.Initialize(functionLoadRequest);

                // Set the dependencyManager to throw in the RunSaveModuleCommand call.
                dependencyManager.ShouldThrow = true;

                // Validate retry logic.
                dependencyManager.InstallFunctionAppDependencies(null, _testLogger);

                // Here we will get four logs: one that says that we are installing the
                // dependencies, and three for failing to install the module.
                bool correctLogCount = (_testLogger.FullLog.Count == 4);
                Assert.True(correctLogCount);

                // The first log should say "Installing FunctionApp dependent modules."
                Assert.Contains(PowerShellWorkerStrings.InstallingFunctionAppDependentModules, _testLogger.FullLog[0]);

                // The subsequent logs should contain the following:
                for (int index = 1; index < _testLogger.FullLog.Count; index++)
                {
                    Assert.Contains("Fail to install module", _testLogger.FullLog[index]);
                }

                // Lastly, DependencyError should get set after unsuccessfully  retyring 3 times.
                Assert.NotNull(dependencyManager.DependencyError);
                Assert.Contains("Fail to install FunctionApp dependencies. Error:", dependencyManager.DependencyError.Message);
            }
            finally
            {
                TestCaseCleanup();
            }
        }
    }

    internal class TestDependencyManager : DependencyManager
    {
        public bool ShouldThrow { get; set; }

        public string TestFileName { get; set; } = "ModuleInstalled.txt";

        internal TestDependencyManager()
        {
        }

        protected override void RunSaveModuleCommand(PowerShell pwsh, string repository, string moduleName, string version, string path)
        {
            if (ShouldThrow)
            {
                var errorMsg = $"Fail to install module '{moduleName}' version '{version}'";
                throw new InvalidOperationException(errorMsg);
            }

            // Write a text file to the given path with the information of the module that was downloaded.
            var message = string.Format(PowerShellWorkerStrings.ModuleHasBeenInstalled, moduleName, version);
            var filePath = Path.Join(path, TestFileName);
            File.WriteAllText(filePath, message);
        }

        protected override void RemoveSaveModuleModules(PowerShell pwsh)
        {
            return;
        }
    }
}
