//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Threading;

    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class DependencySnapshotInstaller : IDependencySnapshotInstaller
    {
        // Maximum number of tries for retry logic when installing function app dependencies.
        private const int MaxNumberOfTries = 3;

        private readonly IModuleProvider _moduleProvider;

        private readonly IDependencyManagerStorage _storage;

        public DependencySnapshotInstaller(
            IModuleProvider moduleProvider,
            IDependencyManagerStorage storage)
        {
            _moduleProvider = moduleProvider ?? throw new ArgumentNullException(nameof(moduleProvider));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void InstallSnapshot(
            IEnumerable<DependencyManifestEntry> dependencies,
            string targetPath,
            PowerShell pwsh,
            ILogger logger)
        {
            logger.Log(isUserOnlyLog: false, LogLevel.Trace, PowerShellWorkerStrings.InstallingFunctionAppDependentModules);

            var installingPath = CreateInstallingSnapshot(targetPath);

            try
            {
                foreach (DependencyInfo module in GetLatestPublishedVersionsOfDependencies(dependencies))
                {
                    string moduleName = module.Name;
                    string latestVersion = module.LatestVersion;

                    logger.Log(isUserOnlyLog: false, LogLevel.Trace, string.Format(PowerShellWorkerStrings.StartedInstallingModule, moduleName, latestVersion));

                    int tries = 1;

                    while (true)
                    {
                        try
                        {
                            _moduleProvider.SaveModule(pwsh, moduleName, latestVersion, installingPath);

                            var message = string.Format(PowerShellWorkerStrings.ModuleHasBeenInstalled, moduleName, latestVersion);
                            logger.Log(isUserOnlyLog: false, LogLevel.Trace, message);

                            break;
                        }
                        catch (Exception e)
                        {
                            string currentAttempt = GetCurrentAttemptMessage(tries);
                            var errorMsg = string.Format(PowerShellWorkerStrings.FailToInstallModule, moduleName, latestVersion, currentAttempt, e.Message);
                            logger.Log(isUserOnlyLog: false, LogLevel.Error, errorMsg);

                            if (tries >= MaxNumberOfTries)
                            {
                                errorMsg = string.Format(PowerShellWorkerStrings.FailToInstallFuncAppDependencies, e.Message);
                                throw new DependencyInstallationException(errorMsg, e);
                            }
                        }

                        // Wait for 2^(tries-1) seconds between retries. In this case, it would be 1, 2, and 4 seconds, respectively.
                        var waitTimeSpan = TimeSpan.FromSeconds(Math.Pow(2, tries - 1));
                        Thread.Sleep(waitTimeSpan);

                        tries++;
                    }
                }

                _storage.PromoteInstallingSnapshotToInstalledAtomically(targetPath);
            }
            catch (Exception e)
            {
                var message = string.Format(PowerShellWorkerStrings.FailedToInstallDependenciesSnapshot, targetPath);
                logger.Log(isUserOnlyLog: false, LogLevel.Warning, message, e);
                _storage.RemoveSnapshot(installingPath);
                throw;
            }
            finally
            {
                _moduleProvider.Cleanup(pwsh);
            }
        }

        private string CreateInstallingSnapshot(string path)
        {
            try
            {
                return _storage.CreateInstallingSnapshot(path);
            }
            catch (Exception e)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToCreateFunctionAppDependenciesDestinationPath, path, e.Message);
                throw new InvalidOperationException(errorMsg);
            }
        }

        /// <summary>
        /// Returns the string representation of the given attempt number.
        /// </summary>
        internal static string GetCurrentAttemptMessage(int attempt)
        {
            switch (attempt)
            {
                case 1: return PowerShellWorkerStrings.FirstAttempt;
                case 2: return PowerShellWorkerStrings.SecondAttempt;
                case 3: return PowerShellWorkerStrings.ThirdAttempt;
                default:
                    throw new InvalidOperationException("Invalid attempt number. Unreachable code.");
            }
        }

        private List<DependencyInfo> GetLatestPublishedVersionsOfDependencies(
            IEnumerable<DependencyManifestEntry> dependencies)
        {
            var result = new List<DependencyInfo>();

            foreach (var entry in dependencies)
            {
                var latestVersion = GetModuleLatestPublishedVersion(entry.Name, entry.MajorVersion);

                var dependencyInfo = new DependencyInfo(entry.Name, latestVersion);
                result.Add(dependencyInfo);
            }

            return result;
        }

        /// <summary>
        /// Gets the latest published module version for the given module name and major version.
        /// </summary>
        private string GetModuleLatestPublishedVersion(string moduleName, string majorVersion)
        {
            string latestVersion = null;

            string errorDetails = null;
            bool throwException = false;

            try
            {
                latestVersion = _moduleProvider.GetLatestPublishedModuleVersion(moduleName, majorVersion);
            }
            catch (Exception e)
            {
                throwException = true;

                if (!string.IsNullOrEmpty(e.Message))
                {
                    errorDetails = string.Format(PowerShellWorkerStrings.ErrorDetails, e.Message);
                }
            }

            // If we could not find the latest module version error out.
            if (string.IsNullOrEmpty(latestVersion) || throwException)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToGetModuleLatestVersion, moduleName, majorVersion, errorDetails ?? string.Empty);
                throw new InvalidOperationException(errorMsg);
            }

            return latestVersion;
        }
    }
}
