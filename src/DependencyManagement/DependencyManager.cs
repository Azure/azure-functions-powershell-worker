//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System.Management.Automation;

    internal class DependencyManager : IDisposable
    {
        #region Private fields

        private static readonly TimeSpan s_minBackgroundUpgradePeriod = GetMinBackgroundUpgradePeriod();

        private readonly IDependencyManagerStorage _storage;

        private readonly IInstalledDependenciesLocator _installedDependenciesLocator;

        private readonly IDependencySnapshotInstaller _installer;

        private readonly IDependencySnapshotPurger _purger;

        private DependencyManifestEntry[] _dependenciesFromManifest;

        private string _currentSnapshotPath;

        private string _nextSnapshotPath;

        private Exception _dependencyInstallationError;

        private Task _dependencyInstallationTask;

        #endregion

        public DependencyManager(
            string requestMetadataDirectory = null,
            IModuleProvider moduleProvider = null,
            IDependencyManagerStorage storage = null,
            IInstalledDependenciesLocator installedDependenciesLocator = null,
            IDependencySnapshotInstaller installer = null,
            IDependencySnapshotPurger purger = null)
        {
            _storage = storage ?? new DependencyManagerStorage(GetFunctionAppRootPath(requestMetadataDirectory));
            _installedDependenciesLocator = installedDependenciesLocator ?? new InstalledDependenciesLocator(_storage);
            _installer = installer ?? new DependencySnapshotInstaller(moduleProvider ?? new PowerShellGalleryModuleProvider(), _storage);
            _purger = purger ?? new DependencySnapshotPurger(_storage);
        }

        /// <summary>
        /// Initializes the dependency manager:
        /// - Parses functionAppRoot\requirements.psd1 file and creates a list of dependencies to install.
        /// - Determines the snapshot directory path to use.
        /// </summary>
        /// <returns>
        /// The dependency snapshot path where all the required dependencies are installed
        /// or will be installed. This path can be added to PSModulePath.
        /// Returns null if managed dependencies are disabled or the manifest does not have any dependencies declared.
        /// </returns>
        public string Initialize(StreamingMessage request, ILogger logger)
        {
            if (!request.FunctionLoadRequest.ManagedDependencyEnabled)
            {
                return null;
            }

            return Initialize(logger);
        }

        internal string Initialize(ILogger logger)
        {
            try
            {
                // Parse and process the function app dependencies defined in the manifest.
                _dependenciesFromManifest = _storage.GetDependencies().ToArray();

                if (!_dependenciesFromManifest.Any())
                {
                    logger.Log(isUserOnlyLog: true, LogLevel.Warning, PowerShellWorkerStrings.FunctionAppDoesNotHaveDependentModulesToInstall);
                    return null;
                }

                _currentSnapshotPath = _installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled()
                                        ?? _storage.CreateNewSnapshotPath();

                _purger.SetCurrentlyUsedSnapshot(_currentSnapshotPath, logger);

                return _currentSnapshotPath;
            }
            catch (Exception e)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToInstallFuncAppDependencies, e.Message);
                throw new DependencyInstallationException(errorMsg, e);
            }
        }

        /// <summary>
        /// Start dependency installation if needed.
        /// firstPowerShell is the first PowerShell instance created in this process (which this is important for local debugging),
        /// and it _may_ be used to download modules.
        /// </summary>
        public void StartDependencyInstallationIfNeeded(StreamingMessage request, PowerShell firstPowerShell, ILogger logger)
        {
            if (!request.FunctionLoadRequest.ManagedDependencyEnabled)
            {
                return;
            }

            StartDependencyInstallationIfNeeded(firstPowerShell, Utils.NewPwshInstance, logger);
        }

        internal void StartDependencyInstallationIfNeeded(PowerShell firstPowerShell, Func<PowerShell> powerShellFactory, ILogger logger)
        {
            if (_dependenciesFromManifest.Length == 0)
            {
                return;
            }

            // Start dependency installation on a separate thread
            _dependencyInstallationTask = Task.Run(() => InstallFunctionAppDependencies(firstPowerShell, powerShellFactory, logger));
        }

        /// <summary>
        /// Waits for dependencies availability if necessary, returns immediately if
        /// the dependencies are already available.
        /// </summary>
        /// <returns>True if waiting for dependencies installation was required.</returns>
        public bool WaitForDependenciesAvailability(Func<ILogger> getLogger)
        {
            if (_dependencyInstallationTask == null || AreAcceptableDependenciesAlreadyInstalled())
            {
                return false;
            }

            var logger = getLogger();
            logger.Log(isUserOnlyLog: true, LogLevel.Information, PowerShellWorkerStrings.DependencyDownloadInProgress);
            WaitOnDependencyInstallationTask();
            return true;
        }

        /// <summary>
        /// For testing purposes only: wait for the background installation task completion.
        /// </summary>
        /// <returns>Returns the path for the new dependencies snapshot that the background task was installing.</returns>
        internal string WaitForBackgroundDependencyInstallationTaskCompletion()
        {
            if (_dependencyInstallationTask != null)
            {
                WaitOnDependencyInstallationTask();
            }

            return _nextSnapshotPath;
        }

        public void Dispose()
        {
            (_purger as IDisposable)?.Dispose();
            _dependencyInstallationTask?.Dispose();
        }

        /// <summary>
        /// Installs function app dependencies.
        /// </summary>
        internal Exception InstallFunctionAppDependencies(PowerShell firstPwsh, Func<PowerShell> pwshFactory, ILogger logger)
        {
            var isBackgroundInstallation = false;

            try
            {
                if (AreAcceptableDependenciesAlreadyInstalled())
                {
                    isBackgroundInstallation = true;

                    _nextSnapshotPath = _storage.CreateNewSnapshotPath();

                    // Purge before installing a new snapshot, as we may be able to free some space.
                    _purger.Purge(logger);

                    if (!IsAnyInstallationStartedRecently())
                    {
                        logger.Log(
                            isUserOnlyLog: false,
                            LogLevel.Trace,
                            PowerShellWorkerStrings.AcceptableFunctionAppDependenciesAlreadyInstalled);

                        // Background installation: can't use the firstPwsh runspace because it belongs
                        // to the pool used to run functions code, so create a new runspace.
                        using (var pwsh = pwshFactory())
                        {
                            _installer.InstallSnapshot(_dependenciesFromManifest, _nextSnapshotPath, pwsh, logger);
                        }

                        // Now that a new snapshot has been installed, there is a chance an old snapshot can be purged.
                        _purger.Purge(logger);
                    }
                }
                else
                {
                    // Foreground installation: *may* use the firstPwsh runspace, since the function execution is
                    // blocked until the installation is complete, so we are potentially saving some time by reusing
                    // the runspace as opposed to creating another one.
                    _installer.InstallSnapshot(_dependenciesFromManifest, _currentSnapshotPath, firstPwsh, logger);
                }
            }
            catch (Exception e)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToInstallFuncAppDependencies, e.Message);
                _dependencyInstallationError = new DependencyInstallationException(errorMsg, e);

                if (isBackgroundInstallation)
                {
                    var dependenciesNotUpdatedMessage =
                        string.Format(PowerShellWorkerStrings.DependenciesUpgradeSkippedMessage, _dependencyInstallationError.Message);

                    logger.Log(isUserOnlyLog: false, LogLevel.Warning, dependenciesNotUpdatedMessage, _dependencyInstallationError);
                }
            }

            return _dependencyInstallationError;
        }

        #region Helper_Methods

        private void WaitOnDependencyInstallationTask()
        {
            _dependencyInstallationTask.Wait();
            _dependencyInstallationTask = null;

            if (_dependencyInstallationError != null)
            {
                throw _dependencyInstallationError;
            }
        }

        private bool AreAcceptableDependenciesAlreadyInstalled()
        {
            return _storage.SnapshotExists(_currentSnapshotPath);
        }

        private bool IsAnyInstallationStartedRecently()
        {
            var threshold = DateTime.UtcNow - s_minBackgroundUpgradePeriod;

            return _storage
                .GetInstalledAndInstallingSnapshots()
                .Select(path => _storage.GetSnapshotCreationTimeUtc(path))
                .Any(creationTime => creationTime > threshold);
        }

        private static string GetFunctionAppRootPath(string requestMetadataDirectory)
        {
            if (string.IsNullOrWhiteSpace(requestMetadataDirectory))
            {
                throw new ArgumentException("Empty request metadata directory path", nameof(requestMetadataDirectory));
            }

            return Path.GetFullPath(Path.Join(requestMetadataDirectory, ".."));
        }

        private static TimeSpan GetMinBackgroundUpgradePeriod()
        {
            var value = Environment.GetEnvironmentVariable("MDMinBackgroundUpgradePeriod");
            return string.IsNullOrEmpty(value) ? TimeSpan.FromDays(1) : TimeSpan.Parse(value);
        }

        #endregion
    }
}
