//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;

    using Moq;
    using Xunit;

    using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    public class DependencySnapshotInstallerTests
    {
        private readonly Mock<IModuleProvider> _mockModuleProvider = new Mock<IModuleProvider>();
        private readonly Mock<IDependencyManagerStorage> _mockStorage = new Mock<IDependencyManagerStorage>(MockBehavior.Strict);
        private readonly Mock<IDependencySnapshotComparer> _mockSnapshotComparer = new Mock<IDependencySnapshotComparer>(MockBehavior.Strict);
        private readonly Mock<IDependencySnapshotContentLogger> _mockSnapshotContentLogger = new Mock<IDependencySnapshotContentLogger>();
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        private readonly string _targetPathInstalled;
        private readonly string _targetPathInstalling;

        public DependencySnapshotInstallerTests()
        {
            _targetPathInstalled = DependencySnapshotFolderNameTools.CreateUniqueName();
            _targetPathInstalling = DependencySnapshotFolderNameTools.ConvertInstalledToInstalling(_targetPathInstalled);
            _mockStorage.Setup(_ => _.CreateInstallingSnapshot(_targetPathInstalled)).Returns(_targetPathInstalling);
            _mockStorage.Setup(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled));
            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns(default(string));
        }

        [Fact]
        public void DoesNothingOnConstruction()
        {
            CreateDependenciesSnapshotInstallerWithMocks();

            _mockModuleProvider.VerifyNoOtherCalls();
            _mockStorage.VerifyNoOtherCalls();
            _mockLogger.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(DependencySnapshotInstallationMode.Required)]
        [InlineData(DependencySnapshotInstallationMode.Optional)]
        public void SavesSpecifiedVersion_WhenExactVersionIsSpecified(DependencySnapshotInstallationMode installationMode)
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Exact version") };

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), installationMode, _mockLogger.Object);

            _mockModuleProvider.Verify(
                _ => _.SaveModule(It.IsAny<PowerShell>(), "Module", "Exact version", _targetPathInstalling),
                Times.Once);

            _mockModuleProvider.Verify(
                _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Theory]
        [InlineData(DependencySnapshotInstallationMode.Required)]
        [InlineData(DependencySnapshotInstallationMode.Optional)]
        public void SavesLatestPublishedVersion_WhenMajorVersionIsSpecified(DependencySnapshotInstallationMode installationMode)
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.MajorVersion, "Major version") };

            _mockModuleProvider.Setup(
                    _ => _.GetLatestPublishedModuleVersion("Module", "Major version"))
                .Returns("Latest version");

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), installationMode, _mockLogger.Object);

            _mockModuleProvider.Verify(
                _ => _.SaveModule(It.IsAny<PowerShell>(), "Module", "Latest version", _targetPathInstalling),
                Times.Once);
        }

        [Theory]
        [InlineData(DependencySnapshotInstallationMode.Required)]
        [InlineData(DependencySnapshotInstallationMode.Optional)]
        public void PromotesInstallingSnapshotToInstalledIfSaveModuleDoesNotThrow(DependencySnapshotInstallationMode installationMode)
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Version") };

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), installationMode, _mockLogger.Object);

            _mockStorage.Verify(_ => _.CreateInstallingSnapshot(_targetPathInstalled), Times.Once);
            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled), Times.Once);
            _mockSnapshotContentLogger.Verify(_ => _.LogDependencySnapshotContent(_targetPathInstalled, _mockLogger.Object), Times.Once);
        }

        [Fact]
        public void PromotesInstallingSnapshotToInstalledIfNotEquivalentToLatest()
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Version") };

            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns("snapshot");
            _mockSnapshotComparer.Setup(_ => _.AreEquivalent(_targetPathInstalling, "snapshot", _mockLogger.Object)).Returns(false);

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), DependencySnapshotInstallationMode.Optional, logger: _mockLogger.Object);

            _mockStorage.Verify(_ => _.CreateInstallingSnapshot(_targetPathInstalled), Times.Once);
            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled), Times.Once);
            _mockSnapshotContentLogger.Verify(_ => _.LogDependencySnapshotContent(_targetPathInstalled, _mockLogger.Object), Times.Once);
        }

        [Fact]
        public void DoesNotLookForEquivalentSnapshotsInRequiredInstallationMode()
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Version") };

            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns("snapshot");

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), DependencySnapshotInstallationMode.Required, logger: _mockLogger.Object);

            _mockSnapshotComparer.VerifyNoOtherCalls();
            _mockStorage.Verify(_ => _.CreateInstallingSnapshot(_targetPathInstalled), Times.Once);
            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled), Times.Once);
            _mockSnapshotContentLogger.Verify(_ => _.LogDependencySnapshotContent(_targetPathInstalled, _mockLogger.Object), Times.Once);
        }

        [Theory]
        [InlineData(DependencySnapshotInstallationMode.Required)]
        [InlineData(DependencySnapshotInstallationMode.Optional)]
        public void CleansUpPowerShellRunspaceAfterSuccessfullySavingModule(DependencySnapshotInstallationMode installationMode)
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Version") };

            var dummyPowerShell = PowerShell.Create();

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, dummyPowerShell, installationMode, _mockLogger.Object);

            _mockModuleProvider.Verify(_ => _.Cleanup(dummyPowerShell), Times.Once);
        }

        [Theory]
        [InlineData(DependencySnapshotInstallationMode.Required)]
        [InlineData(DependencySnapshotInstallationMode.Optional)]
        public void LogsInstallationStartAndFinish(DependencySnapshotInstallationMode installationMode)
        {
            var manifestEntries =
                new[]
                {
                    new DependencyManifestEntry("A", VersionSpecificationType.ExactVersion, "Exact A version"),
                    new DependencyManifestEntry("B", VersionSpecificationType.MajorVersion, "Major B version")
                };

            _mockModuleProvider.Setup(
                    _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("Exact B version");

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), installationMode, _mockLogger.Object);

            VerifyLoggedOnce(new[] { "Started installing", "A", "Exact A version" });
            VerifyLoggedOnce(new[] { "has been installed", "A", "Exact A version" });
            VerifyLoggedOnce(new[] { "Started installing", "B", "Exact B version" });
            VerifyLoggedOnce(new[] { "has been installed", "B", "Exact B version" });
        }

        [Theory]
        [InlineData(DependencySnapshotInstallationMode.Required)]
        [InlineData(DependencySnapshotInstallationMode.Optional)]
        public void DoesNotSaveModuleIfGetLatestPublishedModuleVersionThrows(DependencySnapshotInstallationMode installationMode)
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.MajorVersion, "Version") };

            var dummyPowerShell = PowerShell.Create();

            var injectedException = new InvalidOperationException("Couldn't get latest published module version");

            _mockModuleProvider.Setup(
                    _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(injectedException);

            _mockStorage.Setup(_ => _.RemoveSnapshot(_targetPathInstalling));

            _mockModuleProvider.Setup(_ => _.Cleanup(dummyPowerShell));

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            var caughtException = Assert.Throws<InvalidOperationException>(
                () => installer.InstallSnapshot(manifestEntries, _targetPathInstalled, dummyPowerShell, installationMode, _mockLogger.Object));

            Assert.Contains(injectedException.Message, caughtException.Message);

            _mockModuleProvider.Verify(
                _ => _.SaveModule(It.IsAny<PowerShell>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);

            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(It.IsAny<string>()), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot(_targetPathInstalling), Times.Once);
            _mockModuleProvider.Verify(_ => _.Cleanup(dummyPowerShell), Times.Once);
        }

        [Theory]
        [InlineData(DependencySnapshotInstallationMode.Required)]
        [InlineData(DependencySnapshotInstallationMode.Optional)]
        public void DoesNotPromoteSnapshotIfSaveModuleKeepsThrowing(DependencySnapshotInstallationMode installationMode)
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Version") };

            var dummyPowerShell = PowerShell.Create();

            var injectedException = new Exception("Couldn't save module");

            _mockModuleProvider.Setup(
                    _ => _.SaveModule(It.IsAny<PowerShell>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(injectedException);

            _mockStorage.Setup(_ => _.RemoveSnapshot(_targetPathInstalling));

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            var thrownException = Assert.Throws<DependencyInstallationException>(
                () => installer.InstallSnapshot(manifestEntries, _targetPathInstalled, dummyPowerShell, installationMode, _mockLogger.Object));

            Assert.Contains(injectedException.Message, thrownException.Message);

            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(It.IsAny<string>()), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot(_targetPathInstalling), Times.Once);
            _mockModuleProvider.Verify(_ => _.Cleanup(dummyPowerShell), Times.Once);
        }

        [Fact]
        public void DoesNotPromoteSnapshotIfItIsEquivalentToLatest()
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Version") };

            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns("snapshot");
            _mockSnapshotComparer.Setup(_ => _.AreEquivalent(_targetPathInstalling, "snapshot", _mockLogger.Object)).Returns(true);
            _mockStorage.Setup(_ => _.RemoveSnapshot(_targetPathInstalling));
            _mockStorage.Setup(_ => _.SetSnapshotCreationTimeToUtcNow("snapshot"));

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), DependencySnapshotInstallationMode.Optional, logger: _mockLogger.Object);

            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(It.IsAny<string>()), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot(_targetPathInstalling), Times.Once);
            _mockStorage.Verify(_ => _.SetSnapshotCreationTimeToUtcNow("snapshot"), Times.Once);
            _mockSnapshotContentLogger.Verify(_ => _.LogDependencySnapshotContent(_targetPathInstalling, _mockLogger.Object), Times.Once);
        }

        private DependencySnapshotInstaller CreateDependenciesSnapshotInstallerWithMocks()
        {
            return new DependencySnapshotInstaller(
                _mockModuleProvider.Object,
                _mockStorage.Object,
                _mockSnapshotComparer.Object,
                _mockSnapshotContentLogger.Object);
        }

        private void VerifyLoggedOnce(IEnumerable<string> messageParts)
        {
            _mockLogger.Verify(
                _ => _.Log(
                    false, // isUserOnlyLog
                    LogLevel.Trace,
                    It.Is<string>( // the message should contain every item of messageParts
                        message => messageParts.All(part => message.Contains(part))),
                    null), // exception
                Times.Once);
        }
    }
}
