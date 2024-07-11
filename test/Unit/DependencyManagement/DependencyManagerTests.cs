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
        private readonly Mock<INewerDependencySnapshotDetector> _mockNewerDependencySnapshotDetector = new Mock<INewerDependencySnapshotDetector>();
        private readonly Mock<IBackgroundDependencySnapshotMaintainer> _mockBackgroundDependencySnapshotMaintainer = new Mock<IBackgroundDependencySnapshotMaintainer>();
        private readonly Mock<IBackgroundDependencySnapshotContentLogger> _mockBackgroundDependencySnapshotContentLogger = new Mock<IBackgroundDependencySnapshotContentLogger>();
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        [Fact]
        public void DoesNothingOnConstruction()
        {
            CreateDependencyManagerWithMocks().Dispose();
            _mockBackgroundDependencySnapshotMaintainer.VerifyNoOtherCalls();
            _mockNewerDependencySnapshotDetector.VerifyNoOtherCalls();
        }

        [Fact]
        public void Initialize_ReturnsNewSnapshotPath_WhenNoAcceptableDependencyVersionsInstalled()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(default(string));
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            using (var dependencyManager = CreateDependencyManagerWithMocks())
            {
                var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);
                Assert.Equal("NewSnapshot", dependenciesPath);
            }
        }

        [Fact]
        public void Initialize_Throws_WhenDependenciesAreDefinedInRequirementsPsd1_OnLegion()
        {
            const string ContainerName = "CONTAINER_NAME";
            const string LegionServiceHost = "LEGION_SERVICE_HOST";
            const string AzureWebsiteInstanceId = "WEBSITE_INSTANCE_ID";

            try
            {
                Environment.SetEnvironmentVariable(AzureWebsiteInstanceId, null);
                Environment.SetEnvironmentVariable(ContainerName, "MY_CONTAINER_NAME");
                Environment.SetEnvironmentVariable(LegionServiceHost, "MY_LEGION_SERVICE_HOST");

                _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());

                using (var dependencyManager = CreateDependencyManagerWithMocks())
                {
                    var caughtException = Assert.Throws<DependencyInstallationException>(
                            () => dependencyManager.Initialize(_mockLogger.Object));

                    Assert.Contains("Managed Dependencies is not supported in Linux Consumption on Legion.", caughtException.Message);
                    Assert.Contains("https://aka.ms/functions-powershell-include-modules", caughtException.Message);
                }
            }

            finally
            {
                Environment.SetEnvironmentVariable(ContainerName, null);
                Environment.SetEnvironmentVariable(LegionServiceHost, null);
            }
        }

        [Fact]
        public void Initialize_NoDependenciesOnRequirementsPsd1_OnLegion_DoesNotThrow()
        {
            const string ContainerName = "CONTAINER_NAME";
            const string LegionServiceHost = "LEGION_SERVICE_HOST";
            const string AzureWebsiteInstanceId = "WEBSITE_INSTANCE_ID";

            try
            {
                Environment.SetEnvironmentVariable(AzureWebsiteInstanceId, null);
                Environment.SetEnvironmentVariable(ContainerName, "MY_CONTAINER_NAME");
                Environment.SetEnvironmentVariable(LegionServiceHost, "MY_LEGION_SERVICE_HOST");

                _mockStorage.Setup(_ => _.GetDependencies()).Returns(new DependencyManifestEntry[0]);

                using (var dependencyManager = CreateDependencyManagerWithMocks())
                {
                    var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

                    Assert.Null(dependenciesPath);
                    VerifyMessageLogged(LogLevel.Warning, PowerShellWorkerStrings.FunctionAppDoesNotHaveRequiredModulesToInstall, expectedIsUserLog: true);

                    _mockBackgroundDependencySnapshotMaintainer.VerifyNoOtherCalls();
                    _mockNewerDependencySnapshotDetector.VerifyNoOtherCalls();
                }
            }

            finally
            {
                Environment.SetEnvironmentVariable(ContainerName, null);
                Environment.SetEnvironmentVariable(LegionServiceHost, null);
            }
        }

        [Fact]
        public void Initialize_ReturnsExistingSnapshotPath_WhenAcceptableDependencyVersionsAlreadyInstalled()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns("InstalledSnapshot");

            using (var dependencyManager = CreateDependencyManagerWithMocks())
            {
                var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

                Assert.Equal("InstalledSnapshot", dependenciesPath);
            }
        }

        [Fact]
        public void Initialize_DoesNotTryToCheckOrMaintainDependencies_WhenNoDependenciesInManifest()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(new DependencyManifestEntry[0]);

            using (var dependencyManager = CreateDependencyManagerWithMocks())
            {
                var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

                Assert.Null(dependenciesPath);
                VerifyMessageLogged(LogLevel.Warning, PowerShellWorkerStrings.FunctionAppDoesNotHaveRequiredModulesToInstall, expectedIsUserLog: true);

                _mockBackgroundDependencySnapshotMaintainer.VerifyNoOtherCalls();
                _mockNewerDependencySnapshotDetector.VerifyNoOtherCalls();
            }
        }

        [Fact]
        public void Initialize_StartsBackgroundActivities()
        {
            var dependencyManifest = GetAnyNonEmptyDependencyManifestEntries();
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(dependencyManifest);
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns("InstalledSnapshot");

            using (var dependencyManager = CreateDependencyManagerWithMocks())
            {
                dependencyManager.Initialize(_mockLogger.Object);

                _mockBackgroundDependencySnapshotMaintainer.Verify(
                    _ => _.Start("InstalledSnapshot", dependencyManifest, _mockLogger.Object),
                    Times.Once);

                _mockNewerDependencySnapshotDetector.Verify(
                    _ => _.Start("InstalledSnapshot", _mockLogger.Object),
                    Times.Once);

                _mockBackgroundDependencySnapshotContentLogger.Verify(
                    _ => _.Start("InstalledSnapshot", _mockLogger.Object),
                    Times.Once);
            }
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsNothing_WhenNoDependenciesInManifest()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(new DependencyManifestEntry[0]);

            using (var dependencyManager = CreateDependencyManagerWithMocks())
            {
                dependencyManager.Initialize(_mockLogger.Object);

                dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);
                var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

                Assert.False(hadToWait);
                _mockInstalledDependenciesLocator.VerifyNoOtherCalls();
                _mockInstaller.VerifyNoOtherCalls();
            }
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
                        DependencySnapshotInstallationMode.Required,
                        _mockLogger.Object));

            _mockStorage.Setup(_ => _.SnapshotExists("NewSnapshot")).Returns(false);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new string[0]);

            using (var dependencyManager = CreateDependencyManagerWithMocks())
            {
                dependencyManager.Initialize(_mockLogger.Object);
                dependencyManager.StartDependencyInstallationIfNeeded(firstPowerShellRunspace, PowerShell.Create, _mockLogger.Object);
                var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

                Assert.True(hadToWait);
                VerifyMessageLogged(LogLevel.Warning, PowerShellWorkerStrings.DependencyDownloadInProgress, expectedIsUserLog: true);
                VerifyExactlyOneSnapshotInstalled();
            }
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InvokesBackgroundMaintainer_WhenAcceptableDependenciesAlreadyInstalled()
        {
            try
            {
                Environment.SetEnvironmentVariable("MDEnableAutomaticUpgrades", "true");

                _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                    .Returns("AlreadyInstalled");
                _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());

                var firstPowerShellRunspace = PowerShell.Create();
                Func<PowerShell> powerShellFactory = PowerShell.Create;

                _mockStorage.Setup(_ => _.SnapshotExists("AlreadyInstalled")).Returns(true);

                _mockBackgroundDependencySnapshotMaintainer.Setup(
                    _ => _.InstallAndPurgeSnapshots(It.IsAny<Func<PowerShell>>(), It.IsAny<ILogger>()))
                    .Returns("NewSnapshot");

                using (var dependencyManager = CreateDependencyManagerWithMocks())
                {
                    dependencyManager.Initialize(_mockLogger.Object);
                    dependencyManager.StartDependencyInstallationIfNeeded(firstPowerShellRunspace, powerShellFactory, _mockLogger.Object);
                    var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

                    Assert.False(hadToWait);
                    Assert.Equal("NewSnapshot", dependencyManager.WaitForBackgroundDependencyInstallationTaskCompletion());

                    _mockBackgroundDependencySnapshotMaintainer.Verify(
                        _ => _.InstallAndPurgeSnapshots(powerShellFactory, _mockLogger.Object),
                        Times.Once);
                }

                _mockLogger.Verify(
                    _ => _.Log(
                        false,
                        LogLevel.Trace,
                        It.Is<string>(message => message.Contains(PowerShellWorkerStrings.AcceptableFunctionAppDependenciesAlreadyInstalled)),
                        It.IsAny<Exception>()),
                    Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MDEnableAutomaticUpgrades", null);
            }
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsSnapshotInForeground_WhenNoAcceptableDependenciesInstalledEvenIfAnyInstallationStartedRecently()
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
                        DependencySnapshotInstallationMode.Required,
                        _mockLogger.Object));

            _mockStorage.Setup(_ => _.SnapshotExists("NewSnapshot")).Returns(false);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "NewSnapshot" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("NewSnapshot"))
                .Returns(DateTime.UtcNow - TimeSpan.FromMinutes(1));

            using (var dependencyManager = CreateDependencyManagerWithMocks())
            {
                dependencyManager.Initialize(_mockLogger.Object);
                dependencyManager.StartDependencyInstallationIfNeeded(firstPowerShellRunspace, PowerShell.Create, _mockLogger.Object);
                var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

                Assert.True(hadToWait);
                VerifyMessageLogged(LogLevel.Warning, PowerShellWorkerStrings.DependencyDownloadInProgress, expectedIsUserLog: true);
                VerifyExactlyOneSnapshotInstalled();
            }
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_HandlesExceptionThrownBy_InstallDependenciesSnapshot()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(default(string));

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            using (var dependencyManager = CreateDependencyManagerWithMocks())
            {
                var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

                _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new string[0]);

                var injectedException = new Exception("Can't install dependencies");

                _mockInstaller.Setup(
                    _ => _.InstallSnapshot(
                            It.IsAny<IEnumerable<DependencyManifestEntry>>(),
                            It.IsAny<string>(),
                            It.IsAny<PowerShell>(),
                            It.IsAny<DependencySnapshotInstallationMode>(),
                            It.IsAny<ILogger>()))
                    .Throws(injectedException);

                _mockStorage.Setup(_ => _.SnapshotExists(dependenciesPath)).Returns(false);

                dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);

                var caughtException =
                    Assert.Throws<DependencyInstallationException>(
                        () => dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object));

                Assert.Contains(injectedException.Message, caughtException.Message);
                Assert.Contains("Failed to install function app dependencies", caughtException.Message);
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
                    It.IsAny<IEnumerable<DependencyManifestEntry>>(),
                    It.IsAny<string>(),
                    It.IsAny<PowerShell>(),
                    It.IsAny<DependencySnapshotInstallationMode>(),
                    It.IsAny<ILogger>()),
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
                newerSnapshotDetector: _mockNewerDependencySnapshotDetector.Object,
                maintainer: _mockBackgroundDependencySnapshotMaintainer.Object,
                currentSnapshotContentLogger: _mockBackgroundDependencySnapshotContentLogger.Object,
                logger: _mockLogger.Object);
        }
    }
}
