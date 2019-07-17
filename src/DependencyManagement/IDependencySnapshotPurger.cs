//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    internal interface IDependencySnapshotPurger
    {
        /// <summary>
        /// Set the path to the snapshot currently used by the current worker.
        /// As long as there is any live worker that declared this snapshot as
        /// being in use, this snapshot should not be purged by any worker.
        /// </summary>
        void SetCurrentlyUsedSnapshot(string path, ILogger logger);

        /// <summary>
        /// Remove unused snapshots.
        /// </summary>
        void Purge(ILogger logger);
    }
}
