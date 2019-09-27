//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    internal class PowerShellModuleSnapshotTools
    {
        /// <summary>
        /// Returns all module version subdirectories, assuming that the directory structure follows
        /// regular PowerShell conventions.
        ///
        /// For example, if PowerShell modules are installed into the "C:\Modules" directory
        /// and the content of this directory looks like this:
        /// 
        ///         C:\
        ///             Modules\
        ///                 ModuleA\
        ///                     1.0\
        ///                         ...
        ///                     2.1\
        ///                         ...
        ///                 ModuleB\
        ///                     1.3.2\
        ///                         ...
        /// then GetModuleVersionSubdirectories("C:\Modules") will return the following strings:
        /// 
        ///     C:\Modules\ModuleA\1.0
        ///     C:\Modules\ModuleA\2.1
        ///     C:\Modules\ModuleB\1.3.2
        /// 
        /// </summary>
        public static IEnumerable<string> GetModuleVersionSubdirectories(string snapshotPath)
        {
            return GetModuleVersionSubdirectories(snapshotPath, Directory.EnumerateDirectories);
        }

        public static IEnumerable<string> GetModuleVersionSubdirectories(
            string snapshotPath,
            Func<string, IEnumerable<string>> getSubdirectories)
        {
            var modulePaths = getSubdirectories(snapshotPath).ToList();
            modulePaths.Sort();
            foreach (var modulePath in modulePaths)
            {
                var versionPaths = getSubdirectories(modulePath).ToList();
                versionPaths.Sort();
                foreach (var versionPath in versionPaths)
                {
                    yield return Path.Join(Path.GetFileName(modulePath), Path.GetFileName(versionPath));
                }
            }
        }
    }
}
