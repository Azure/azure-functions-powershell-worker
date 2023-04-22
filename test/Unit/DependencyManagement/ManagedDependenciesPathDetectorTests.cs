//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using System;
    using System.IO;
    using Xunit;
    using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;

    public class ManagedDependenciesPathDetectorTests
    {
        [Theory]
        [InlineData("CONTAINER_NAME", "MyContainerName", true)]
        [InlineData("WEBSITE_INSTANCE_ID", "MyInstanceId", true)]
        [InlineData(null, null, false)]
        public void ValidateManagedDependenciesPath(string name, string value, bool setEnvironmentVariable)
        {
            const string HomeDriveName = "HOME";
            const string FunctionName = "MyFunction";

            string expectedPath = null;

            if (setEnvironmentVariable)
            {
                Environment.SetEnvironmentVariable(name, value);
                Environment.SetEnvironmentVariable(HomeDriveName, "home");

                const string DataFolderName = "data";
                const string ManagedDependenciesFolderName = "ManagedDependencies";
                expectedPath = Path.Combine(HomeDriveName, DataFolderName, ManagedDependenciesFolderName);
            }
            else
            {
                const string ManagedDependenciesFolderName = "ManagedDependencies";
                const string AzureFunctionsFolderName = "AzureFunctions";
                string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
                expectedPath = Path.Combine(appDataFolder, AzureFunctionsFolderName, FunctionName, ManagedDependenciesFolderName);
            }

            var functionAppRoot = Path.Join(Path.GetTempPath(), FunctionName);

            try
            {
                var managedDependenciesPath = ManagedDependenciesPathDetector.GetManagedDependenciesPath(functionAppRoot);
                var dependenciesPathIsValid = managedDependenciesPath.StartsWith(expectedPath, StringComparison.CurrentCultureIgnoreCase);
                Assert.True(dependenciesPathIsValid);
            }
            finally
            {
                if (setEnvironmentVariable)
                {
                    Environment.SetEnvironmentVariable(name, null);
                    Environment.SetEnvironmentVariable(HomeDriveName, null);
                }

                if (Directory.Exists(functionAppRoot))
                {
                    Directory.Delete(functionAppRoot);
                }
            }
        }
    }
}
