//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;

    using Moq;
    using Xunit;

    using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    public class DependencyManagerTests
    {
        private readonly Mock<IDependencyManagerStorage> _mockStorage = new Mock<IDependencyManagerStorage>(MockBehavior.Strict);
        private readonly Mock<IInstalledDependenciesLocator> _mockInstalledDependenciesLocator = new Mock<IInstalledDependenciesLocator>(MockBehavior.Strict);
        private readonly Mock<IDependencySnapshotInstaller> _mockInstaller = new Mock<IDependencySnapshotInstaller>(MockBehavior.Strict);
        private readonly Mock<IDependencySnapshotPurger> _mockPurger = new Mock<IDependencySnapshotPurger>();
        private readonly Mock<INewerDependencySnapshotDetector> _mockNewerDependencySnapshotDetector = new Mock<INewerDependencySnapshotDetector>();
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        [Fact]
        public void DoesNothingOnConstruction()
        {
            CreateDependencyManagerWithMocks();
        }

        [Fact]
        public void Initialize_ReturnsNewSnapshotPath_WhenNoAcceptableDependencyVersionsInstalled()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(default(string));
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");
            _mockPurger.Setup(_ => _.SetCurrentlyUsedSnapshot(It.IsAny<string>(), _mockLogger.Object));

            var dependencyManager = CreateDependencyManagerWithMocks();
            var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

            Assert.Equal("NewSnapshot", dependenciesPath);
            _mockPurger.Verify(_ => _.SetCurrentlyUsedSnapshot("NewSnapshot", _mockLogger.Object), Times.Once);
        }

        [Fact]
        public void Initialize_ReturnsExistingSnapshotPath_WhenAcceptableDependencyVersionsAlreadyInstalled()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns("InstalledSnapshot");
            _mockPurger.Setup(_ => _.SetCurrentlyUsedSnapshot(It.IsAny<string>(), _mockLogger.Object));

            var dependencyManager = CreateDependencyManagerWithMocks();
            var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

            Assert.Equal("InstalledSnapshot", dependenciesPath);
            _mockPurger.Verify(_ => _.SetCurrentlyUsedSnapshot("InstalledSnapshot", _mockLogger.Object), Times.Once);
        }

        [Fact]
        public void Initialize_DoesNotTryToCheckDependencies_WhenNoDependenciesInManifest()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(new DependencyManifestEntry[0]);

            var dependencyManager = CreateDependencyManagerWithMocks();
            var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

            Assert.Null(dependenciesPath);
            VerifyMessageLogged(LogLevel.Warning, PowerShellWorkerStrings.FunctionAppDoesNotHaveDependentModulesToInstall, expectedIsUserLog: true);
        }

        [Fact]
        public void Initialize_StartsNewerDependencySnapshotDetector()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns("InstalledSnapshot");

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);

            _mockNewerDependencySnapshotDetector.Verify(
                _ => _.Start("InstalledSnapshot", _mockLogger.Object));
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsNothing_WhenNoDependenciesInManifest()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(new DependencyManifestEntry[0]);

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);

            dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.False(hadToWait);
            _mockInstalledDependenciesLocator.VerifyNoOtherCalls();
            _mockInstaller.VerifyNoOtherCalls();
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsSnapshotInForeground_WhenNoAcceptableDependenciesInstalled()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(default(string));
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            var firstPowerShellRunspace = PowerShell.Create();

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<IEnumerable<DependencyManifestEntry>>(),
                        "NewSnapshot",
                        // Must run on the same runspace
                        It.Is<PowerShell>(powerShell => ReferenceEquals(firstPowerShellRunspace, powerShell)),
                        _mockLogger.Object));

            _mockStorage.Setup(_ => _.SnapshotExists("NewSnapshot")).Returns(false);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new string[0]);
            _mockPurger.Setup(_ => _.SetCurrentlyUsedSnapshot(It.IsAny<string>(), _mockLogger.Object));

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);
            dependencyManager.StartDependencyInstallationIfNeeded(firstPowerShellRunspace, PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.True(hadToWait);
            VerifyMessageLogged(LogLevel.Information, PowerShellWorkerStrings.DependencyDownloadInProgress, expectedIsUserLog: true);
            VerifyExactlyOneSnapshotInstalled();
            _mockPurger.Verify(_ => _.Purge(It.IsAny<ILogger>()), Times.Never);
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsSnapshotInBackground_WhenAcceptableDependenciesAlreadyInstalled()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns("AlreadyInstalled");
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            var firstPowerShellRunspace = PowerShell.Create();

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<IEnumerable<DependencyManifestEntry>>(),
                        "NewSnapshot",
                        // Must run on a separate runspace
                        It.Is<PowerShell>(powerShell => !ReferenceEquals(firstPowerShellRunspace, powerShell)),
                        _mockLogger.Object));

            _mockStorage.Setup(_ => _.SnapshotExists("AlreadyInstalled")).Returns(true);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "AlreadyInstalled" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("AlreadyInstalled"))
                .Returns(DateTime.UtcNow - TimeSpan.FromHours(25));
            _mockPurger.Setup(_ => _.SetCurrentlyUsedSnapshot(It.IsAny<string>(), _mockLogger.Object));

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);
            dependencyManager.StartDependencyInstallationIfNeeded(firstPowerShellRunspace, PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.False(hadToWait);
            Assert.Equal("NewSnapshot", dependencyManager.WaitForBackgroundDependencyInstallationTaskCompletion());
            VerifyExactlyOneSnapshotInstalled();
            _mockPurger.Verify(_ => _.Purge(_mockLogger.Object), Times.AtLeastOnce());
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_DoesNotInstallSnapshot_WhenAcceptableDependenciesInstalledAndAnyInstallationStartedRecently()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns("AlreadyInstalled");
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");
            _mockStorage.Setup(_ => _.SnapshotExists("AlreadyInstalled")).Returns(true);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "AlreadyInstalled", "InProgress" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("AlreadyInstalled"))
                .Returns(DateTime.UtcNow - TimeSpan.FromMinutes(30));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("InProgress"))
                .Returns(DateTime.UtcNow - TimeSpan.FromMinutes(1));
            _mockPurger.Setup(_ => _.SetCurrentlyUsedSnapshot(It.IsAny<string>(), _mockLogger.Object));

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);
            dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.Equal(!true, hadToWait);
            dependencyManager.WaitForBackgroundDependencyInstallationTaskCompletion();
            _mockInstaller.VerifyNoOtherCalls();
            _mockPurger.Verify(_ => _.Purge(_mockLogger.Object), Times.AtLeastOnce());
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsSnapshotInForeground_WhenNoAcceptableDependenciesInstalledAndAnyInstallationStartedRecently()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(default(string));
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");
            _mockPurger.Setup(_ => _.SetCurrentlyUsedSnapshot(It.IsAny<string>(), _mockLogger.Object));

            var firstPowerShellRunspace = PowerShell.Create();

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<IEnumerable<DependencyManifestEntry>>(),
                        "NewSnapshot",
                        // Must run on the same runspace
                        It.Is<PowerShell>(powerShell => ReferenceEquals(firstPowerShellRunspace, powerShell)),
                        _mockLogger.Object));

            _mockStorage.Setup(_ => _.SnapshotExists("NewSnapshot")).Returns(false);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "NewSnapshot" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("NewSnapshot"))
                .Returns(DateTime.UtcNow - TimeSpan.FromMinutes(1));

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);
            dependencyManager.StartDependencyInstallationIfNeeded(firstPowerShellRunspace, PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.True(hadToWait);
            VerifyMessageLogged(LogLevel.Information, PowerShellWorkerStrings.DependencyDownloadInProgress, expectedIsUserLog: true);
            VerifyExactlyOneSnapshotInstalled();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StartDependencyInstallationIfNeeded_HandlesExceptionThrownBy_InstallDependenciesSnapshot(
            bool isAcceptableDependenciesSnapshotAlreadyInstalled)
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(isAcceptableDependenciesSnapshotAlreadyInstalled ? "AlreadyInstalled" : null);

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");
            _mockPurger.Setup(_ => _.SetCurrentlyUsedSnapshot(It.IsAny<string>(), _mockLogger.Object));

            var dependencyManager = CreateDependencyManagerWithMocks();
            var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new string[0]);

            var injectedException = new Exception("Can't install dependencies");

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<IEnumerable<DependencyManifestEntry>>(), It.IsAny<string>(), It.IsAny<PowerShell>(), It.IsAny<ILogger>()))
                .Throws(injectedException);

            _mockStorage.Setup(_ => _.SnapshotExists(dependenciesPath))
                .Returns(isAcceptableDependenciesSnapshotAlreadyInstalled);

            dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);

            if (isAcceptableDependenciesSnapshotAlreadyInstalled)
            {
                // Don't expect any exception, as an acceptable version of dependencies
                // has been found and will be used.
                dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

                var caughtException =
                    Assert.Throws<DependencyInstallationException>(
                        () => dependencyManager.WaitForBackgroundDependencyInstallationTaskCompletion());

                VerifyMessageLogged(LogLevel.Trace, PowerShellWorkerStrings.AcceptableFunctionAppDependenciesAlreadyInstalled, expectedIsUserLog: false);

                Assert.Contains(injectedException.Message, caughtException.Message);
                VerifyMessageLogged(LogLevel.Warning, injectedException.Message, expectedIsUserLog: false);
            }
            else
            {
                var caughtException =
                    Assert.Throws<DependencyInstallationException>(
                        () => dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object));

                Assert.Contains(injectedException.Message, caughtException.Message);
                Assert.Contains("Fail to install FunctionApp dependencies", caughtException.Message);
            }
        }

        private static DependencyManifestEntry[] GetAnyNonEmptyDependencyManifestEntries()
        {
            return new[] { new DependencyManifestEntry("ModuleName", VersionSpecificationType.MajorVersion, "1") };
        }

        private void VerifyExactlyOneSnapshotInstalled()
        {
            _mockInstaller.Verify(
                _ => _.InstallSnapshot(
                    It.IsAny<IEnumerable<DependencyManifestEntry>>(), It.IsAny<string>(), It.IsAny<PowerShell>(), It.IsAny<ILogger>()),
                Times.Once());

            _mockInstaller.VerifyNoOtherCalls();
        }

        private void VerifyMessageLogged(LogLevel expectedLogLevel, string expectedMessage, bool expectedIsUserLog)
        {
            _mockLogger.Verify(
                _ => _.Log(
                        expectedIsUserLog,
                        expectedLogLevel,
                        It.Is<string>(message => message.Contains(expectedMessage)),
                        It.IsAny<Exception>()));
        }

        private DependencyManager CreateDependencyManagerWithMocks()
        {
            return new DependencyManager(
                requestMetadataDirectory: null,
                moduleProvider: null,
                storage: _mockStorage.Object,
                installedDependenciesLocator: _mockInstalledDependenciesLocator.Object,
                installer: _mockInstaller.Object,
                purger: _mockPurger.Object,
                newerSnapshotDetector: _mockNewerDependencySnapshotDetector.Object);
        }
    }
}
