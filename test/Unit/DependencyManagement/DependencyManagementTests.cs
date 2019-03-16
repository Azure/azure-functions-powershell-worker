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
    public class DependencyManagementTests
    {
        private readonly string _dependencyManagementDirectory;
        private readonly string _functionId;
        private const string ManagedDependenciesFolderName = "ManagedDependencies";

        public DependencyManagementTests()
        {
            _dependencyManagementDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "TestScripts", "DependencyManagement");
            _functionId = Guid.NewGuid().ToString();
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
                var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName,
                    "FunctionDirectory");
                var managedDependenciesFolderPath = Path.Combine(_dependencyManagementDirectory,
                    requirementsDirectoryName, ManagedDependenciesFolderName);
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
                var functionFolderPath = Path.Combine(_dependencyManagementDirectory, requirementsDirectoryName,
                    "FunctionDirectory");
                var managedDependenciesFolderPath = Path.Combine(_dependencyManagementDirectory,
                    requirementsDirectoryName, ManagedDependenciesFolderName);
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
    }
}
