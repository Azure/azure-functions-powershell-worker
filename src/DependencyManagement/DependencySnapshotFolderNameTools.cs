﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;

    internal static class DependencySnapshotFolderNameTools
    {
        private const string InstallingPostfix = ".i";

        public const string InstalledPostfix = ".r";

        public const string InstalledPattern = "*" + InstalledPostfix;

        public static string CreateUniqueName()
        {
            var uniqueBase = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss.ffffff");
            return uniqueBase + InstalledPostfix;
        }

        /// <summary>
        /// Converts an _installed_ snapshot path to an _installing_ snapshot by
        /// appending a postfix, so that that the resulting path follows a different
        /// pattern and can be discovered using a different file mask.
        /// For example, for the _installed_ path
        ///     ".../20190710-1234.567890.r"
        /// the _installing_ path will be:
        ///     ".../20190710-1234.567890.r.i"
        /// This makes it possible to enumerate all the installed snapshots by using ".../*.r" mask,
        /// and all the installing snapshots by using ".../*.i" mask.
        /// </summary>
        public static string ConvertInstalledToInstalling(string installedPath)
        {
            return installedPath + InstallingPostfix;
        }
    }
}
