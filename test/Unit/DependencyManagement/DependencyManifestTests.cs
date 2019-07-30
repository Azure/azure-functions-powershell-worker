//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using System;
    using System.IO;
    using System.Linq;
    using Xunit;

    using PowerShellWorker.DependencyManagement;

    public class DependencyManifestTests : IDisposable
    {
        private readonly string _appRootPath;

        public DependencyManifestTests()
        {
            _appRootPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_appRootPath);
        }

        public void Dispose()
        {
            Directory.Delete(_appRootPath, recursive: true);
        }

        [Fact]
        public void CanBeConstructedWithAnyAppRootPath()
        {
            new DependencyManifest("This path does not have to exist");
        }

        [Fact]
        public void GetEntriesThrowsWhenRequirementsFileDoesNotExist()
        {
            var manifest = new DependencyManifest(_appRootPath);
            var exception = Assert.Throws<ArgumentException>(() => manifest.GetEntries().ToList());
            Assert.Contains("No 'requirements.psd1' is found at the FunctionApp root folder", exception.Message);
            Assert.Contains(_appRootPath, exception.Message);
        }

        [Fact]
        public void GetEntriesThrowsWhenRequirementsFileIsEmpty()
        {
            CreateRequirementsFile(string.Empty);

            var manifest = new DependencyManifest(_appRootPath);
            var exception = Assert.Throws<ArgumentException>(() => manifest.GetEntries().ToList());
            Assert.Equal(
                "The PowerShell data file 'requirements.psd1' is invalid since it cannot be evaluated into a Hashtable object.",
                exception.Message);
        }

        [Fact]
        public void GetEntriesParsesRequirementsFileWithNoEntries()
        {
            CreateRequirementsFile("@{ }");

            var manifest = new DependencyManifest(_appRootPath);
            Assert.Empty(manifest.GetEntries());
        }

        [Theory]
        [InlineData("@{ MyModule = '0.*' }", "MyModule", "0")]
        [InlineData("@{ MyModule = '1.*' }", "MyModule", "1")]
        [InlineData("@{ MyModule = '23.*' }", "MyModule", "23")]
        [InlineData("@{ MyModule = '456.*' }", "MyModule", "456")]
        public void GetEntriesParsesRequirementsFileWithSingleEntry(string content, string moduleName, string majorVersion)
        {
            CreateRequirementsFile(content);

            var manifest = new DependencyManifest(_appRootPath);
            var entries = manifest.GetEntries().ToList();

            Assert.Single(entries);
            Assert.Equal(moduleName, entries.Single().Name);
            Assert.Equal(majorVersion, entries.Single().MajorVersion);
        }

        [Fact]
        public void GetEntriesParsesRequirementsFileWithMultipleEntries()
        {
            CreateRequirementsFile("@{ A = '3.*'; B = '7.*'; C = '0.*' }");

            var manifest = new DependencyManifest(_appRootPath, maxDependencyEntries: 3);
            var entries = manifest.GetEntries().ToList();

            Assert.Equal(3, entries.Count);
            Assert.Equal("3", entries.Single(entry => entry.Name == "A").MajorVersion);
            Assert.Equal("7", entries.Single(entry => entry.Name == "B").MajorVersion);
            Assert.Equal("0", entries.Single(entry => entry.Name == "C").MajorVersion);
        }

        [Theory]
        [InlineData("@{ MyModule = '' }")]
        [InlineData("@{ MyModule = 'a' }")]
        [InlineData("@{ MyModule = '.' }")]
        [InlineData("@{ MyModule = '1' }")]
        [InlineData("@{ MyModule = '1.' }")]
        [InlineData("@{ MyModule = '1.0' }")]
        [InlineData("@{ MyModule = '1.2' }")]
        [InlineData("@{ MyModule = '2.3.4' }")]
        public void GetEntriesThrowsOnInvalidVersionSpecification(string content)
        {
            CreateRequirementsFile(content);

            var manifest = new DependencyManifest(_appRootPath);

            var exception = Assert.Throws<ArgumentException>(() => manifest.GetEntries().ToList());
            Assert.Contains("Version is not in the correct format", exception.Message);
        }

        [Theory]
        [InlineData("@{ '' = '1.*' }")]
        [InlineData("@{ ' ' = '1.*' }")]
        [InlineData("@{ ' ' = '' }")]
        public void GetEntriesThrowsOnInvalidModuleName(string content)
        {
            CreateRequirementsFile(content);

            var manifest = new DependencyManifest(_appRootPath);

            var exception = Assert.Throws<ArgumentException>(() => manifest.GetEntries().ToList());
            Assert.Contains("Dependency name is null or empty", exception.Message);
        }

        [Fact]
        public void GetEntriesThrowsOnNullModuleName()
        {
            CreateRequirementsFile("@{ $null = '1.0' }");

            var manifest = new DependencyManifest(_appRootPath);

            Assert.ThrowsAny<Exception>(() => manifest.GetEntries().ToList());
        }

        [Fact]
        public void GetEntriesThrowsWhenTooManyEntries()
        {
            CreateRequirementsFile("@{ A = '3.*'; B = '7.*'; C = '0.*' }");

            var manifest = new DependencyManifest(_appRootPath, maxDependencyEntries: 2);

            var exception = Assert.Throws<ArgumentException>(() => manifest.GetEntries().ToList());

            const string ExpectedMessage = "The number of entries in the 'requirements.psd1' file is 3,"
                                           + " which exceeds the maximum supported number of entries (2).";
            Assert.Equal(ExpectedMessage, exception.Message);
        }

        private void CreateRequirementsFile(string content)
        {
            using (var writer = new StreamWriter(Path.Join(_appRootPath, "requirements.psd1")))
            {
                writer.Write(content);
            }
        }
    }
}
