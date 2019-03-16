//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyInfo
    {
        internal readonly string Name;
        internal readonly string MajorVersion;
        internal readonly string LatestVersion;

        internal DependencyInfo(string name, string majorVersion, string latestVersion)
        {
            Name = name;
            MajorVersion = majorVersion;
            LatestVersion = latestVersion;
        }
    }
}
