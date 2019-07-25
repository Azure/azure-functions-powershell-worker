//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using Moq;
    using Xunit;

    using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;

    public class InstalledDependenciesLocatorTests
    {
        private readonly Mock<IDependencyManagerStorage> _mockStorage = new Mock<IDependencyManagerStorage>(MockBehavior.Strict);

        private readonly DependencyManifestEntry[] _dependencyManifestEntries =
            {
                new DependencyManifestEntry("A", VersionSpecificationType.ExactVersion, "3"),
                new DependencyManifestEntry("B", VersionSpecificationType.MajorVersion, "11")
            };

        [Fact]
        public void ReturnsNull_WhenNoInstalledDependencySnapshotsFound()
        {
            _mockStorage.Setup(_ => _.GetInstalledSnapshots()).Returns(new string[0]);

            var installedDependenciesLocator = new InstalledDependenciesLocator(_mockStorage.Object);
            var result = installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled();

            Assert.Null(result);
        }

        [Fact]
        public void ReturnsNull_WhenNoMajorVersionInstalled()
        {
            // Even though multiple snapshots can be currently installed, only the latest one will be considered
            // (determined by name).
            _mockStorage.Setup(_ => _.GetInstalledSnapshots()).Returns(new[] { "s1", "s3", "s2" });

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(_dependencyManifestEntries);

            // No 11.* version for module B detected!
            _mockStorage.Setup(_ => _.IsModuleVersionInstalled("s3", "A", "3")).Returns(true);
            _mockStorage.Setup(_ => _.GetInstalledModuleVersions("s3", "B", "11")).Returns(new string[0]);

            var installedDependenciesLocator = new InstalledDependenciesLocator(_mockStorage.Object);
            var result = installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled();

            Assert.Null(result);
        }

        [Fact]
        public void ReturnsNull_WhenExactModuleVersionIsNotInstalled()
        {
            // Even though multiple snapshots can be currently installed, only the latest one will be considered
            // (determined by name).
            _mockStorage.Setup(_ => _.GetInstalledSnapshots()).Returns(new[] { "s1", "s3", "s2" });

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(_dependencyManifestEntries);

            // The specified module A version is not installed
            _mockStorage.Setup(_ => _.IsModuleVersionInstalled("s3", "A", "3")).Returns(false);
            _mockStorage.Setup(_ => _.GetInstalledModuleVersions("s3", "B", "11")).Returns(new [] { "11.8.0.2" });

            var installedDependenciesLocator = new InstalledDependenciesLocator(_mockStorage.Object);
            var result = installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled();

            Assert.Null(result);
        }

        [Fact]
        public void ReturnsLatestSnapshotPath_WhenAllDependenciesHaveAcceptableVersionInstalled()
        {
            // Even though multiple snapshots can be currently installed, only the latest one will be considered
            // (determined by name).
            _mockStorage.Setup(_ => _.GetInstalledSnapshots()).Returns(new[] { "s1", "s3", "s2" });

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(_dependencyManifestEntries);

            _mockStorage.Setup(_ => _.IsModuleVersionInstalled("s3", "A", "3")).Returns(true);
            _mockStorage.Setup(_ => _.GetInstalledModuleVersions("s3", "B", "11")).Returns(new [] { "11.8.0.2" });

            var installedDependenciesLocator = new InstalledDependenciesLocator(_mockStorage.Object);
            var result = installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled();

            Assert.Equal("s3", result);
        }
    }
}
