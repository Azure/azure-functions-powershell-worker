using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Unit.PowerShell
{
    public class PowershellManagedModulePathTests
    {
        private readonly string _functionDirectory;
        private readonly string _workerModulePath;

        public PowershellManagedModulePathTests()
        {
            _functionDirectory = TestUtils.FunctionDirectory;
            _workerModulePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules");
        }

        [Fact]
        public void FunctionLoaderHandlesEmptyManagedModulePath()
        {
            FunctionLoader.SetupRuntimePaths(_functionDirectory, string.Empty);
            string funcAppModulePath = Path.Join(FunctionLoader.FunctionAppRootPath, "Modules");
            string expectedPath = $"{funcAppModulePath}{Path.PathSeparator}{_workerModulePath}";
            Assert.Equal(expectedPath, FunctionLoader.FunctionModulePath);
        }

        [Fact]
        public void FunctionLoaderHandlesNullManagedModulePath()
        {
            FunctionLoader.SetupRuntimePaths(_functionDirectory, null);
            string funcAppModulePath = Path.Join(FunctionLoader.FunctionAppRootPath, "Modules");
            string expectedPath = $"{funcAppModulePath}{Path.PathSeparator}{_workerModulePath}";
            Assert.Equal(expectedPath, FunctionLoader.FunctionModulePath);
        }

        [Fact]
        public void FunctionLoaderThrowsUponNonExistentManagedModulePath()
        {
            var nonExistentManagedModulePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, this.GetRandomFolderName());

            Assert.Throws<ArgumentException>(() => FunctionLoader.SetupRuntimePaths(_functionDirectory, nonExistentManagedModulePath));
        }

        [Fact]
        public void FunctionLoaderIncludesValidManagedModulePath()
        {
            var validManagedModulePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, this.GetRandomFolderName());
            // create a temporary managed module directory
            Directory.CreateDirectory(validManagedModulePath);

            FunctionLoader.SetupRuntimePaths(_functionDirectory, validManagedModulePath);
            Assert.Contains(validManagedModulePath, FunctionLoader.FunctionModulePath);

            // clean up temporary managed module directory
            Directory.Delete(validManagedModulePath);
        }

        [Fact]
        public void FunctionLoaderAddsValidManagedModulePathAfterAppModulePaths()
        {
            var validManagedModulePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, this.GetRandomFolderName());
            Directory.CreateDirectory(validManagedModulePath);

            FunctionLoader.SetupRuntimePaths(_functionDirectory, validManagedModulePath);
            string funcAppModulePath = Path.Join(FunctionLoader.FunctionAppRootPath, "Modules");
            string expectedPath = $"{funcAppModulePath}{Path.PathSeparator}{_workerModulePath}{Path.PathSeparator}{validManagedModulePath}";

            Assert.Equal(expectedPath, FunctionLoader.FunctionModulePath);

            Directory.Delete(validManagedModulePath);
        }

        private string GetRandomFolderName()
        {
            // Path.GetRandomFileName() generates a 12 chars with dot starting at the 9th position. So, take the first 8 chars.
            return Path.GetRandomFileName().Substring(0, 8);
        }
    }
}