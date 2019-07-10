//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using System.Threading;

    using Xunit;

    using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;

    public class DependencySnapshotFolderNameToolsTests
    {
        [Fact]
        public void CreatesUniqueEnoughNames()
        {
            var name1 = DependencySnapshotFolderNameTools.CreateUniqueName();
            Thread.Sleep(1); // A snapshot name created 1 millisecond later must be different
            var name2 = DependencySnapshotFolderNameTools.CreateUniqueName();
            Assert.NotEqual(name1, name2);
        }

        [Fact]
        public void CreatedNamesHaveInstalledPostfix()
        {
            var name = DependencySnapshotFolderNameTools.CreateUniqueName();
            Assert.EndsWith(DependencySnapshotFolderNameTools.InstalledPostfix, name);
        }

        [Fact]
        public void NamesConvertedFromInstalledToInstallingDoNotHaveInstalledPostfix()
        {
            var name = DependencySnapshotFolderNameTools.CreateUniqueName();
            var convertedToInstalling = DependencySnapshotFolderNameTools.ConvertInstalledToInstalling(name);
            Assert.False(convertedToInstalling.EndsWith(DependencySnapshotFolderNameTools.InstalledPostfix));
        }

        [Fact]
        public void UniqueNamesConvertedFromInstalledToInstallingAreStillUnique()
        {
            var name1 = DependencySnapshotFolderNameTools.CreateUniqueName();
            Thread.Sleep(1); // A snapshot name created 1 millisecond later must be different
            var name2 = DependencySnapshotFolderNameTools.CreateUniqueName();

            var convertedToInstalling1 = DependencySnapshotFolderNameTools.ConvertInstalledToInstalling(name1);
            var convertedToInstalling2 = DependencySnapshotFolderNameTools.ConvertInstalledToInstalling(name2);

            Assert.NotEqual(convertedToInstalling1, convertedToInstalling2);
        }
    }
}
