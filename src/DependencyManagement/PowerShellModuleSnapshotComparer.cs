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

    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class PowerShellModuleSnapshotComparer : IDependencySnapshotComparer
    {
        private readonly Func<string, IEnumerable<string>> _getSubdirectories;

        public PowerShellModuleSnapshotComparer()
            : this(Directory.EnumerateDirectories)
        {
        }

        internal PowerShellModuleSnapshotComparer(Func<string, IEnumerable<string>> getSubdirectories)
        {
            _getSubdirectories = getSubdirectories;
        }

        public bool AreEquivalent(string snapshotPathA, string snapshotPathB, ILogger logger)
        {
            try
            {
                var versionSubdirsA = GetModuleVersionSubdirectories(snapshotPathA);
                var versionSubdirsB = GetModuleVersionSubdirectories(snapshotPathB);
                return versionSubdirsA.SequenceEqual(versionSubdirsB);
            }
            catch (IOException e)
            {
                // An exception here is not really expected, so logging it just in case,
                // but it is safe to assume that the snapshots are different.
                var message = string.Format(
                    PowerShellWorkerStrings.FailedToCompareDependencySnapshots,
                    snapshotPathA,
                    snapshotPathB,
                    e.Message);

                logger.Log(isUserOnlyLog: false, LogLevel.Warning, message);
                return false;
            }
        }

        private IEnumerable<string> GetModuleVersionSubdirectories(string snapshotPath)
        {
            var modulePaths = _getSubdirectories(snapshotPath).ToList();
            modulePaths.Sort();
            foreach (var modulePath in modulePaths)
            {
                var versionPaths = _getSubdirectories(modulePath).ToList();
                versionPaths.Sort();
                foreach (var versionPath in versionPaths)
                {
                    yield return Path.Join(Path.GetFileName(modulePath), Path.GetFileName(versionPath));
                }
            }
        }
    }
}
