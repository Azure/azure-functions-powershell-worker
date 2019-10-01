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
                var versionSubdirsA = PowerShellModuleSnapshotTools.GetModuleVersionSubdirectories(snapshotPathA, _getSubdirectories);
                var versionSubdirsB = PowerShellModuleSnapshotTools.GetModuleVersionSubdirectories(snapshotPathB, _getSubdirectories);
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
    }
}
