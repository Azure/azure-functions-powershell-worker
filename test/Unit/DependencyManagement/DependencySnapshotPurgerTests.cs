//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using System;
    using System.IO;
    using Moq;
    using Xunit;

    using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    public class DependencySnapshotPurgerTests
    {
        private readonly Mock<IDependencyManagerStorage> _mockStorage = new Mock<IDependencyManagerStorage>(MockBehavior.Strict);
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        [Fact]
        public void DoesNotRemoveAnythingIfNoSnapshots()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new string[0]);

            using (var purger = new DependencySnapshotPurger(_mockStorage.Object))
            {
                purger.Purge(_mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.RemoveSnapshot(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void RemovesSnapshotNotUsedForLongTime()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "snapshot" });
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("snapshot")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(16));
            _mockStorage.Setup(_ => _.RemoveSnapshot("snapshot"));

            using (var purger = new DependencySnapshotPurger(
                                        _mockStorage.Object,
                                        heartbeatPeriod: TimeSpan.FromMinutes(10),
                                        oldHeartbeatAgeMargin: TimeSpan.FromMinutes(5),
                                        minNumberOfSnapshotsToKeep: 0))
            {
                purger.Purge(_mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.RemoveSnapshot(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void DoesNotRemoveRecentlyUsedSnapshot()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "snapshot" });
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("snapshot")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(14));

            using (var purger = new DependencySnapshotPurger(
                                        _mockStorage.Object,
                                        heartbeatPeriod: TimeSpan.FromMinutes(10),
                                        oldHeartbeatAgeMargin: TimeSpan.FromMinutes(5),
                                        minNumberOfSnapshotsToKeep: 0))
            {
                purger.Purge(_mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.RemoveSnapshot(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void RemovesMultipleSnapshotNotUsedForLongTime()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots())
                .Returns(new[] { "1", "2", "3", "4", "5", "6" });
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("1")).Returns(DateTime.UtcNow);
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("2")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(300));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("3")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(89));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("4")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(1));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("5")).Returns(DateTime.UtcNow + TimeSpan.FromMinutes(1));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("6")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(91));

            _mockStorage.Setup(_ => _.RemoveSnapshot("2"));
            _mockStorage.Setup(_ => _.RemoveSnapshot("6"));

            using (var purger = new DependencySnapshotPurger(
                                        _mockStorage.Object,
                                        heartbeatPeriod: TimeSpan.FromMinutes(60),
                                        oldHeartbeatAgeMargin: TimeSpan.FromMinutes(30),
                                        minNumberOfSnapshotsToKeep: 2))
            {
                purger.Purge(_mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.RemoveSnapshot("1"), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot("2"), Times.Once);
            _mockStorage.Verify(_ => _.RemoveSnapshot("3"), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot("4"), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot("5"), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot("6"), Times.Once);
        }

        [Fact]
        public void KeepMinNumberOfNewestSnapshotsEvenIfTheyAreOld()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots())
                .Returns(new[] { "1", "2", "3", "4", "5" });
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("1")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(600)); // keep
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("2")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(700));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("3")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(91));  // keep
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("4")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(800));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("5")).Returns(DateTime.UtcNow - TimeSpan.FromDays(365));

            // Don't delete 1 and 3 because they are the newest
            _mockStorage.Setup(_ => _.RemoveSnapshot("2"));
            _mockStorage.Setup(_ => _.RemoveSnapshot("4"));
            _mockStorage.Setup(_ => _.RemoveSnapshot("5"));

            using (var purger = new DependencySnapshotPurger(
                                        _mockStorage.Object,
                                        heartbeatPeriod: TimeSpan.FromMinutes(60),
                                        oldHeartbeatAgeMargin: TimeSpan.FromMinutes(30),
                                        minNumberOfSnapshotsToKeep: 2))
            {
                purger.Purge(_mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.RemoveSnapshot("1"), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot("2"), Times.Once);
            _mockStorage.Verify(_ => _.RemoveSnapshot("3"), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot("4"), Times.Once);
            _mockStorage.Verify(_ => _.RemoveSnapshot("5"), Times.Once);
        }

        [Fact]
        public void DoesNotRemoveAnythingIfGetInstalledAndInstallingSnapshotsThrows()
        {
            var injectedException = new Exception("Can't get snapshots");
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots())
                .Throws(injectedException);

            using (var purger = new DependencySnapshotPurger(_mockStorage.Object))
            {
                var thrownException = Assert.Throws<Exception>(() => purger.Purge(_mockLogger.Object));
                Assert.Same(injectedException, thrownException);
            }

            _mockStorage.Verify(_ => _.RemoveSnapshot(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void KeepsPurgingIfGetSnapshotAccessTimeUtcThrows()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots())
                .Returns(new[] { "1", "2", "3" });

            var injectedException = new IOException("Couldn't get access time");
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("1")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(11));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("2")).Throws(injectedException);
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("3")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(15));

            _mockStorage.Setup(_ => _.RemoveSnapshot("1"));
            _mockStorage.Setup(_ => _.RemoveSnapshot("3"));

            using (var purger = new DependencySnapshotPurger(
                                        _mockStorage.Object,
                                        heartbeatPeriod: TimeSpan.FromMinutes(10),
                                        oldHeartbeatAgeMargin: TimeSpan.FromMinutes(0),
                                        minNumberOfSnapshotsToKeep: 0))
            {
                purger.Purge(_mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.RemoveSnapshot("1"), Times.Once);
            _mockStorage.Verify(_ => _.RemoveSnapshot("2"), Times.Never);
            _mockStorage.Verify(_ => _.RemoveSnapshot("3"), Times.Once);

            _mockLogger.Verify(
                _ => _.Log(
                    LogLevel.Warning,
                    It.Is<string>(message => message.Contains("Failed to retrieve dependencies folder '2' access time")
                                             && message.Contains(injectedException.Message)),
                    injectedException,
                    true),
                Times.Once);
        }

        [Fact]
        public void KeepsPurgingIfRemoveSnapshotThrows()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots())
                .Returns(new[] { "1", "2", "3", "4", "5" });
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("1")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(11));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("2")).Returns(DateTime.UtcNow);
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("3")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(6));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("4")).Returns(DateTime.UtcNow + TimeSpan.FromMinutes(1));
            _mockStorage.Setup(_ => _.GetSnapshotAccessTimeUtc("5")).Returns(DateTime.UtcNow - TimeSpan.FromMinutes(12));

            var injectedException = new IOException("Couldn't delete directory");
            _mockStorage.Setup(_ => _.RemoveSnapshot("1")).Throws(injectedException);
            _mockStorage.Setup(_ => _.RemoveSnapshot("5"));

            using (var purger = new DependencySnapshotPurger(
                                        _mockStorage.Object,
                                        heartbeatPeriod: TimeSpan.FromMinutes(10),
                                        oldHeartbeatAgeMargin: TimeSpan.FromMinutes(0),
                                        minNumberOfSnapshotsToKeep: 0))
            {
                purger.Purge(_mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.RemoveSnapshot("1"), Times.Once);
            _mockStorage.Verify(_ => _.RemoveSnapshot("5"), Times.Once);

            _mockLogger.Verify(
                _ => _.Log(
                    LogLevel.Warning,
                    It.Is<string>(message => message.Contains("Failed to remove old dependencies folder '1'")
                                             && message.Contains(injectedException.Message)),
                    injectedException,
                    true),
                Times.Once);
        }

        [Fact]
        public void Heartbeat_UpdatesCurrentSnapshotAccessTime_WhenSnapshotExists()
        {
            _mockStorage.Setup(_ => _.SnapshotExists("Current")).Returns(true);
            _mockStorage.Setup(_ => _.SetSnapshotAccessTimeToUtcNow("Current"));

            using (var purger = new DependencySnapshotPurger(_mockStorage.Object))
            {
                purger.Heartbeat("Current", _mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.SetSnapshotAccessTimeToUtcNow(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void Heartbeat_DoesNotUpdateCurrentSnapshotAccessTime_WhenSnapshotDoesNotExist()
        {
            _mockStorage.Setup(_ => _.SnapshotExists("Current")).Returns(false);

            using (var purger = new DependencySnapshotPurger(_mockStorage.Object))
            {
                purger.Heartbeat("Current", _mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.SetSnapshotAccessTimeToUtcNow(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SetCurrentlyUsedSnapshot_UpdatesCurrentSnapshotAccessTimeImmediately()
        {
            _mockStorage.Setup(_ => _.SnapshotExists("Current")).Returns(true);
            _mockStorage.Setup(_ => _.SetSnapshotAccessTimeToUtcNow("Current"));

            using (var purger = new DependencySnapshotPurger(_mockStorage.Object))
            {
                purger.SetCurrentlyUsedSnapshot("Current", _mockLogger.Object);
            }

            _mockStorage.Verify(_ => _.SetSnapshotAccessTimeToUtcNow(It.IsAny<string>()), Times.Once);
        }
    }
}
