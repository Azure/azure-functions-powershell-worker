//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Xunit;
    using Moq;

    using PowerShellWorker.DependencyManagement;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    public class PowerShellModuleSnapshotComparerTests
    {
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        [Fact]
        public void ReturnsTrueWhenNoModules()
        {
            var comparer = new PowerShellModuleSnapshotComparer(path => new string[0]);

            Assert.True(comparer.AreEquivalent("SnapshotX", "SnapshotY", _mockLogger.Object));
            Assert.True(comparer.AreEquivalent("SnapshotY", "SnapshotX", _mockLogger.Object));
        }

        [Theory]
        // Single module:
        [InlineData(
            "SnapshotX/ModuleA",
            "SnapshotX/ModuleA/Version1",
            "SnapshotY/ModuleA",
            "SnapshotY/ModuleA/Version1")]
        // Multiple modules:
        [InlineData(
            "SnapshotX/ModuleA",
            "SnapshotX/ModuleA/Version2",
            "SnapshotX/ModuleB",
            "SnapshotX/ModuleB/Version3",
            "SnapshotY/ModuleA",
            "SnapshotY/ModuleA/Version2",
            "SnapshotY/ModuleB",
            "SnapshotY/ModuleB/Version3")]
        // The order of module subdirectories does not matter:
        [InlineData(
            "SnapshotX/ModuleA",
            "SnapshotX/ModuleA/Version2",
            "SnapshotX/ModuleB",
            "SnapshotX/ModuleB/Version3",
            "SnapshotY/ModuleB",
            "SnapshotY/ModuleB/Version3",
            "SnapshotY/ModuleA",
            "SnapshotY/ModuleA/Version2")]
        // Multiple versions:
        [InlineData(
            "SnapshotX/ModuleA",
            "SnapshotX/ModuleA/Version1",
            "SnapshotX/ModuleA/Version2",
            "SnapshotY/ModuleA",
            "SnapshotY/ModuleA/Version1",
            "SnapshotY/ModuleA/Version2")]
        // The order of module version subdirectories does not matter:
        [InlineData(
            "SnapshotX/ModuleA",
            "SnapshotX/ModuleA/Version1",
            "SnapshotX/ModuleA/Version2",
            "SnapshotY/ModuleA",
            "SnapshotY/ModuleA/Version2",
            "SnapshotY/ModuleA/Version1")]
        public void ReturnsTrueWhenSameModuleVersions(params string[] subdirStrings)
        {
            var subdirs = ParseTestSubdirectories(subdirStrings);
            var comparer = new PowerShellModuleSnapshotComparer(path => subdirs[path]);

            Assert.True(comparer.AreEquivalent("SnapshotX", "SnapshotY", _mockLogger.Object));
            Assert.True(comparer.AreEquivalent("SnapshotY", "SnapshotX", _mockLogger.Object));
        }

        [Theory]
        // Same module, different versions:
        [InlineData(
            "SnapshotX/ModuleA",
            "SnapshotX/ModuleA/Version1",
            "SnapshotY/ModuleA",
            "SnapshotY/ModuleA/Version2")]
        // Same version, different modules:
        [InlineData(
            "SnapshotX/ModuleA",
            "SnapshotX/ModuleA/Version1",
            "SnapshotY/ModuleB",
            "SnapshotY/ModuleB/Version1")]
        // Additional or missing modules:
        [InlineData(
            "SnapshotX/ModuleA",
            "SnapshotX/ModuleA/Version1",
            "SnapshotY/ModuleB",
            "SnapshotY/ModuleB/Version1",
            "SnapshotY/ModuleC",
            "SnapshotY/ModuleC/Version1")]
        // Additional or missing versions:
        [InlineData(
            "SnapshotX/ModuleA",
            "SnapshotX/ModuleA/Version1",
            "SnapshotY/ModuleB",
            "SnapshotY/ModuleB/Version1",
            "SnapshotY/ModuleB/Version2")]
        public void ReturnsFalseWhenDifferentModuleVersions(params string[] subdirStrings)
        {
            var subdirs = ParseTestSubdirectories(subdirStrings);
            var comparer = new PowerShellModuleSnapshotComparer(path => subdirs[path]);

            Assert.False(comparer.AreEquivalent("SnapshotX", "SnapshotY", _mockLogger.Object));
            Assert.False(comparer.AreEquivalent("SnapshotY", "SnapshotX", _mockLogger.Object));
        }

        [Fact]
        public void ReturnsFalseWhenEnumeratingSubdirectoriesThrows()
        {
            var injectedException = new IOException("Can't enumerate directories");

            var comparer = new PowerShellModuleSnapshotComparer(
                path => path == "SnapshotX" ? throw injectedException : new string[0]);

            Assert.False(comparer.AreEquivalent("SnapshotX", "SnapshotY", _mockLogger.Object));
            Assert.False(comparer.AreEquivalent("SnapshotY", "SnapshotX", _mockLogger.Object));

            _mockLogger.Verify(
                _ => _.Log(
                    false,
                    LogLevel.Warning,
                    It.Is<string>(message => message.Contains("SnapshotX")
                                             && message.Contains("SnapshotY")
                                             && message.Contains(injectedException.Message)),
                    null),
                Times.Exactly(2));
        }

        private static IDictionary<string, List<string>> ParseTestSubdirectories(IEnumerable<string> paths)
        {
            var result = new Dictionary<string, List<string>>();

            foreach (var path in paths.Select(p => p.Replace('/', Path.DirectorySeparatorChar)))
            {
                var parentPath = Path.GetDirectoryName(path);
                if (result.ContainsKey(parentPath))
                {
                    result[parentPath].Add(path);
                }
                else
                {
                    result[parentPath] = new List<string> { path };
                }
            }

            return result;
        }
    }
}
