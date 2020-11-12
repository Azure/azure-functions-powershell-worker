//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using System;
    using System.Management.Automation;

    using Moq;
    using Xunit;

    using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    public class BackgroundDependencySnapshotMaintainerTests
    {
        private readonly Mock<IDependencyManagerStorage> _mockStorage = new Mock<IDependencyManagerStorage>(MockBehavior.Strict);
        private readonly Mock<IDependencySnapshotInstaller> _mockInstaller = new Mock<IDependencySnapshotInstaller>(MockBehavior.Strict);
        private readonly Mock<IDependencySnapshotPurger> _mockPurger = new Mock<IDependencySnapshotPurger>();
        private readonly Mock<IDependencySnapshotContentLogger> _mockContentLogger = new Mock<IDependencySnapshotContentLogger>();
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        private readonly DependencyManifestEntry[] _dependencyManifest = new DependencyManifestEntry[0];

        readonly TimeSpan _minBackgroundUpgradePeriod = TimeSpan.FromHours(12);

        [Fact]
        public void SetsCurrentlyUsedSnapshotOnPurger()
        {
            using (var maintainer = CreateMaintainerWithMocks())
            {
                maintainer.Start("current snapshot", _dependencyManifest, _mockLogger.Object);
            }

            _mockPurger.Verify(_ => _.SetCurrentlyUsedSnapshot("current snapshot", _mockLogger.Object), Times.Once());
        }

        [Fact]
        public void InstallsSnapshotIfNoRecentlyInstalledSnapshotFound()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "older snapshot" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("older snapshot"))
                .Returns(DateTime.UtcNow - _minBackgroundUpgradePeriod - TimeSpan.FromSeconds(1));

            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("new snapshot path");

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<DependencyManifestEntry[]>(),
                        It.IsAny<string>(),
                        It.IsAny<PowerShell>(),
                        It.IsAny<DependencySnapshotInstallationMode>(),
                        It.IsAny<ILogger>()));

            using (var dummyPowerShell = PowerShell.Create())
            using (var maintainer = CreateMaintainerWithMocks(_minBackgroundUpgradePeriod))
            {
                const string CurrentSnapshotPath = "current snapshot";

                maintainer.Start(CurrentSnapshotPath, _dependencyManifest, _mockLogger.Object);

                // ReSharper disable once AccessToDisposedClosure
                var installedSnapshotPath = maintainer.InstallAndPurgeSnapshots(() => dummyPowerShell, _mockLogger.Object);
                Assert.Equal("new snapshot path", installedSnapshotPath);

                // ReSharper disable once AccessToDisposedClosure
                _mockInstaller.Verify(
                    _ => _.InstallSnapshot(_dependencyManifest, "new snapshot path", dummyPowerShell, DependencySnapshotInstallationMode.Optional, _mockLogger.Object),
                    Times.Once);

                _mockPurger.Verify(_ => _.Purge(_mockLogger.Object), Times.Exactly(2));
                _mockContentLogger.Verify(_ => _.LogDependencySnapshotContent(CurrentSnapshotPath, _mockLogger.Object), Times.Exactly(1));
            }
        }

        [Fact]
        public void DoesNotInstallSnapshotIfRecentlyInstalledSnapshotFound()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "older snapshot" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("older snapshot"))
                .Returns(DateTime.UtcNow - _minBackgroundUpgradePeriod + TimeSpan.FromSeconds(1));

            using (var maintainer = CreateMaintainerWithMocks(_minBackgroundUpgradePeriod))
            {
                const string CurrentSnapshotPath = "current snapshot";

                maintainer.Start(CurrentSnapshotPath, _dependencyManifest, _mockLogger.Object);

                var installedSnapshotPath = maintainer.InstallAndPurgeSnapshots(PowerShell.Create, _mockLogger.Object);
                Assert.Null(installedSnapshotPath);

                _mockPurger.Verify(_ => _.Purge(_mockLogger.Object), Times.Once);
                _mockContentLogger.Verify(_ => _.LogDependencySnapshotContent(CurrentSnapshotPath, _mockLogger.Object), Times.Exactly(1));
            }
        }

        [Fact]
        public void LogsWarningIfCannotInstallSnapshot()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "older snapshot" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("older snapshot"))
                .Returns(DateTime.UtcNow - _minBackgroundUpgradePeriod - TimeSpan.FromSeconds(1));

            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("new snapshot path");

            var injectedException = new Exception("Can't install snapshot");

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<DependencyManifestEntry[]>(),
                        It.IsAny<string>(),
                        It.IsAny<PowerShell>(),
                        It.IsAny<DependencySnapshotInstallationMode>(),
                        It.IsAny<ILogger>()))
                .Throws(injectedException);

            using (var dummyPowerShell = PowerShell.Create())
            using (var maintainer = CreateMaintainerWithMocks(_minBackgroundUpgradePeriod))
            {
                maintainer.Start("current snapshot", _dependencyManifest, _mockLogger.Object);

                // ReSharper disable once AccessToDisposedClosure
                maintainer.InstallAndPurgeSnapshots(() => dummyPowerShell, _mockLogger.Object);
            }

            _mockLogger.Verify(
                _ => _.Log(
                    false,
                    LogLevel.Warning,
                    It.Is<string>(message => message.Contains(injectedException.Message)),
                    It.IsAny<Exception>()),
                Times.Once);
        }

        private BackgroundDependencySnapshotMaintainer CreateMaintainerWithMocks(TimeSpan? minBackgroundUpgradePeriod = null)
        {
            var maintainer = new BackgroundDependencySnapshotMaintainer(
                                    _mockStorage.Object,
                                    _mockInstaller.Object,
                                    _mockPurger.Object,
                                    _mockContentLogger.Object);

            if (minBackgroundUpgradePeriod != null)
            {
                maintainer.MinBackgroundUpgradePeriod = minBackgroundUpgradePeriod.Value;
            }

            return maintainer;
        }
    }
}
