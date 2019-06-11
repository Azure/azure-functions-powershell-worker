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
            try
            {
                // Test case setup.
                var requirementsDirectoryName = "BasicRequirements";
                var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
                var functionAppRoot = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName);
                var managedDependenciesFolderPath = GetManagedDependenciesPath(functionAppRoot);
                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                var dependencyManager = new TestDependencyManager();
                dependencyManager.Initialize(functionLoadRequest);

                // Configure the dependency manager to mimic a successful download.
                dependencyManager.SuccessfulDownload = true;

                // Install the function app dependencies.
                dependencyManager.InstallFunctionAppDependencies(null, _testLogger);

                // Here we will get two logs: one that says that we are installing the dependencies, and one for a successful download.
                bool correctLogCount = (_testLogger.FullLog.Count == 2);
                Assert.True(correctLogCount);

                // The first log should say "Installing FunctionApp dependent modules."
                Assert.Contains(PowerShellWorkerStrings.InstallingFunctionAppDependentModules, _testLogger.FullLog[0]);

                // In the overwritten RunSaveModuleCommand method, we saved in DownloadedModuleInfo the module name and version.
                // This same information is logged after running save-module, so validate that they match.
                Assert.Contains(dependencyManager.DownloadedModuleInfo, _testLogger.FullLog[1]);

                // Lastly, DependencyError should be null since the module was downloaded successfully.
                Assert.Null(dependencyManager.DependencyError);
            }
            finally
            {
                TestCaseCleanup();
            }
        }

        [Fact]
        public void TestManagedDependencySuccessfulModuleDownloadAfterTwoTries()
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

                // Configure the dependencyManager to not throw in the RunSaveModuleCommand call after 2 tries.
                dependencyManager.ShouldNotThrowAfterCount = 2;

                // Try to install the function app dependencies.
                dependencyManager.InstallFunctionAppDependencies(null, _testLogger);

                // Here we will get four logs:
                // - one that say that we are installing the dependencies
                // - two that say that we failed to download the module
                // - one for a successful module download
                bool correctLogCount = (_testLogger.FullLog.Count == 4);
                Assert.True(correctLogCount);

                // The first log should say "Installing FunctionApp dependent modules."
                Assert.Contains(PowerShellWorkerStrings.InstallingFunctionAppDependentModules, _testLogger.FullLog[0]);

                // The subsequent two logs should contain the following: "Fail to install module"
                for (int index = 1; index < _testLogger.FullLog.Count - 1; index++)
                {
                    Assert.Contains("Fail to install module", _testLogger.FullLog[index]);
                    var currentAttempt = dependencyManager.GetCurrentAttemptMessage(index);
                    Assert.Contains(currentAttempt, _testLogger.FullLog[index]);
                }

                // Successful module download log after two retries.
                // In the overwritten RunSaveModuleCommand method, we saved in DownloadedModuleInfo the module name and version.
                // This same information is logged after running save-module, so validate that they match.
                Assert.Contains(dependencyManager.DownloadedModuleInfo, _testLogger.FullLog[3]);

                // Lastly, DependencyError should be null since the module was downloaded successfully after two tries.
                Assert.Null(dependencyManager.DependencyError);
            }
            finally
            {
                TestCaseCleanup();
            }
        }

        [Fact]
        public void TestManagedDependencyRetryLogicMaxNumberOfTries()
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

                // Try to install the function app dependencies.
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
                    var currentAttempt = dependencyManager.GetCurrentAttemptMessage(index);
                    Assert.Contains(currentAttempt, _testLogger.FullLog[index]);
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
        // RunSaveModuleCommand in the DependencyManager class has retry logic with a max number of tries
        // set to three. By default, we set ShouldNotThrowAfterCount to 4 to always throw.
        public int ShouldNotThrowAfterCount { get; set; } = 4;

        public bool SuccessfulDownload { get; set; }

        public string DownloadedModuleInfo { get; set; }

        private int SaveModuleCount { get; set; }

        internal TestDependencyManager()
        {
        }

        protected override void RunSaveModuleCommand(PowerShell pwsh, string repository, string moduleName, string version, string path)
        {
            if (SuccessfulDownload || (SaveModuleCount >= ShouldNotThrowAfterCount))
            {
                // Save the module name and version for a successful download.
                DownloadedModuleInfo = string.Format(PowerShellWorkerStrings.ModuleHasBeenInstalled, moduleName, version);
                return;
            }

            SaveModuleCount++;

            var errorMsg = $"Fail to install module '{moduleName}' version '{version}'";
            throw new InvalidOperationException(errorMsg);
        }

        protected override void RemoveSaveModuleModules(PowerShell pwsh)
        {
            return;
        }
    }
}
