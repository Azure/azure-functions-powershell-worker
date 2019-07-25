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

        private readonly IEnumerable<DependencyManifestEntry> _testDependencyManifestEntries =
            new[]
            {
                new DependencyManifestEntry("A", VersionSpecificationType.MajorVersion, "3"),
                new DependencyManifestEntry("C", VersionSpecificationType.ExactVersion, "7.0.1.3"),
                new DependencyManifestEntry("B", VersionSpecificationType.MajorVersion, "11")
            };

        private readonly Dictionary<string, string> _testLatestPublishedModuleVersions =
            new Dictionary<string, string>
            {
                { "A", "3.8.2" },
                { "B", "11.0" },
                { "C", "7.0.1.3" }
            };

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
        }

        [Fact]
        public void InstallsDependencySnapshots()
        {
            // Arrange

            var dummyPowerShell = PowerShell.Create();
            _mockStorage.Setup(_ => _.CreateInstallingSnapshot(_targetPathInstalled))
                .Returns(_targetPathInstalling);

            foreach (var entry in _testDependencyManifestEntries)
            {
                _mockModuleProvider.Setup(
                        _ => _.GetLatestPublishedModuleVersion(entry.Name, entry.VersionSpecification))
                    .Returns(_testLatestPublishedModuleVersions[entry.Name]);

                _mockModuleProvider.Setup(
                    _ => _.SaveModule(dummyPowerShell, entry.Name, _testLatestPublishedModuleVersions[entry.Name], _targetPathInstalling));
            }

            _mockStorage.Setup(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled));
            _mockModuleProvider.Setup(_ => _.Cleanup(dummyPowerShell));

            // Act

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(_testDependencyManifestEntries, _targetPathInstalled, dummyPowerShell, _mockLogger.Object);

            // Assert

            _mockStorage.Verify(_ => _.CreateInstallingSnapshot(_targetPathInstalled), Times.Once);

            foreach (var entry in _testDependencyManifestEntries)
            {
                _mockModuleProvider.Verify(
                    _ => _.SaveModule(dummyPowerShell, entry.Name, _testLatestPublishedModuleVersions[entry.Name], _targetPathInstalling),
                    Times.Once);
            }

            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled), Times.Once);
            _mockModuleProvider.Verify(_ => _.Cleanup(dummyPowerShell), Times.Once);
        }

        [Fact]
        public void DoesNotSaveModuleIfGetLatestPublishedModuleVersionThrows()
        {
            // Arrange

            var dummyPowerShell = PowerShell.Create();
            _mockStorage.Setup(_ => _.CreateInstallingSnapshot(_targetPathInstalled))
                .Returns(_targetPathInstalling);

            var injectedException = new InvalidOperationException("Couldn't get latest published module version");

            _mockModuleProvider.Setup(
                    _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(injectedException);

            _mockStorage.Setup(_ => _.RemoveSnapshot(_targetPathInstalling));

            _mockModuleProvider.Setup(_ => _.Cleanup(dummyPowerShell));

            // Act

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            var caughtException = Assert.Throws<InvalidOperationException>(
                () => installer.InstallSnapshot(_testDependencyManifestEntries, _targetPathInstalled, dummyPowerShell, _mockLogger.Object));

            // Assert

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
            // Arrange

            var dummyPowerShell = PowerShell.Create();
            _mockStorage.Setup(_ => _.CreateInstallingSnapshot(_targetPathInstalled))
                .Returns(_targetPathInstalling);

            var injectedException = new Exception("Couldn't save module");

            foreach (var entry in _testDependencyManifestEntries)
            {
                _mockModuleProvider.Setup(
                        _ => _.GetLatestPublishedModuleVersion(entry.Name, entry.VersionSpecification))
                    .Returns(_testLatestPublishedModuleVersions[entry.Name]);
            }

            _mockModuleProvider.Setup(
                _ => _.SaveModule(It.IsAny<PowerShell>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(injectedException);

            _mockStorage.Setup(_ => _.RemoveSnapshot(_targetPathInstalling));

            // Act

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            var thrownException = Assert.Throws<DependencyInstallationException>(
                () => installer.InstallSnapshot(_testDependencyManifestEntries, _targetPathInstalled, dummyPowerShell, _mockLogger.Object));

            // Assert

            Assert.Contains(injectedException.Message, thrownException.Message);

            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(It.IsAny<string>()), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot(_targetPathInstalling));
            _mockModuleProvider.Verify(_ => _.Cleanup(dummyPowerShell), Times.Once);
        }

        [Fact]
        public void InstallsLatestPublishedVersion_WhenMajorVersionIsSpecified()
        {
            // Arrange

            var manifestEntries =
                new[]
                {
                    new DependencyManifestEntry("Module", VersionSpecificationType.MajorVersion, "Major version")
                };

            _mockStorage.Setup(
                _ => _.CreateInstallingSnapshot(It.IsAny<string>())).Returns(_targetPathInstalling);

            _mockModuleProvider.Setup(
                _ => _.GetLatestPublishedModuleVersion("Module", "Major version"))
                .Returns("Latest version");

            _mockModuleProvider.Setup(
                _ => _.SaveModule(It.IsAny<PowerShell>(), "Module", "Latest version", _targetPathInstalling));

            _mockStorage.Setup(_ => _.PromoteInstallingSnapshotToInstalledAtomically(It.IsAny<string>()));

            // Act

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), _mockLogger.Object);

            // Assert

            _mockModuleProvider.Verify(
                _ => _.SaveModule(It.IsAny<PowerShell>(), "Module", "Latest version", _targetPathInstalling),
                Times.Once);
        }

        [Fact]
        public void InstallsSpecifiedVersion_WhenExactVersionIsSpecified()
        {
            var manifestEntries =
                new[]
                {
                    new DependencyManifestEntry("Module", VersionSpecificationType.ExactVersion, "Exact version")
                };

            // Arrange

            _mockStorage.Setup(
                _ => _.CreateInstallingSnapshot(It.IsAny<string>())).Returns(_targetPathInstalling);

            _mockModuleProvider.Setup(
                _ => _.SaveModule(It.IsAny<PowerShell>(), "Module", "Exact version", _targetPathInstalling));

            _mockStorage.Setup(_ => _.PromoteInstallingSnapshotToInstalledAtomically(It.IsAny<string>()));

            // Act

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), _mockLogger.Object);

            // Assert

            _mockModuleProvider.Verify(
                _ => _.SaveModule(It.IsAny<PowerShell>(), "Module", "Exact version", _targetPathInstalling),
                Times.Once);

            _mockModuleProvider.Verify(
                _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void LogsInstallationStartAndFinish()
        {
            // Arrange

            var manifestEntries =
                new[]
                {
                    new DependencyManifestEntry("A", VersionSpecificationType.ExactVersion, "Exact A version"),
                    new DependencyManifestEntry("B", VersionSpecificationType.MajorVersion, "Major B version")
                };

            _mockModuleProvider.Setup(
                    _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("Exact B version");

            _mockStorage.Setup(_ => _.CreateInstallingSnapshot(It.IsAny<string>())).Returns(_targetPathInstalling);
            _mockStorage.Setup(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled));

            // Act

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(manifestEntries, _targetPathInstalled, PowerShell.Create(), _mockLogger.Object);

            // Assert
            VerifyLoggedOnce(new[] { "Started installing", "A", "Exact A version" });
            VerifyLoggedOnce(new[] { "has been installed", "A", "Exact A version" });
            VerifyLoggedOnce(new[] { "Started installing", "B", "Exact B version" });
            VerifyLoggedOnce(new[] { "has been installed", "B", "Exact B version" });
        }

        private DependencySnapshotInstaller CreateDependenciesSnapshotInstallerWithMocks()
        {
            return new DependencySnapshotInstaller(_mockModuleProvider.Object, _mockStorage.Object);
        }

        private void VerifyLoggedOnce(IEnumerable<string> messageParts)
        {
            _mockLogger.Verify(
                _ => _.Log(
                    false,
                    LogLevel.Trace,
                    It.Is<string>(
                        message => messageParts.All(part => message.Contains(part))),
                    null),
                Times.Once);
        }
    }
}
