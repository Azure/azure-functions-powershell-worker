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
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        private readonly string _targetPathInstalled;
        private readonly string _targetPathInstalling;

        public DependencySnapshotInstallerTests()
        {
            _targetPathInstalled = DependencySnapshotFolderNameTools.CreateUniqueName();
            _targetPathInstalling = DependencySnapshotFolderNameTools.ConvertInstalledToInstalling(_targetPathInstalled);
        }

        [Fact]
        public void DoesNothingOnConstruction()
        {
            CreateDependenciesSnapshotInstallerWithMocks();

            _mockModuleProvider.VerifyNoOtherCalls();
            _mockStorage.VerifyNoOtherCalls();
            _mockLogger.VerifyNoOtherCalls();
        }

        [Fact]
        public void SavesSpecifiedVersion_WhenExactVersionIsSpecified()
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Exact version") };

            ExpectSnapshotCreationAndPromotion();

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), _mockLogger.Object);

            _mockModuleProvider.Verify(
                _ => _.SaveModule(It.IsAny<PowerShell>(), "Module", "Exact version", _targetPathInstalling),
                Times.Once);

            _mockModuleProvider.Verify(
                _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void SavesLatestPublishedVersion_WhenMajorVersionIsSpecified()
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.MajorVersion, "Major version") };

            ExpectSnapshotCreationAndPromotion();

            _mockModuleProvider.Setup(
                    _ => _.GetLatestPublishedModuleVersion("Module", "Major version"))
                .Returns("Latest version");

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), _mockLogger.Object);

            _mockModuleProvider.Verify(
                _ => _.SaveModule(It.IsAny<PowerShell>(), "Module", "Latest version", _targetPathInstalling),
                Times.Once);
        }

        [Fact]
        public void PromotesInstallingSnapshotToInstalledAfterSuccessfullySavingModule()
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Version") };

            ExpectSnapshotCreationAndPromotion();

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), _mockLogger.Object);

            _mockStorage.Verify(_ => _.CreateInstallingSnapshot(_targetPathInstalled), Times.Once);
            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled), Times.Once);
        }

        [Fact]
        public void CleansUpPowerShellRunspaceAfterSuccessfullySavingModule()
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Version") };

            ExpectSnapshotCreationAndPromotion();

            var dummyPowerShell = PowerShell.Create();

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, dummyPowerShell, _mockLogger.Object);

            _mockModuleProvider.Verify(_ => _.Cleanup(dummyPowerShell), Times.Once);
        }

        [Fact]
        public void LogsInstallationStartAndFinish()
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

            ExpectSnapshotCreationAndPromotion();

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), _mockLogger.Object);

            VerifyLoggedOnce(new[] { "Started installing", "A", "Exact A version" });
            VerifyLoggedOnce(new[] { "has been installed", "A", "Exact A version" });
            VerifyLoggedOnce(new[] { "Started installing", "B", "Exact B version" });
            VerifyLoggedOnce(new[] { "has been installed", "B", "Exact B version" });
        }

        [Fact]
        public void DoesNotSaveModuleIfGetLatestPublishedModuleVersionThrows()
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.MajorVersion, "Version") };

            ExpectSnapshotCreationAndPromotion();

            var dummyPowerShell = PowerShell.Create();

            var injectedException = new InvalidOperationException("Couldn't get latest published module version");

            _mockModuleProvider.Setup(
                    _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(injectedException);

            _mockStorage.Setup(_ => _.RemoveSnapshot(_targetPathInstalling));

            _mockModuleProvider.Setup(_ => _.Cleanup(dummyPowerShell));

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            var caughtException = Assert.Throws<InvalidOperationException>(
                () => installer.InstallSnapshot(manifestEntries, _targetPathInstalled, dummyPowerShell, _mockLogger.Object));

            Assert.Contains(injectedException.Message, caughtException.Message);

            _mockModuleProvider.Verify(
                _ => _.SaveModule(It.IsAny<PowerShell>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);

            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(It.IsAny<string>()), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot(_targetPathInstalling));
            _mockModuleProvider.Verify(_ => _.Cleanup(dummyPowerShell), Times.Once);
        }

        [Fact]
        public void DoesNotPromoteDependenciesSnapshotIfSaveModuleKeepsThrowing()
        {
            var manifestEntries =
                new[] { new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Version") };

            ExpectSnapshotCreationAndPromotion();

            var dummyPowerShell = PowerShell.Create();

            var injectedException = new Exception("Couldn't save module");

            _mockModuleProvider.Setup(
                    _ => _.SaveModule(It.IsAny<PowerShell>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(injectedException);

            _mockStorage.Setup(_ => _.RemoveSnapshot(_targetPathInstalling));

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            var thrownException = Assert.Throws<DependencyInstallationException>(
                () => installer.InstallSnapshot(manifestEntries, _targetPathInstalled, dummyPowerShell, _mockLogger.Object));

            Assert.Contains(injectedException.Message, thrownException.Message);

            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(It.IsAny<string>()), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot(_targetPathInstalling));
            _mockModuleProvider.Verify(_ => _.Cleanup(dummyPowerShell), Times.Once);
        }

        private DependencySnapshotInstaller CreateDependenciesSnapshotInstallerWithMocks()
        {
            return new DependencySnapshotInstaller(_mockModuleProvider.Object, _mockStorage.Object);
        }

        private void ExpectSnapshotCreationAndPromotion()
        {
            _mockStorage.Setup(_ => _.CreateInstallingSnapshot(_targetPathInstalled)).Returns(_targetPathInstalling);
            _mockStorage.Setup(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled));
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
