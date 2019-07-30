﻿//
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
        [InlineData("@{ MyModule = '0.*' }", "MyModule", "0", VersionSpecificationType.MajorVersion)]
        [InlineData("@{ MyModule = '1.*' }", "MyModule", "1", VersionSpecificationType.MajorVersion)]
        [InlineData("@{ MyModule = '23.*' }", "MyModule", "23", VersionSpecificationType.MajorVersion)]
        [InlineData("@{ MyModule = '456.*' }", "MyModule", "456", VersionSpecificationType.MajorVersion)]
        [InlineData("@{ MyModule = '0' }", "MyModule", "0", VersionSpecificationType.ExactVersion)]
        [InlineData("@{ MyModule = '1' }", "MyModule", "1", VersionSpecificationType.ExactVersion)]
        [InlineData("@{ MyModule = '1.0' }", "MyModule", "1.0", VersionSpecificationType.ExactVersion)]
        [InlineData("@{ MyModule = '3.4.5' }", "MyModule", "3.4.5", VersionSpecificationType.ExactVersion)]
        [InlineData("@{ MyModule = '123.45.67.89' }", "MyModule", "123.45.67.89", VersionSpecificationType.ExactVersion)]
        [InlineData("@{ MyModule = '123.45.67.89-alpha4' }", "MyModule", "123.45.67.89-alpha4", VersionSpecificationType.ExactVersion)]
        public void GetEntriesParsesRequirementsFileWithSingleEntry(
            string content,
            string moduleName,
            string majorVersion,
            VersionSpecificationType versionSpecificationType)
        {
            CreateRequirementsFile(content);

            var manifest = new DependencyManifest(_appRootPath);
            var entries = manifest.GetEntries().ToList();

            Assert.Single(entries);
            Assert.Equal(moduleName, entries.Single().Name);
            Assert.Equal(versionSpecificationType, entries.Single().VersionSpecificationType);
            Assert.Equal(majorVersion, entries.Single().VersionSpecification);
        }

        [Fact]
        public void GetEntriesParsesRequirementsFileWithMultipleEntries()
        {
            CreateRequirementsFile("@{ A = '3.*'; B = '7.*'; C = '0.*' }");

            var manifest = new DependencyManifest(_appRootPath, maxDependencyEntries: 3);
            var entries = manifest.GetEntries().ToList();

            Assert.Equal(3, entries.Count);

            var entryA = entries.Single(entry => entry.Name == "A");
            Assert.Equal(VersionSpecificationType.MajorVersion, entryA.VersionSpecificationType);
            Assert.Equal("3", entryA.VersionSpecification);

            var entryB = entries.Single(entry => entry.Name == "B");
            Assert.Equal(VersionSpecificationType.MajorVersion, entryB.VersionSpecificationType);
            Assert.Equal("7", entryB.VersionSpecification);

            var entryC = entries.Single(entry => entry.Name == "C");
            Assert.Equal(VersionSpecificationType.MajorVersion, entryC.VersionSpecificationType);
            Assert.Equal("0", entryC.VersionSpecification);
        }

        [Theory]
        [InlineData("@{ MyModule = '' }")]
        [InlineData("@{ MyModule = ' ' }")]
        [InlineData("@{ MyModule = 'a' }")]
        [InlineData("@{ MyModule = '1a' }")]
        [InlineData("@{ MyModule = '.' }")]
        [InlineData("@{ MyModule = '1.' }")]
        [InlineData("@{ MyModule = '*' }")]
        [InlineData("@{ MyModule = '*.1' }")]
        [InlineData("@{ MyModule = '1.*.2' }")]
        [InlineData("@{ MyModule = '1.0.*' }")]
        public void GetEntriesThrowsOnInvalidVersionSpecification(string content)
        {
            CreateRequirementsFile(content);

            var manifest = new DependencyManifest(_appRootPath);

            var exception = Assert.Throws<ArgumentException>(() => manifest.GetEntries().ToList());
            Assert.Contains("not in the correct format", exception.Message);
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
