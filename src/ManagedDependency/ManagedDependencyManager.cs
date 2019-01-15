// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Functions.PowerShellWorker.ManagedDependency
{
    using Microsoft.Azure.Functions.PowerShellWorker.ManagedDependency;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Linq;

    internal class ManagedDependencyManager
    {
        /// <summary>
        /// Adds Az module path if Az is mentioned as module dependencies
        /// </summary>
        /// <param name="functionAppRootPath">Function app root path </param>
        /// <param name="functionModulePath">Modules path for powershell modules</param>
        /// <returns>Powershell function modules path</returns>
        public static string AddAzModulesPath(string functionAppRootPath, string functionModulePath)
        {
            if (string.IsNullOrWhiteSpace(functionAppRootPath))
            {
                throw new ArgumentException($"Function app root path parameter: {nameof(functionAppRootPath)} cannot be null or empty");
            }

            var dependentLibraryProvider = new DependentLibraryProvider();
            var dependentLibraries = dependentLibraryProvider.GetDependentLibrariesAsync(Path.Combine(functionAppRootPath, LanguageConstants.HostFileName)).ConfigureAwait(false).GetAwaiter().GetResult();

            if (dependentLibraries != null
                && dependentLibraries.ManagedDependencies != null
                && dependentLibraries.ManagedDependencies.Any())
            {
                var azLibrary = dependentLibraries.ManagedDependencies.FirstOrDefault(library => string.Equals(library.Name, "az", StringComparison.InvariantCultureIgnoreCase));
                if (azLibrary != null)
                {
                    functionModulePath = $"{functionModulePath}{Path.PathSeparator}{LanguageConstants.LatestAzModulePath}";
                }
            }

            return functionModulePath;
        }
    }
}
