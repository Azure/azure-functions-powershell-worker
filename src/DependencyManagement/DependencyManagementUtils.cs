//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyManagementUtils
    {
        /// <summary>
        /// Returns true if the given majorVersion is less or equal to the major version in latestSupportedVersion.
        /// </summary>
        internal static bool IsValidMajorVersion(string majorVersion, string latestSupportedVersion)
        {
            // A Version object cannot be created with a single digit so add a '.0' to it.
            var requestedVersion = new Version(majorVersion + ".0");
            var latestVersion = new Version(latestSupportedVersion);

            var result = (requestedVersion.Major <= latestVersion.Major);

            return result;
        }

        /// <summary>
        /// Deletes the contents at the given directory
        /// </summary>
        internal static void EmptyDirectory(string path)
        {
            var directoryInfo = new DirectoryInfo(path);

            if (directoryInfo.Exists)
            {
                foreach (var file in directoryInfo.GetFiles())
                {
                    file.Delete();
                }

                foreach (var directory in directoryInfo.GetDirectories())
                {
                    directory.Delete(true);
                }
            }
        }
    }
}

