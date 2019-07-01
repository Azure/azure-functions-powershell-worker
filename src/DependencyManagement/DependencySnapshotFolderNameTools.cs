//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;

    internal static class DependencySnapshotFolderNameTools
    {
        private const string InstallingPrefix = ".i";

        public const string InstalledPostfix = ".r";

        public const string InstalledPattern = "*" + InstalledPostfix;

        public static string CreateUniqueName()
        {
            var uniqueBase = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss.ffffff");
            return uniqueBase + InstalledPostfix;
        }

        public static string ConvertInstalledToInstalling(string installedPath)
        {
            return installedPath + InstallingPrefix;
        }
    }
}
