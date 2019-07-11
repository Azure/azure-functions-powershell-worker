//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyManifestEntry
    {
        public string Name { get; }

        public string MajorVersion { get; }

        public DependencyManifestEntry(string name, string majorVersion)
        {
            Name = name;
            MajorVersion = majorVersion;
        }
    }
}
