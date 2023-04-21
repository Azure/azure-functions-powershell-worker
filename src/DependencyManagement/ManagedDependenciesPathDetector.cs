//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal static class ManagedDependenciesPathDetector
    {
        // Environment variables to help figure out if we are running in Azure.
        private const string AzureWebsiteInstanceId = "WEBSITE_INSTANCE_ID";
        private const string ContainerName = "CONTAINER_NAME";
        private const string LegionServiceHost = "LEGION_SERVICE_HOST";

        private const string HomeDriveName = "HOME";
        private const string DataFolderName = "data";

        // AzureFunctions folder name.
        private const string AzureFunctionsFolderName = "AzureFunctions";

        // Managed Dependencies folder name.
        private const string ManagedDependenciesFolderName = "ManagedDependencies";

        /// <summary>
        /// Gets the Managed Dependencies folder path.
        /// If we are running in Azure, the path is HOME\data\ManagedDependencies.
        /// Otherwise, the path is LocalApplicationData\AzureFunctions\FunctionAppName\ManagedDependencies.
        /// </summary>
        public static string GetManagedDependenciesPath(string functionAppRootPath)
        {
            if (IsLinuxConsumptionOnLegion())
            {
                throw new NotSupportedException(PowerShellWorkerStrings.ManagedDependenciesIsNotSupportedOnLegion);
            }

            // If we are running in Azure App Service or Linux Consumption use the 'HOME\data' path.
            if (IsAppService() || IsLinuxConsumption())
            {
                var homeDriveVariable = Environment.GetEnvironmentVariable(HomeDriveName);
                if (string.IsNullOrEmpty(homeDriveVariable))
                {
                    var errorMsg = string.Format(PowerShellWorkerStrings.FailToResolveHomeDirectory, HomeDriveName);
                    throw new ArgumentException(errorMsg);
                }

                return Path.Combine(homeDriveVariable, DataFolderName, ManagedDependenciesFolderName);
            }

            // Otherwise, the ManagedDependencies folder is created under LocalApplicationData\AzureFunctions\FunctionAppName\ManagedDependencies.
            string functionAppName = Path.GetFileName(functionAppRootPath);
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            return Path.Combine(appDataFolder, AzureFunctionsFolderName, functionAppName, ManagedDependenciesFolderName);
        }

        private static bool IsAppService()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AzureWebsiteInstanceId));
        }

        private static bool IsLinuxConsumption()
        {
            return !IsAppService() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ContainerName));
        }

        public static bool IsLinuxConsumptionOnLegion()
        {
            return !IsAppService() &&
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ContainerName)) &&
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LegionServiceHost));
        }
    }
}
