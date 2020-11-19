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

        private readonly IDependencyManagerStorage _storage;

        private readonly IInstalledDependenciesLocator _installedDependenciesLocator;

        private readonly IDependencySnapshotInstaller _installer;

        private readonly INewerDependencySnapshotDetector _newerSnapshotDetector;

        private readonly IBackgroundDependencySnapshotMaintainer _backgroundSnapshotMaintainer;

        private readonly IBackgroundDependencySnapshotContentLogger _currentSnapshotContentLogger;

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
            INewerDependencySnapshotDetector newerSnapshotDetector = null,
            IBackgroundDependencySnapshotMaintainer maintainer = null,
            IBackgroundDependencySnapshotContentLogger currentSnapshotContentLogger = null)
        {
            _storage = storage ?? new DependencyManagerStorage(GetFunctionAppRootPath(requestMetadataDirectory));
            _installedDependenciesLocator = installedDependenciesLocator ?? new InstalledDependenciesLocator(_storage);
            var snapshotContentLogger = new PowerShellModuleSnapshotLogger();
            _installer = installer ?? new DependencySnapshotInstaller(
                                            moduleProvider ?? new PowerShellGalleryModuleProvider(),
                                            _storage,
                                            new PowerShellModuleSnapshotComparer(),
                                            snapshotContentLogger);
            _newerSnapshotDetector = newerSnapshotDetector ?? new NewerDependencySnapshotDetector(_storage, new WorkerRestarter());
            _backgroundSnapshotMaintainer =
                maintainer ?? new BackgroundDependencySnapshotMaintainer(
                                    _storage,
                                    _installer,
                                    new DependencySnapshotPurger(_storage));
            _currentSnapshotContentLogger =
                currentSnapshotContentLogger ?? new BackgroundDependencySnapshotContentLogger(snapshotContentLogger);
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
                    logger.Log(isUserOnlyLog: true, LogLevel.Warning, PowerShellWorkerStrings.FunctionAppDoesNotHaveRequiredModulesToInstall);
                    return null;
                }

                _currentSnapshotPath = _installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled()
                                        ?? _storage.CreateNewSnapshotPath();

                _backgroundSnapshotMaintainer.Start(_currentSnapshotPath, _dependenciesFromManifest, logger);
                _newerSnapshotDetector.Start(_currentSnapshotPath, logger);
                _currentSnapshotContentLogger.Start(_currentSnapshotPath, logger);

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
            logger.Log(isUserOnlyLog: true, LogLevel.Warning, PowerShellWorkerStrings.DependencyDownloadInProgress);
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
            (_backgroundSnapshotMaintainer as IDisposable)?.Dispose();
            (_newerSnapshotDetector as IDisposable)?.Dispose();
            (_currentSnapshotContentLogger as IDisposable)?.Dispose();
            _dependencyInstallationTask?.Dispose();
        }

        /// <summary>
        /// Installs function app dependencies.
        /// </summary>
        internal Exception InstallFunctionAppDependencies(PowerShell firstPwsh, Func<PowerShell> pwshFactory, ILogger logger)
        {
            if (AreAcceptableDependenciesAlreadyInstalled())
            {
                logger.Log(
                    isUserOnlyLog: false,
                    RpcLog.Types.Level.Trace,
                    PowerShellWorkerStrings.AcceptableFunctionAppDependenciesAlreadyInstalled);

                // Background installation: can't use the firstPwsh runspace because it belongs
                // to the pool used to run functions code, so create a new runspace.
                _nextSnapshotPath = _backgroundSnapshotMaintainer.InstallAndPurgeSnapshots(pwshFactory, logger);
            }
            else
            {
                // Foreground installation: *may* use the firstPwsh runspace, since the function execution is
                // blocked until the installation is complete, so we are potentially saving some time by reusing
                // the runspace as opposed to creating another one.
                InstallSnapshotInForeground(firstPwsh, logger);
            }

            return _dependencyInstallationError;
        }

        #region Helper_Methods

        private void InstallSnapshotInForeground(PowerShell pwsh, ILogger logger)
        {
            try
            {
                _installer.InstallSnapshot(
                    _dependenciesFromManifest,
                    _currentSnapshotPath,
                    pwsh,
                    // As opposed to the background upgrade case, the worker does not have an acceptable
                    // snapshot to use yet, so the initial snapshot is *required* for this worker to run.
                    DependencySnapshotInstallationMode.Required,
                    logger);
            }
            catch (Exception e)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToInstallFuncAppDependencies, e.Message);
                _dependencyInstallationError = new DependencyInstallationException(errorMsg, e);
            }
        }

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

        private static string GetFunctionAppRootPath(string requestMetadataDirectory)
        {
            if (string.IsNullOrWhiteSpace(requestMetadataDirectory))
            {
                throw new ArgumentException("Empty request metadata directory path", nameof(requestMetadataDirectory));
            }

            return Path.GetFullPath(Path.Join(requestMetadataDirectory, ".."));
        }

        #endregion
    }
}
