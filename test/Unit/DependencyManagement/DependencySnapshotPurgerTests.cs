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

    public class DependencySnapshotPurgerTests
    {
        private readonly Mock<IDependencyManagerStorage> _mockStorage = new Mock<IDependencyManagerStorage>(MockBehavior.Strict);
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        [Fact]
        public void DoesNotPurgeAnythingIfNoSnapshots()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new string[0]);

            var purger = new DependencySnapshotPurger(_mockStorage.Object);
            purger.Purge(_mockLogger.Object);
        }

        [Fact]
        public void PurgesOldDependencySnapshots()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots())
                .Returns(new[] { "1", "2", "3", "4", "5" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("1")).Returns(DateTime.Now - TimeSpan.FromDays(8));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("2")).Returns(DateTime.Now);
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("3")).Returns(DateTime.Now - TimeSpan.FromDays(6));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("4")).Returns(DateTime.Now + TimeSpan.FromMinutes(1));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("5")).Returns(DateTime.Now - TimeSpan.FromDays(9));

            _mockStorage.Setup(_ => _.RemoveSnapshot("1"));
            _mockStorage.Setup(_ => _.RemoveSnapshot("5"));

            var purger = new DependencySnapshotPurger(_mockStorage.Object);
            purger.Purge(_mockLogger.Object);

            _mockStorage.Verify(_ => _.RemoveSnapshot("1"), Times.Once);
            _mockStorage.Verify(_ => _.RemoveSnapshot("5"), Times.Once);
        }

        [Fact]
        public void KeepNewestSnapshotsEvenIfTheyAreOld()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots())
                .Returns(new[] { "1", "2", "3", "4", "5" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("1")).Returns(DateTime.Now - TimeSpan.FromDays(35)); // keep
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("2")).Returns(DateTime.Now - TimeSpan.FromDays(40));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("3")).Returns(DateTime.Now - TimeSpan.FromDays(8));  // keep
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("4")).Returns(DateTime.Now - TimeSpan.FromDays(42));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("5")).Returns(DateTime.Now - TimeSpan.FromDays(365));

            // Don't delete 1 and 3 because they are the newest
            _mockStorage.Setup(_ => _.RemoveSnapshot("2"));
            _mockStorage.Setup(_ => _.RemoveSnapshot("4"));
            _mockStorage.Setup(_ => _.RemoveSnapshot("5"));

            var purger = new DependencySnapshotPurger(_mockStorage.Object);
            purger.Purge(_mockLogger.Object);

            _mockStorage.Verify(_ => _.RemoveSnapshot("2"), Times.Once);
            _mockStorage.Verify(_ => _.RemoveSnapshot("4"), Times.Once);
            _mockStorage.Verify(_ => _.RemoveSnapshot("5"), Times.Once);
        }

        [Fact]
        public void KeepsPurgingIfDeleteDirectoryThrows()
        {
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots())
                .Returns(new[] { "1", "2", "3", "4", "5" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("1")).Returns(DateTime.Now - TimeSpan.FromDays(8));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("2")).Returns(DateTime.Now);
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("3")).Returns(DateTime.Now - TimeSpan.FromDays(6));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("4")).Returns(DateTime.Now + TimeSpan.FromMinutes(1));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("5")).Returns(DateTime.Now - TimeSpan.FromDays(9));

            _mockStorage.Setup(_ => _.RemoveSnapshot("1")).Throws(new IOException("Couldn't delete directory"));
            _mockStorage.Setup(_ => _.RemoveSnapshot("5"));

            var purger = new DependencySnapshotPurger(_mockStorage.Object);
            purger.Purge(_mockLogger.Object);

            _mockStorage.Verify(_ => _.RemoveSnapshot("1"), Times.Once);
            _mockStorage.Verify(_ => _.RemoveSnapshot("5"), Times.Once);
        }
    }
}
