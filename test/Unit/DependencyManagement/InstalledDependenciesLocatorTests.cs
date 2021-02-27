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
                new DependencyManifestEntry("A", VersionSpecificationType.ExactVersion, "exact version of A"),
                new DependencyManifestEntry("B", VersionSpecificationType.MajorVersion, "major version of B")
            };

        [Fact]
        public void ReturnsLatestSnapshotPath_WhenAllDependenciesHaveAcceptableVersionInstalled()
        {
            // Even though multiple snapshots can be currently installed, only the latest one will be considered
            // (determined by name).
            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns("snapshot");

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(_dependencyManifestEntries);

            _mockStorage.Setup(_ => _.IsModuleVersionInstalled("snapshot", "A", "exact version of A")).Returns(true);
            _mockStorage.Setup(_ => _.GetInstalledModuleVersions("snapshot", "B", "major version of B")).Returns(new [] { "exact version of B" });

            var installedDependenciesLocator = new InstalledDependenciesLocator(_mockStorage.Object);
            var result = installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled();

            Assert.Equal("snapshot", result);
        }

        [Fact]
        public void ReturnsNull_WhenNoInstalledDependencySnapshotsFound()
        {
            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns(default(string));

            var installedDependenciesLocator = new InstalledDependenciesLocator(_mockStorage.Object);
            var result = installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled();

            Assert.Null(result);
        }

        [Fact]
        public void ReturnsNull_WhenNoMajorVersionInstalled()
        {
            // Even though multiple snapshots can be currently installed, only the latest one will be considered
            // (determined by name).
            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns("snapshot");

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(_dependencyManifestEntries);

            // No version for module B detected!
            _mockStorage.Setup(_ => _.IsModuleVersionInstalled("snapshot", "A", "exact version of A")).Returns(true);
            _mockStorage.Setup(_ => _.GetInstalledModuleVersions("snapshot", "B", "major version of B")).Returns(new string[0]);

            var installedDependenciesLocator = new InstalledDependenciesLocator(_mockStorage.Object);
            var result = installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled();

            Assert.Null(result);
        }

        [Fact]
        public void ReturnsNull_WhenExactModuleVersionIsNotInstalled()
        {
            // Even though multiple snapshots can be currently installed, only the latest one will be considered
            // (determined by name).
            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns("snapshot");

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(_dependencyManifestEntries);

            // The specified module A version is not installed
            _mockStorage.Setup(_ => _.IsModuleVersionInstalled("snapshot", "A", "exact version of A")).Returns(false);
            _mockStorage.Setup(_ => _.GetInstalledModuleVersions("snapshot", "B", "major version of B")).Returns(new [] { "exact version of B" });

            var installedDependenciesLocator = new InstalledDependenciesLocator(_mockStorage.Object);
            var result = installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled();

            Assert.Null(result);
        }

        // Any version with a postfix starting with '-' will be considered a preview version.
        // Preview versions may be installed into a folder with the base version name, without the postfix
        // (for example '4.0.2-preview' may be installed into a folder with the name '4.0.2'),
        // so we need to take this into account and look for both.
        [Theory]
        [InlineData("-preview")]
        [InlineData("-alfa")]
        [InlineData("-prerelease")]
        [InlineData("-anything")]
        public void ReturnsLatestSnapshotPath_WhenPreviewVersionInstalled(string postfix)
        {
            var baseVersion = "4.0.2";
            var fullVersion = baseVersion + postfix;
            
            DependencyManifestEntry[] dependencyManifestEntries =
            {
                new DependencyManifestEntry("A", VersionSpecificationType.ExactVersion, fullVersion)
            };

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(dependencyManifestEntries);

            _mockStorage.Setup(_ => _.GetLatestInstalledSnapshot()).Returns("snapshot");

            // No exact match...
            _mockStorage.Setup(_ => _.IsModuleVersionInstalled("snapshot", "A", fullVersion)).Returns(false);
            // ...but the base version is here
            _mockStorage.Setup(_ => _.IsModuleVersionInstalled("snapshot", "A", baseVersion)).Returns(true);

            var installedDependenciesLocator = new InstalledDependenciesLocator(_mockStorage.Object);
            var result = installedDependenciesLocator.GetPathWithAcceptableDependencyVersionsInstalled();

            Assert.Equal("snapshot", result);
        }
    }
}
