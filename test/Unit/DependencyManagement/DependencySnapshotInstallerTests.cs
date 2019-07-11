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

    public class DependencySnapshotInstallerTests
    {
        private readonly Mock<IModuleProvider> _mockModuleProvider = new Mock<IModuleProvider>(MockBehavior.Strict);
        private readonly Mock<IDependencyManagerStorage> _mockStorage = new Mock<IDependencyManagerStorage>(MockBehavior.Strict);
        private readonly Mock<IDependencySnapshotPurger> _mockPurger = new Mock<IDependencySnapshotPurger>(MockBehavior.Strict);
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        private readonly IEnumerable<DependencyManifestEntry> _testDependencyManifestEntries =
            new[]
            {
                new DependencyManifestEntry("A", "3"),
                new DependencyManifestEntry("C", "7"),
                new DependencyManifestEntry("B", "11")
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
                    _ => _.SaveModule(dummyPowerShell, entry.Name, _testLatestPublishedModuleVersions[entry.Name], _targetPathInstalling));
            }

            _mockStorage.Setup(_ => _.PromoteInstallingSnapshotToInstalledAtomically(_targetPathInstalled));
            _mockModuleProvider.Setup(_ => _.Cleanup(dummyPowerShell));
            _mockPurger.Setup(_ => _.Purge(_mockLogger.Object));

            var dependencyInfo = _testDependencyManifestEntries.Select(
                entry => new DependencyInfo(entry.Name, _testLatestPublishedModuleVersions[entry.Name]));

            // Act

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            installer.InstallSnapshot(dependencyInfo, _targetPathInstalled, dummyPowerShell, _mockLogger.Object);

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
            _mockPurger.Verify(_ => _.Purge(_mockLogger.Object), Times.Once);
        }

        [Fact]
        public void DoesNotPromoteDependenciesSnapshotIfSaveModuleKeepsThrowing()
        {
            // Arrange

            var dummyPowerShell = PowerShell.Create();
            _mockStorage.Setup(_ => _.CreateInstallingSnapshot(_targetPathInstalled))
                .Returns(_targetPathInstalling);

            var injectedException = new Exception("Couldn't save module");

            _mockModuleProvider.Setup(
                _ => _.SaveModule(It.IsAny<PowerShell>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(injectedException);

            _mockModuleProvider.Setup(_ => _.Cleanup(dummyPowerShell));
            _mockPurger.Setup(_ => _.Purge(_mockLogger.Object));

            var dependencyInfo = _testDependencyManifestEntries.Select(
                entry => new DependencyInfo(entry.Name, _testLatestPublishedModuleVersions[entry.Name]));

            // Act

            var installer = CreateDependenciesSnapshotInstallerWithMocks();
            var thrownException = Assert.Throws<DependencyInstallationException>(
                () => installer.InstallSnapshot(dependencyInfo, _targetPathInstalled, dummyPowerShell, _mockLogger.Object));

            // Assert

            Assert.Contains(injectedException.Message, thrownException.Message);

            _mockStorage.Verify(_ => _.PromoteInstallingSnapshotToInstalledAtomically(It.IsAny<string>()), Times.Never);
            _mockModuleProvider.Verify(_ => _.Cleanup(dummyPowerShell), Times.Once);
            _mockPurger.Verify(_ => _.Purge(_mockLogger.Object), Times.Once);
        }

        private DependencySnapshotInstaller CreateDependenciesSnapshotInstallerWithMocks()
        {
            return new DependencySnapshotInstaller(
                _mockModuleProvider.Object, _mockStorage.Object, _mockPurger.Object);
        }
    }
}
