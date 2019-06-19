//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyManagementUtils
    {
        /// <summary>
        /// Deletes the contents at the given directory.
        /// </summary>
        internal static void EmptyDirectory(string path)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(path);

                if (directoryInfo.Exists)
                {
                    IEnumerable<string> files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);

                        // Remove any problematic file attributes.
                        fileInfo.Attributes = fileInfo.Attributes &
                                              ~(FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System);
                        fileInfo.Delete();
                    }

                    foreach (DirectoryInfo subDirectory in directoryInfo.GetDirectories())
                    {
                        subDirectory.Delete(true);
                    }
                }
            }
            catch (Exception)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToClenupModuleDestinationPath, path);
                throw new InvalidOperationException(errorMsg);
            }
        }
    }
}
