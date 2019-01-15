// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
namespace Microsoft.Azure.Functions.PowerShellWorker.ManagedDependency
{
    using System.Threading.Tasks;

    /// <summary>
    /// When implemented, prvides the enumerable collection of <see cref="DependentLibrary"/>"/>
    /// </summary>
    internal interface IDependentLibraryProvider
    {
        /// <summary>
        /// Returns the enumerable collection of <see cref="DependentLibrary"/>
        /// </summary>
        /// <param name="hostFilePath">The function app host file path location</param>
        /// <returns>DependentLibraries object</returns>
        Task<DependentLibraries> GetDependentLibrariesAsync(string hostFilePath);
    }
}
