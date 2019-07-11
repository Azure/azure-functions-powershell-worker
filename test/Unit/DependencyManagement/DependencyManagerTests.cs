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

    public class DependencyManagerTests
    {
        private readonly Mock<IModuleProvider> _mockModuleProvider = new Mock<IModuleProvider>(MockBehavior.Strict);
        private readonly Mock<IDependencyManagerStorage> _mockStorage = new Mock<IDependencyManagerStorage>(MockBehavior.Strict);
        private readonly Mock<IInstalledDependenciesLocator> _mockInstalledDependenciesLocator = new Mock<IInstalledDependenciesLocator>(MockBehavior.Strict);
        private readonly Mock<IDependencySnapshotInstaller> _mockInstaller = new Mock<IDependencySnapshotInstaller>(MockBehavior.Strict);
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        [Fact]
        public void DoesNothingOnConstruction()
        {
            CreateDependencyManagerWithMocks();
        }

        [Fact]
        public void Initialize_ReturnsNewSnapshotPath_WhenNoAcceptableDependencyVersionsInstalled()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(
                new[] { new DependencyManifestEntry("ModuleName", "1") });
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(default(string));
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            var dependencyManager = CreateDependencyManagerWithMocks();
            var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

            Assert.Equal("NewSnapshot", dependenciesPath);
        }

        [Fact]
        public void Initialize_ReturnsExistingSnapshotPath_WhenAcceptableDependencyVersionsAlreadyInstalled()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(
                GetAnyNonEmptyDependencyManifestEntries());
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns("InstalledSnapshot");

            var dependencyManager = CreateDependencyManagerWithMocks();
            var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

            Assert.Equal("InstalledSnapshot", dependenciesPath);
        }

        [Fact]
        public void Initialize_DoesNotTryToCheckDependencies_WhenNoDependenciesInManifest()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(new DependencyManifestEntry[0]);

            var dependencyManager = CreateDependencyManagerWithMocks();
            var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

            Assert.Null(dependenciesPath);
            VerifyMessageLogged(LogLevel.Warning, PowerShellWorkerStrings.FunctionAppDoesNotHaveDependentModulesToInstall);
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsNothing_WhenNoDependenciesInManifest()
        {
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(new DependencyManifestEntry[0]);

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);

            dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.False(hadToWait);
            _mockModuleProvider.VerifyNoOtherCalls();
            _mockInstalledDependenciesLocator.VerifyNoOtherCalls();
            _mockInstaller.VerifyNoOtherCalls();
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsSnapshotWithLatestPublishedModuleVersions()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(default(string));

            var dependencyManifestEntries = new[]
                {
                    new DependencyManifestEntry("A", "3"),
                    new DependencyManifestEntry("C", "7"),
                    new DependencyManifestEntry("B", "11")
                };

            var latestPublishedVersions = new Dictionary<string, string>
                {
                    { "A", "3.8.2" },
                    { "B", "11.0" },
                    { "C", "7.0.1.3" }
                };

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(dependencyManifestEntries);
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            foreach (var dependencyManifestEntry in dependencyManifestEntries)
            {
                _mockModuleProvider.Setup(
                    _ => _.GetLatestPublishedModuleVersion(
                            dependencyManifestEntry.Name,
                            dependencyManifestEntry.MajorVersion))
                    .Returns(latestPublishedVersions[dependencyManifestEntry.Name]);
            }

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.Is<IEnumerable<DependencyInfo>>(
                            di => IsExpectedDependencyInfoSequence(di, dependencyManifestEntries, latestPublishedVersions)),
                        "NewSnapshot",
                        It.IsAny<PowerShell>(),
                        _mockLogger.Object));

            _mockStorage.Setup(_ => _.SnapshotExists(It.IsAny<string>())).Returns(false);

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);
            dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);
            dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            _mockInstaller.Verify(
                _ => _.InstallSnapshot(
                    It.IsAny<IEnumerable<DependencyInfo>>(), It.IsAny<string>(), It.IsAny<PowerShell>(), It.IsAny<ILogger>()),
                Times.Once());

            _mockInstaller.VerifyNoOtherCalls();
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsSnapshotInForeground_WhenNoAcceptableDependenciesInstalled()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(default(string));
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");
            _mockModuleProvider.Setup(
                _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("LatestVersion");

            var firstPowerShellRunspace = PowerShell.Create();

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<IEnumerable<DependencyInfo>>(),
                        "NewSnapshot",
                        // Must run on the same runspace
                        It.Is<PowerShell>(powerShell => ReferenceEquals(firstPowerShellRunspace, powerShell)),
                        _mockLogger.Object));

            _mockStorage.Setup(_ => _.SnapshotExists("NewSnapshot")).Returns(false);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new string[0]);

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);
            dependencyManager.StartDependencyInstallationIfNeeded(firstPowerShellRunspace, PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.True(hadToWait);
            VerifyMessageLogged(LogLevel.Information, PowerShellWorkerStrings.DependencyDownloadInProgress);
            VerifyExactlyOneSnapshotInstalled();
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsSnapshotInBackground_WhenAcceptableDependenciesAlreadyInstalled()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns("AlreadyInstalled");
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");
            _mockModuleProvider.Setup(
                _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("LatestVersion");

            var firstPowerShellRunspace = PowerShell.Create();

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<IEnumerable<DependencyInfo>>(),
                        "NewSnapshot",
                        // Must run on a separate runspace
                        It.Is<PowerShell>(powerShell => !ReferenceEquals(firstPowerShellRunspace, powerShell)),
                        _mockLogger.Object));

            _mockStorage.Setup(_ => _.SnapshotExists("AlreadyInstalled")).Returns(true);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "AlreadyInstalled" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("AlreadyInstalled"))
                .Returns(DateTime.UtcNow - TimeSpan.FromMinutes(30));

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);
            dependencyManager.StartDependencyInstallationIfNeeded(firstPowerShellRunspace, PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.False(hadToWait);
            Assert.Equal("NewSnapshot", dependencyManager.WaitForBackgroundDependencyInstallationTaskCompletion());
            VerifyExactlyOneSnapshotInstalled();
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_DoesNotInstallSnapshot_WhenAcceptableDependenciesInstalledAndAnyInstallationStartedRecently()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns("AlreadyInstalled");

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            _mockModuleProvider.Setup(
                _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("LatestVersion");

            _mockStorage.Setup(_ => _.SnapshotExists("AlreadyInstalled")).Returns(true);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "AlreadyInstalled", "InProgress" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("AlreadyInstalled"))
                .Returns(DateTime.UtcNow - TimeSpan.FromMinutes(30));
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("InProgress"))
                .Returns(DateTime.UtcNow - TimeSpan.FromMinutes(1));

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);
            dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.Equal(!true, hadToWait);
            dependencyManager.WaitForBackgroundDependencyInstallationTaskCompletion();
            _mockInstaller.VerifyNoOtherCalls();
        }

        [Fact]
        public void StartDependencyInstallationIfNeeded_InstallsSnapshotInForeground_WhenNoAcceptableDependenciesInstalledAndAnyInstallationStartedRecently()
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(default(string));
            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            _mockModuleProvider.Setup(
                _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("LatestVersion");

            var firstPowerShellRunspace = PowerShell.Create();

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<IEnumerable<DependencyInfo>>(),
                        "NewSnapshot",
                        // Must run on the same runspace
                        It.Is<PowerShell>(powerShell => ReferenceEquals(firstPowerShellRunspace, powerShell)),
                        _mockLogger.Object));

            _mockStorage.Setup(_ => _.SnapshotExists("NewSnapshot")).Returns(false);
            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new[] { "NewSnapshot" });
            _mockStorage.Setup(_ => _.GetSnapshotCreationTimeUtc("NewSnapshot"))
                .Returns(DateTime.UtcNow - TimeSpan.FromMinutes(1));

            var dependencyManager = CreateDependencyManagerWithMocks();
            dependencyManager.Initialize(_mockLogger.Object);
            dependencyManager.StartDependencyInstallationIfNeeded(firstPowerShellRunspace, PowerShell.Create, _mockLogger.Object);
            var hadToWait = dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

            Assert.True(hadToWait);
            VerifyMessageLogged(LogLevel.Information, PowerShellWorkerStrings.DependencyDownloadInProgress);
            VerifyExactlyOneSnapshotInstalled();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StartDependencyInstallationIfNeeded_HandlesExceptionThrownBy_GetLatestPublishedModuleVersion(
            bool isAcceptableDependenciesSnapshotAlreadyInstalled)
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(isAcceptableDependenciesSnapshotAlreadyInstalled ? "AlreadyInstalled" : null);

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            var dependencyManager = CreateDependencyManagerWithMocks();
            var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

            var injectedException = new InvalidOperationException("Couldn't get latest published module version");

            _mockModuleProvider.Setup(
                _ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(injectedException);

            _mockStorage.Setup(_ => _.SnapshotExists(dependenciesPath))
                .Returns(isAcceptableDependenciesSnapshotAlreadyInstalled);

            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new string[0]);

            dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);

            if (isAcceptableDependenciesSnapshotAlreadyInstalled)
            {
                // Don't expect any exception, as an acceptable version of dependencies
                // has been found and will be used.
                dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

                var caughtException =
                    Assert.Throws<DependencyInstallationException>(
                        () => dependencyManager.WaitForBackgroundDependencyInstallationTaskCompletion());

                Assert.Contains(injectedException.Message, caughtException.Message);
                VerifyMessageLogged(LogLevel.Warning, "Function app dependencies upgrade skipped.");
                VerifyMessageLogged(LogLevel.Warning, injectedException.Message);
            }
            else
            {
                var caughtException =
                    Assert.Throws<DependencyInstallationException>(
                        () => dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object));

                Assert.Contains(injectedException.Message, caughtException.Message);
                Assert.Contains("Fail to get latest version for module", caughtException.Message);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StartDependencyInstallationIfNeeded_HandlesExceptionThrownBy_InstallDependenciesSnapshot(
            bool isAcceptableDependenciesSnapshotAlreadyInstalled)
        {
            _mockInstalledDependenciesLocator.Setup(_ => _.GetPathWithAcceptableDependencyVersionsInstalled())
                .Returns(isAcceptableDependenciesSnapshotAlreadyInstalled ? "AlreadyInstalled" : null);

            _mockStorage.Setup(_ => _.GetDependencies()).Returns(GetAnyNonEmptyDependencyManifestEntries());
            _mockStorage.Setup(_ => _.CreateNewSnapshotPath()).Returns("NewSnapshot");

            var dependencyManager = CreateDependencyManagerWithMocks();
            var dependenciesPath = dependencyManager.Initialize(_mockLogger.Object);

            _mockModuleProvider.Setup(_ => _.GetLatestPublishedModuleVersion(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("LatestVersion");

            _mockStorage.Setup(_ => _.GetInstalledAndInstallingSnapshots()).Returns(new string[0]);

            var injectedException = new Exception("Can't install dependencies");

            _mockInstaller.Setup(
                _ => _.InstallSnapshot(
                        It.IsAny<IEnumerable<DependencyInfo>>(), It.IsAny<string>(), It.IsAny<PowerShell>(), It.IsAny<ILogger>()))
                .Throws(injectedException);

            _mockStorage.Setup(_ => _.SnapshotExists(dependenciesPath))
                .Returns(isAcceptableDependenciesSnapshotAlreadyInstalled);

            dependencyManager.StartDependencyInstallationIfNeeded(PowerShell.Create(), PowerShell.Create, _mockLogger.Object);

            if (isAcceptableDependenciesSnapshotAlreadyInstalled)
            {
                // Don't expect any exception, as an acceptable version of dependencies
                // has been found and will be used.
                dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object);

                var caughtException =
                    Assert.Throws<DependencyInstallationException>(
                        () => dependencyManager.WaitForBackgroundDependencyInstallationTaskCompletion());

                VerifyMessageLogged(LogLevel.Trace, PowerShellWorkerStrings.AcceptableFunctionAppDependenciesAlreadyInstalled);

                Assert.Contains(injectedException.Message, caughtException.Message);
                VerifyMessageLogged(LogLevel.Warning, injectedException.Message);
            }
            else
            {
                var caughtException =
                    Assert.Throws<DependencyInstallationException>(
                        () => dependencyManager.WaitForDependenciesAvailability(() => _mockLogger.Object));

                Assert.Contains(injectedException.Message, caughtException.Message);
                Assert.Contains("Fail to install FunctionApp dependencies", caughtException.Message);
            }
        }

        private static DependencyManifestEntry[] GetAnyNonEmptyDependencyManifestEntries()
        {
            return new[] { new DependencyManifestEntry("ModuleName", "1") };
        }

        private static bool IsExpectedDependencyInfoSequence(
            IEnumerable<DependencyInfo> dependencyInfos,
            IReadOnlyCollection<DependencyManifestEntry> dependencyManifestEntries,
            IReadOnlyDictionary<string, string> latestPublishedVersions)
        {
            var dependencyInfosArray = dependencyInfos.ToArray();
            if (dependencyInfosArray.Length != dependencyManifestEntries.Count)
            {
                return false;
            }

            return dependencyManifestEntries.All(
                dependencyManifestEntry =>
                {
                    var dependencyInfo = dependencyInfosArray.SingleOrDefault(di => di.Name == dependencyManifestEntry.Name);
                    return (dependencyInfo != null)
                           && (dependencyInfo.LatestVersion == latestPublishedVersions[dependencyInfo.Name]);
                });
        }

        private void VerifyExactlyOneSnapshotInstalled()
        {
            _mockInstaller.Verify(
                _ => _.InstallSnapshot(
                    It.IsAny<IEnumerable<DependencyInfo>>(), It.IsAny<string>(), It.IsAny<PowerShell>(), It.IsAny<ILogger>()),
                Times.Once());

            _mockInstaller.VerifyNoOtherCalls();
        }

        private void VerifyMessageLogged(LogLevel expectedLogLevel, string expectedMessage)
        {
            _mockLogger.Verify(
                _ => _.Log(
                        expectedLogLevel,
                        It.Is<string>(message => message.Contains(expectedMessage)),
                        It.IsAny<Exception>(),
                        true));
        }

        private DependencyManager CreateDependencyManagerWithMocks()
        {
            return new DependencyManager(
                null,
                _mockModuleProvider.Object,
                _mockStorage.Object,
                _mockInstalledDependenciesLocator.Object,
                _mockInstaller.Object);
        }
    }
}
