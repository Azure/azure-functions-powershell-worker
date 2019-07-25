//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyManifestEntry
    {
        public string Name { get; }

        public VersionSpecificationType VersionSpecificationType { get;  }

        public string VersionSpecification { get; }

        public DependencyManifestEntry(string name, string versionSpecification)
        {
            Name = name;
            VersionSpecificationType = VersionSpecificationType.MajorVersion;
            VersionSpecification = versionSpecification;
        }
    }
}
