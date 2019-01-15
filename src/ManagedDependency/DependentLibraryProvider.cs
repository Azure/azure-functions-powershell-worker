// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Functions.PowerShellWorker.ManagedDependency
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Object to provide the dependent list of libraries mentioned in the host.json file
    /// </summary>
    internal class DependentLibraryProvider : IDependentLibraryProvider
    {
        /// <summary>
        /// Returns the enumerable collection of <see cref="DependentLibrary"/>
        /// </summary>
        /// <param name="hostFilePath">The function app host file path location</param>
        /// <returns>DependentLibraries object</returns>
        public async Task<DependentLibraries> GetDependentLibrariesAsync(string hostFilePath)
        {
            if (string.IsNullOrWhiteSpace(hostFilePath) || !File.Exists(hostFilePath))
            {
                return null;
            }

            var hostJson = JObject.Parse(await ReadAsync(hostFilePath));
            var managedDependenciesJToken = default(JToken);
            if (!hostJson.TryGetValue(LanguageConstants.ManagedDepenciesPropertyName, out managedDependenciesJToken) || managedDependenciesJToken == null)
            {
                return null;
            }

            var managedDependencies = Newtonsoft.Json.JsonConvert.DeserializeObject<DependentLibraries>(hostJson.ToString());
            return managedDependencies;
        }

        /// <summary>
        /// Ansychronously reads the content of a file
        /// </summary>
        /// <param name="path">The file path</param>
        /// <param name="encoding">The encoding of the file</param>
        /// <returns>String content of the file</returns>
        private async Task<string> ReadAsync(string path, Encoding encoding = null)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            encoding = encoding ?? Encoding.UTF8;
            using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(fileStream, encoding, true, 4096))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
