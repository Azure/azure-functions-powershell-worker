//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
    using System.Management.Automation;
    using System.Management.Automation.Language;

    internal class DependencyManager
    {
        // The list of dependent modules for the function app.
        internal static List<DependencyInfo> Dependencies { get; private set; }

        // This is the location where the dependent modules will be installed.
        internal static string DependenciesPath { get; private set; }

        // Az module name.
        private const string AzModuleName = "Az";

        // Requirements.psd1 file name.
        private const string RequirementsPsd1FileName = "requirements.psd1";

        // The list of managed dependencies supported in Azure Functions.
        internal static readonly List<string> SupportedManagedDependencies = new List<string>() { AzModuleName };

        // Environment variables to help figure out if we are running in Azure.
        private const string AzureWebsiteInstanceId = "WEBSITE_INSTANCE_ID";
        private const string HomeDriveName = "HOME";
        private const string DataFolderName = "data";

        // Central repository for acquiring PowerShell modules.
        private const string Repository = "PSGallery";

        // AzureFunctions folder name.
        private const string AzureFunctionsFolderName = "AzureFunctions";

        // Managed Dependencies folder name.
        private const string ManagedDependenciesFolderName = "ManagedDependencies";

        // This flag is used to figure out if we need to install/reinstall all the function app dependencies.
        // If we do, we use it to clean up the module destination path.
        private bool _shouldUpdateFunctionAppDependencies;

        internal DependencyManager()
        {
            Dependencies = new List<DependencyInfo>();
        }

        internal void ProcessDependencies(
            MessagingStream msgStream,
            PowerShellManagerPool powershellPool,
            StreamingMessage request,
            FunctionLoader functionLoader)
        {
            PowerShellManager psManager = null;
            StreamingMessage response = new StreamingMessage { RequestId = request.RequestId };
            var status = new StatusResult { Status = StatusResult.Types.Status.Success };

            try
            {
                var functionLoadRequest = request.FunctionLoadRequest;
                // If 'ManagedDependencyEnabled' is true, process the function app dependencies as defined in FunctionAppRoot\requirements.psd1.
                // These dependencies will be installed via 'Save-Module' when the first PowerShellManager instance is being created.
                if (functionLoadRequest.ManagedDependencyEnabled)
                {
                    Initialize(functionLoadRequest);
                }

                psManager = powershellPool.CheckoutIdleWorker(request);
                InstallFunctionAppDependencies(psManager.PowerShellInstance, psManager.Logger);
                response.FunctionLoadResponse.IsDependencyDownloaded = true;
                functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }
            finally
            {
                powershellPool.ReclaimUsedWorker(psManager);
                response.FunctionLoadResponse = new FunctionLoadResponse() { Result = status };
                msgStream.Write(response);
            }
        }

        /// <summary>
        /// Initializes the dependency manger and performs the following:
        /// - Parse functionAppRoot\requirements.psd1 file and create a list of dependencies to install.
        /// - Set the DependenciesPath which gets used in 'SetupWellKnownPaths'.
        /// - Determines if the dependency module needs to be installed by checking the latest available version
        ///   in the PSGallery and the destination path (to see if it is already installed).
        /// - Set the destination path (if running in Azure vs local) where the function app dependencies will be installed.
        /// </summary>
        internal void Initialize(FunctionLoadRequest request)
        {
            try
            {
                // Resolve the FunctionApp root path.
                var functionAppRootPath = Path.GetFullPath(Path.Join(request.Metadata.Directory, ".."));

                // Resolve the managed dependencies installation path.
                DependenciesPath = GetManagedDependenciesPath(functionAppRootPath);

                // Parse and process the function app dependencies defined in requirements.psd1.
                Hashtable entries = ParsePowerShellDataFile(functionAppRootPath, RequirementsPsd1FileName);
                foreach (DictionaryEntry entry in entries)
                {
                    // A valid entry is of the form: 'ModuleName'='MajorVersion.*"
                    string name = (string)entry.Key;
                    string version = (string)entry.Value;

                    // Validates that the module name is a supported dependency.
                    ValidateModuleName(name);

                    // Validate the module version.
                    string majorVersion = GetMajorVersion(version);
                    string latestVersion = DependencyManagementUtils.GetModuleLatestSupportedVersion(name, majorVersion);
                    ValidateModuleMajorVersion(name, majorVersion, latestVersion);

                    // Before installing the module, check the path to see if it is already installed.
                    var moduleVersionFolderPath = Path.Combine(DependenciesPath, name, latestVersion);
                    if (!Directory.Exists(moduleVersionFolderPath))
                    {
                        _shouldUpdateFunctionAppDependencies = true;
                    }

                    // Create a DependencyInfo object and add it to the list of dependencies to install.
                    var dependencyInfo = new DependencyInfo(name, majorVersion, latestVersion);
                    Dependencies.Add(dependencyInfo);
                }
            }
            catch (Exception e)
            {
                // Reset DependenciesPath and Dependencies.
                DependenciesPath = null;
                Dependencies.Clear();

                var errorMsg = string.Format(PowerShellWorkerStrings.FailToInstallFuncAppDependencies, e.Message);
                throw new DependencyInstallationException(errorMsg, e);
            }
        }

        /// <summary>
        /// Installs function app dependencies specified in functionAppRoot\requirements.psd1.
        /// </summary>
        internal void InstallFunctionAppDependencies(PowerShell pwsh, ILogger logger)
        {
            try
            {
                if (Dependencies.Count == 0)
                {
                    // If there are no dependencies to install, log and return.
                    logger.Log(LogLevel.Trace, PowerShellWorkerStrings.FunctionAppDoesNotHaveDependentModulesToInstall, isUserLog: true);
                    return;
                }

                if (!_shouldUpdateFunctionAppDependencies)
                {
                    // The function app already has the latest dependencies installed.
                    logger.Log(LogLevel.Trace, PowerShellWorkerStrings.LatestFunctionAppDependenciesAlreadyInstalled, isUserLog: true);
                    return;
                }

                // Install the function dependencies.
                logger.Log(LogLevel.Trace, PowerShellWorkerStrings.InstallingFunctionAppDependentModules, isUserLog: true);

                if (Directory.Exists(DependenciesPath))
                {
                    // Save-Module supports downloading side-by-size module versions. However, we only want to keep one version at the time.
                    // If the ManagedDependencies folder exits, remove all its contents.
                    DependencyManagementUtils.EmptyDirectory(DependenciesPath);
                }
                else
                {
                    // If the destination path does not exist, create it.
                    Directory.CreateDirectory(DependenciesPath);
                }

                try
                {
                    foreach (DependencyInfo module in Dependencies)
                    {
                        string moduleName = module.Name;
                        string latestVersion = module.LatestVersion;

                        // Save the module to the given path
                        pwsh.AddCommand("PowerShellGet\\Save-Module")
                            .AddParameter("Repository", Repository)
                            .AddParameter("Name", moduleName)
                            .AddParameter("RequiredVersion", latestVersion)
                            .AddParameter("Path", DependenciesPath)
                            .AddParameter("Force", true)
                            .AddParameter("ErrorAction", "Stop")
                            .InvokeAndClearCommands();

                        var message = string.Format(PowerShellWorkerStrings.ModuleHasBeenInstalled, moduleName, latestVersion);
                        logger.Log(LogLevel.Trace, message, isUserLog: true);
                    }
                }
                finally
                {
                    // Clean up
                    pwsh.AddCommand(Utils.RemoveModuleCmdletInfo)
                        .AddParameter("Name", "PackageManagement, PowerShellGet")
                        .AddParameter("Force", true)
                        .AddParameter("ErrorAction", "SilentlyContinue")
                        .InvokeAndClearCommands();
                }
            }
            catch (Exception e)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToInstallFuncAppDependencies, e.Message);
                throw new DependencyInstallationException(errorMsg, e);
            }
        }

        #region Helper_Methods

        /// <summary>
        /// Validates that the given major version is less or equal to the latest supported major version.
        /// </summary>
        private void ValidateModuleMajorVersion(string moduleName, string majorVersion, string latestVersion)
        {
            // A Version object cannot be created with a single digit so add a '.0' to it.
            var requestedVersion = new Version($"{majorVersion}.0");
            var latestSupportedVersion = new Version(latestVersion);

            if (requestedVersion.Major > latestSupportedVersion.Major)
            {
                // The requested major version is greater than the latest major supported version.
                var errorMsg = string.Format(PowerShellWorkerStrings.InvalidModuleMajorVersion, moduleName, majorVersion);
                throw new ArgumentException(errorMsg);
            }
        }

        /// <summary>
        /// Parses the given string version and extracts the major version.
        /// Please note that the only version we currently support is of the form '1.*'.
        /// </summary>
        private string GetMajorVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.DependencyPropertyIsNullOrEmpty, "version");
                throw new ArgumentException(errorMessage);
            }

            // Validate that version is in the correct format: 'MajorVersion.*'
            if (!IsValidVersionFormat(version))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.InvalidVersionFormat, "MajorVersion.*");
                throw new ArgumentException(errorMessage);
            }

            // Return the major version.
            return version.Split(".")[0];
        }

        /// <summary>
        /// Parses the given PowerShell (psd1) data file.
        /// Returns a Hashtable representing the key value pairs.
        /// </summary>
        private Hashtable ParsePowerShellDataFile(string functionAppRootPath, string fileName)
        {
            // Path to requirements.psd1 file.
            var requirementsFilePath = Path.Join(functionAppRootPath, fileName);

            if (!File.Exists(requirementsFilePath))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.FileNotFound, fileName, functionAppRootPath);
                throw new ArgumentException(errorMessage);
            }

            // Try to parse the requirements.psd1 file.
            var ast = Parser.ParseFile(requirementsFilePath, out _, out ParseError[] errors);

            if (errors?.Length > 0)
            {
                var stringBuilder = new StringBuilder();
                foreach (var error in errors)
                {
                    stringBuilder.AppendLine(error.Message);
                }

                string errorMsg = stringBuilder.ToString();
                throw new ArgumentException(string.Format(PowerShellWorkerStrings.FailToParseScript, RequirementsPsd1FileName, errorMsg));
            }

            Ast hashtableAst = ast.Find(x => x is HashtableAst, false);
            Hashtable hashtable = hashtableAst?.SafeGetValue() as Hashtable;

            if (hashtable == null)
            {
                string errorMsg = string.Format(PowerShellWorkerStrings.InvalidPowerShellDataFile, RequirementsPsd1FileName);
                throw new ArgumentException(errorMsg);
            }

            return hashtable;
        }

        /// <summary>
        /// Validates the given version format. Currently, we only support 'Number.*'.
        /// </summary>
        private bool IsValidVersionFormat(string version)
        {
            var pattern = @"^(\d){1,2}(\.)(\*)";
            return Regex.IsMatch(version, pattern);
        }

        /// <summary>
        /// Validate that the module name is not null or empty,
        /// and ensure that the module is a supported dependency.
        /// </summary>
        private void ValidateModuleName(string name)
        {
            // Validate the name property.
            if (string.IsNullOrEmpty(name))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.DependencyPropertyIsNullOrEmpty, "name");
                throw new ArgumentException(errorMessage);
            }

            // If this is not a supported module, error out.
            if (!SupportedManagedDependencies.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.ManagedDependencyNotSupported, name);
                throw new ArgumentException(errorMessage);
            }
        }

        /// <summary>
        /// Gets the Managed Dependencies folder path.
        /// If we are running in Azure, the path is HOME\data\ManagedDependencies.
        /// Otherwise, the path is LocalApplicationData\AzureFunctions\FunctionAppName\ManagedDependencies.
        /// </summary>
        private string GetManagedDependenciesPath(string functionAppRootPath)
        {
            string managedDependenciesFolderPath = null;

            // If we are running in Azure use the 'HOME\Data' path.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AzureWebsiteInstanceId)))
            {
                var homeDriveVariable = Environment.GetEnvironmentVariable(HomeDriveName);
                if (string.IsNullOrEmpty(homeDriveVariable))
                {
                    var errorMsg = string.Format(PowerShellWorkerStrings.FailToResolveHomeDirectory, HomeDriveName);
                    throw new ArgumentException(errorMsg);
                }

                managedDependenciesFolderPath = Path.Combine(homeDriveVariable, DataFolderName, ManagedDependenciesFolderName);
            }
            else
            {
                // Otherwise, the ManagedDependencies folder is created under LocalApplicationData\AzureFunctions\FunctionAppName\ManagedDependencies.
                string functionAppName = Path.GetFileName(functionAppRootPath);
                string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
                managedDependenciesFolderPath = Path.Combine(appDataFolder, AzureFunctionsFolderName, functionAppName, ManagedDependenciesFolderName);
            }

            return managedDependenciesFolderPath;
        }

        #endregion
    }
}
