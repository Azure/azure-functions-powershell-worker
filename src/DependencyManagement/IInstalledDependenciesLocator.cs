//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal interface IInstalledDependenciesLocator
    {
        string GetPathWithAcceptableDependencyVersionsInstalled();
    }
}