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
    using System.Linq;
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

        private string InitializeManagedDependenciesDirectory(string functionAppRootPath)
        {
            string functionAppName = Path.GetFileName(functionAppRootPath);
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            string managedDependenciesFolderPath = Path.Combine(appDataFolder, AzureFunctionsFolderName, functionAppName, ManagedDependenciesFolderName);

            if (Directory.Exists(managedDependenciesFolderPath))
            {
                Directory.Delete(managedDependenciesFolderPath, recursive: true);
            }

            Directory.CreateDirectory(managedDependenciesFolderPath);

            return managedDependenciesFolderPath;
        }

        private void TestCaseCleanup()
        {
            // We run a test case clean up to reset DependencyManager.Dependencies and DependencyManager.DependenciesPath
            var functionFolderPath = Path.Combine(_dependencyManagementDirectory, "DirectoryThatDoesNotExist");
            var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

            try
            {
                using (var dependencyManager = new DependencyManager(functionLoadRequest.Metadata.Directory))
                {
                    dependencyManager.Initialize(_testLogger);
                }
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
                var managedDependenciesFolderPath = InitializeManagedDependenciesDirectory(functionAppRoot);

                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                using (var dependencyManager = new DependencyManager(functionLoadRequest.Metadata.Directory))
                {
                    var currentDependenciesPath = dependencyManager.Initialize(_testLogger);

                    // Validate that dependenciesPath and DependencyManager.Dependencies are set correctly.
                    var dependenciesPathIsValid = currentDependenciesPath.StartsWith(
                                                    managedDependenciesFolderPath,
                                                    StringComparison.CurrentCultureIgnoreCase);
                    Assert.True(dependenciesPathIsValid);
                }
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
                var managedDependenciesFolderPath = InitializeManagedDependenciesDirectory(functionAppRoot);
                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                using (var dependencyManager = new DependencyManager(functionLoadRequest.Metadata.Directory))
                {
                    var currentDependenciesPath = dependencyManager.Initialize(_testLogger);

                    Assert.Null(currentDependenciesPath);
                }
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

            using (var dependencyManager = new DependencyManager(functionLoadRequest.Metadata.Directory))
            {
                // Trying to set the functionApp dependencies should throw since requirements.psd1 is not a hash table.
                var exception = Assert.Throws<DependencyInstallationException>(
                                    () => { dependencyManager.Initialize(_testLogger); });
                Assert.Contains("The PowerShell data file", exception.Message);
                Assert.Contains("requirements.psd1", exception.Message);
                Assert.Contains("is invalid since it cannot be evaluated into a Hashtable object", exception.Message);
            }
        }

        [Fact]
        public void TestManagedDependencyInvalidRequirementsFormatShouldThrow()
        {
            // Test case setup.
            var requirementsDirectoryName = "InvalidRequirementsFormat";
            var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
            var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

            using (var dependencyManager = new DependencyManager(functionLoadRequest.Metadata.Directory))
            {
                // Trying to set the functionApp dependencies should throw since the module version
                // in requirements.psd1 is not in a valid format.
                var exception = Assert.Throws<DependencyInstallationException>(
                                    () => { dependencyManager.Initialize(_testLogger); });

                Assert.Contains("not in the correct format.", exception.Message);
                Assert.Contains("1.0.*", exception.Message);
                Assert.Contains("Please specify the exact version or use the following notation: 'MajorVersion.*'", exception.Message);
            }
        }

        [Fact]
        public void TestManagedDependencyNoRequirementsFileShouldThrow()
        {
            // Test case setup.
            var requirementsDirectoryName = "ModuleThatDoesNotExist";
            var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
            var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

            using (var dependencyManager = new DependencyManager(functionLoadRequest.Metadata.Directory))
            {
                // Trying to set the functionApp dependencies should throw since no
                // requirements.psd1 is found at the function app root.
                var exception = Assert.Throws<DependencyInstallationException>(
                                    () => { dependencyManager.Initialize(_testLogger); });

                Assert.Contains("No 'requirements.psd1'", exception.Message);
                Assert.Contains("is found at the FunctionApp root folder", exception.Message);
            }
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
                var managedDependenciesFolderPath = InitializeManagedDependenciesDirectory(functionAppRoot);
                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Configure MockModuleProvider to mimic a successful download.
                var mockModuleProvider = new MockModuleProvider { SuccessfulDownload = true };

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                using (var dependencyManager = new DependencyManager(functionLoadRequest.Metadata.Directory, mockModuleProvider))
                {
                    dependencyManager.Initialize(_testLogger);

                    // Install the function app dependencies.
                    var dependencyError = dependencyManager.InstallFunctionAppDependencies(PowerShell.Create(), PowerShell.Create, _testLogger);

                    var relevantLogs = _testLogger.FullLog.Where(
                        message => message.StartsWith("Trace: Module name")
                                   || message.StartsWith("Trace: Installing FunctionApp dependent modules")).ToList();

                    // Here we will get two logs: one that says that we are installing the dependencies, and one for a successful download.
                    Assert.Equal(2, relevantLogs.Count);

                    // The first log should say "Installing FunctionApp dependent modules."
                    Assert.Contains(PowerShellWorkerStrings.InstallingFunctionAppDependentModules, relevantLogs[0]);

                    // In the overwritten RunSaveModuleCommand method, we saved in DownloadedModuleInfo the module name and version.
                    // This same information is logged after running save-module, so validate that they match.
                    Assert.Contains(mockModuleProvider.DownloadedModuleInfo, relevantLogs[1]);

                    // Lastly, DependencyError should be null since the module was downloaded successfully.
                    Assert.Null(dependencyError);
                }
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
                var managedDependenciesFolderPath = InitializeManagedDependenciesDirectory(functionAppRoot);

                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Configure MockModuleProvider to not throw in the RunSaveModuleCommand call after 2 tries.
                var mockModuleProvider = new MockModuleProvider { ShouldNotThrowAfterCount = 2 };

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                using (var dependencyManager = new DependencyManager(functionLoadRequest.Metadata.Directory, mockModuleProvider))
                {
                    dependencyManager.Initialize(_testLogger);

                    // Try to install the function app dependencies.
                    var dependencyError = dependencyManager.InstallFunctionAppDependencies(PowerShell.Create(), PowerShell.Create, _testLogger);

                    var relevantLogs = _testLogger.FullLog.Where(
                        message => message.StartsWith("Error: Fail to install module")
                                   || message.StartsWith("Trace: Installing FunctionApp dependent modules")
                                   || message.StartsWith("Trace: Module name")).ToList();

                    // Here we will get four logs:
                    // - one that say that we are installing the dependencies
                    // - two that say that we failed to download the module
                    // - one for a successful module download
                    Assert.Equal(4, relevantLogs.Count);

                    // The first log should say "Installing FunctionApp dependent modules."
                    Assert.Contains(PowerShellWorkerStrings.InstallingFunctionAppDependentModules, relevantLogs[0]);

                    // The subsequent two logs should contain the following: "Fail to install module"
                    for (int index = 1; index < relevantLogs.Count - 1; index++)
                    {
                        Assert.Contains("Fail to install module", relevantLogs[index]);
                        var currentAttempt = DependencySnapshotInstaller.GetCurrentAttemptMessage(index);
                        Assert.Contains(currentAttempt, relevantLogs[index]);
                    }

                    // Successful module download log after two retries.
                    // In the overwritten RunSaveModuleCommand method, we saved in DownloadedModuleInfo the module name and version.
                    // This same information is logged after running save-module, so validate that they match.
                    Assert.Contains(mockModuleProvider.DownloadedModuleInfo, relevantLogs[3]);

                    // Lastly, DependencyError should be null since the module was downloaded successfully after two tries.
                    Assert.Null(dependencyError);
                }
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
                var managedDependenciesFolderPath = InitializeManagedDependenciesDirectory(functionAppRoot);

                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and process the requirements.psd1 file at the function app root.
                using (var dependencyManager = new DependencyManager(functionLoadRequest.Metadata.Directory, new MockModuleProvider()))
                {
                    dependencyManager.Initialize(_testLogger);

                    // Try to install the function app dependencies.
                    var dependencyError = dependencyManager.InstallFunctionAppDependencies(PowerShell.Create(), PowerShell.Create, _testLogger);

                    var relevantLogs = _testLogger.FullLog.Where(
                        message => message.StartsWith("Error: Fail to install module")
                                   || message.StartsWith("Trace: Installing FunctionApp dependent modules")
                                   || message.StartsWith("Warning: Failed to install dependencies")).ToList();

                    // Here we will get five logs: one that says that we are installing the
                    // dependencies, three for failing to install the module,
                    // and one warning notifying of removing the dependencies folder.
                    Assert.Equal(5, relevantLogs.Count);

                    // The first log should say "Installing FunctionApp dependent modules."
                    Assert.Contains(PowerShellWorkerStrings.InstallingFunctionAppDependentModules, relevantLogs[0]);

                    // The subsequent logs should contain the following:
                    for (int index = 1; index < 4; index++)
                    {
                        Assert.Contains("Fail to install module", relevantLogs[index]);
                        var currentAttempt = DependencySnapshotInstaller.GetCurrentAttemptMessage(index);
                        Assert.Contains(currentAttempt, relevantLogs[index]);
                    }

                    Assert.Matches("Warning: Failed to install dependencies into '(.+?)', removing the folder", relevantLogs[4]);

                    // Lastly, DependencyError should get set after unsuccessfully retrying 3 times.
                    Assert.NotNull(dependencyError);
                    Assert.Contains("Fail to install FunctionApp dependencies. Error:", dependencyError.Message);
                }
            }
            finally
            {
                TestCaseCleanup();
            }
        }

        [Fact]
        public void FunctionAppExecutionShouldStopIfNoPreviousDependenciesAreInstalled()
        {
            try
            {
                // Test case setup
                var requirementsDirectoryName = "BasicRequirements";
                var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
                var functionAppRoot = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName);
                var managedDependenciesFolderPath = InitializeManagedDependenciesDirectory(functionAppRoot);

                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and configure it to mimic being unable to reach
                // the PSGallery to retrieve the latest module version
                using (var dependencyManager = new DependencyManager(
                    functionLoadRequest.Metadata.Directory,
                    new MockModuleProvider { GetLatestModuleVersionThrows = true }))
                {
                    dependencyManager.Initialize(_testLogger);
                    dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _testLogger);
                    var dependencyError = Assert.Throws<DependencyInstallationException>(
                                            () => dependencyManager.WaitForDependenciesAvailability(() => _testLogger));

                    Assert.Contains("Fail to install FunctionApp dependencies.", dependencyError.Message);
                    Assert.Contains("Fail to get latest version for module 'Az' with major version '1'.", dependencyError.Message);
                    Assert.Contains("Fail to connect to the PSGallery", dependencyError.Message);
                }
            }
            finally
            {
                TestCaseCleanup();
            }
        }

        [Fact]
        public void FunctionAppExecutionShouldContinueIfPreviousDependenciesExist()
        {
            string AzModulePath = null;
            try
            {
                // Test case setup
                var requirementsDirectoryName = "BasicRequirements";
                var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName, "FunctionDirectory");
                var functionAppRoot = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName);
                var managedDependenciesFolderPath = InitializeManagedDependenciesDirectory(functionAppRoot);
                var functionLoadRequest = GetFuncLoadRequest(functionFolderPath, true);

                // Create DependencyManager and configure it to mimic being unable to reach
                // the PSGallery to retrive the latest module version
                using (var dependencyManager = new DependencyManager(
                    functionLoadRequest.Metadata.Directory,
                    new MockModuleProvider { GetLatestModuleVersionThrows = true }))
                {
                    // Create a path to mimic an existing installation of the Az module
                    AzModulePath = Path.Join(managedDependenciesFolderPath, "FakeDependenciesSnapshot", "Az");
                    if (Directory.Exists(AzModulePath))
                    {
                        Directory.Delete(AzModulePath, true);
                    }
                    Directory.CreateDirectory(Path.Join(AzModulePath, "1.0"));

                    // Initializing the dependency manager should not throw even though we were not able
                    // to connect to the PSGallery--given that a previous installation of the Az module is present
                    var currentDependenciesPath = dependencyManager.Initialize(_testLogger);

                    // Validate that DependencyManager.DependenciesPath is set, so
                    // Get-Module can find the existing dependencies installed
                    var dependenciesPathIsValid = currentDependenciesPath.StartsWith(
                        managedDependenciesFolderPath,
                        StringComparison.CurrentCultureIgnoreCase);
                    Assert.True(dependenciesPathIsValid);
                }
            }
            finally
            {
                if (Directory.Exists(AzModulePath))
                {
                    Directory.Delete(AzModulePath, true);
                }

                TestCaseCleanup();
            }
        }
    }

    class MockModuleProvider : IModuleProvider
    {
        public bool GetLatestModuleVersionThrows { get; set; }

        // RunSaveModuleCommand in the DependencyManager class has retry logic with a max number of tries
        // set to three. By default, we set ShouldNotThrowAfterCount to 4 to always throw.
        public int ShouldNotThrowAfterCount { get; set; } = 4;

        public bool SuccessfulDownload { get; set; }

        public string DownloadedModuleInfo { get; set; }

        private int SaveModuleCount { get; set; }

        public string GetLatestPublishedModuleVersion(string moduleName, string majorVersion)
        {
            if (GetLatestModuleVersionThrows)
            {
                throw new InvalidOperationException("Fail to connect to the PSGallery");
            }

            return "2.0";
        }

        public void SaveModule(PowerShell pwsh, string moduleName, string version, string path)
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

        public void Cleanup(PowerShell pwsh)
        {
        }
    }
}
