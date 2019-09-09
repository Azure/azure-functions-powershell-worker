//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using Moq;
    using Xunit;

    using PowerShellWorker.DependencyManagement;
    using Utility;

    public class NewerDependencySnapshotDetectorTests
    {
        private readonly Mock<IDependencyManagerStorage> _mockStorage = new Mock<IDependencyManagerStorage>(MockBehavior.Strict);
        private readonly Mock<IWorkerRestarter> _mockWorkerRestarter = new Mock<IWorkerRestarter>(MockBehavior.Strict);
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        [Fact]
        public void DoesNotAccessStorageOnConstruction()
        {
            using (CreateNewerDependencySnapshotDetector())
            {
            }

            _mockStorage.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("snapshot", null)]
        [InlineData("snapshot 1", "snapshot 1")]
        [InlineData("snapshot 2", "snapshot 1")]
        public void DoesNotRestartWorker_WhenCurrentSnapshotIsNotOlderThanLatest(
            string currentlyUsedSnapshot, string latestInstalledSnapshot)
        {
            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns(latestInstalledSnapshot);

            using (var detector = CreateNewerDependencySnapshotDetector())
            {
                detector.CheckForNewerDependencySnapshot(currentlyUsedSnapshot, _mockLogger.Object);
            }

            _mockWorkerRestarter.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("snapshot 1", "snapshot 2")]
        public void RestartsWorker_WhenCurrentSnapshotIsOlderThanLatest(
            string currentlyUsedSnapshot, string latestInstalledSnapshot)
        {
            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns(latestInstalledSnapshot);
            _mockWorkerRestarter.Setup(_ => _.Restart(It.IsAny<ILogger>()));

            using (var detector = CreateNewerDependencySnapshotDetector())
            {
                detector.CheckForNewerDependencySnapshot(currentlyUsedSnapshot, _mockLogger.Object);
            }

            _mockWorkerRestarter.Verify(_ => _.Restart(_mockLogger.Object), Times.Once);
        }

        private NewerDependencySnapshotDetector CreateNewerDependencySnapshotDetector()
        {
            return new NewerDependencySnapshotDetector(_mockStorage.Object, _mockWorkerRestarter.Object);
        }
    }
}
