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
        internal readonly string Path;
        internal readonly bool IsDownloadRequired;

        internal DependencyInfo(string name, string majorVersion, string path, bool isDownloadRequired)
        {
            Name = name;
            MajorVersion = majorVersion;
            Path = path;
            IsDownloadRequired = isDownloadRequired;
        }
    }
}
