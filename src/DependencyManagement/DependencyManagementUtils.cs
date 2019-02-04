//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyManagementUtils
    {
        // File name which holds module information after it is downloaded/installed from the PSGallery.
        private const string DependencyInfoJsonFileName = "DependencyInfo.json";

        /// <summary>
        /// Returns true if the given majorVersion is less or equal to the major version in latestSupportedVersion.
        /// </summary>
        internal static bool IsValidMajorVersion(string majorVersion, string latestSupportedVersion)
        {
            // A Version object cannot be created with a single digit so add a '.0' to it.
            var requestedVersion = new Version(majorVersion + ".0");
            var latestVersion = new Version(latestSupportedVersion);

            var result = (requestedVersion.Major <= latestVersion.Major);

            return result;
        }

        /// <summary>
        /// Validates if a module is already installed at the given path for the given version.
        /// </summary>
        internal static bool IsLatestVersion(string moduleFolderPath, string version)
        {
            // Try to read the DependencyInfo.json file in the module folder
            var pathToDependencyInfoJson = Path.Join(moduleFolderPath, DependencyInfoJsonFileName);
            var installedDependency = GetInstalledDependency(pathToDependencyInfoJson);

            if (installedDependency != null)
            {
                // Compare the version of the installed dependency with the given one
                var installedVersion = new Version(installedDependency.Version);
                var latestVersion = new Version(version);
                var result = installedVersion.CompareTo(latestVersion);

                return (result >= 0);
            }

            return false;
        }

        /// <summary>
        /// Create a hidden DependencyInfo.json entry under the module folder name.
        /// </summary>
        internal static void NewDependencyInfoEntry(string moduleName, string version, string path)
        {
            // File content
            var jsonContent = "{'Name': '" + moduleName + "', 'Version': '" + version + "' }";
            jsonContent = FormatJson(jsonContent);

            // Module folder path (path\ModuleName)
            var moduleFolderPath = Path.Join(path, moduleName);

            // Write the json file
            var jsonFilePath = Path.Join(moduleFolderPath, DependencyInfoJsonFileName);
            File.WriteAllText(jsonFilePath, jsonContent);

            // Set the file attributes to hidden
            File.SetAttributes(jsonFilePath, FileAttributes.Hidden);
        }

        /// <summary>
        /// Formats the given json string
        /// </summary>
        private static string FormatJson(string jsonContent)
        {
            if (!string.IsNullOrWhiteSpace(jsonContent))
            {
                return JToken.Parse(jsonContent).ToString();
            }

            return null;
        }

        /// <summary>
        /// Reads the json file at the given path and returns a list of InstalledDependency objects.
        /// </summary>
        internal static InstalledDependency GetInstalledDependency(string filePath)
        {
            try
            {
                var fileContent = File.ReadAllText(filePath);
                var jsonContent = JObject.Parse(fileContent);
                var jsonContentObject = JsonConvert.DeserializeObject<InstalledDependency>(jsonContent.ToString());
                return jsonContentObject;
            }
            catch
            {
                // Ignore exceptions caused by trying to read the file and deserialization.
            }

            return null;
        }
    }

    internal class InstalledDependency
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }
}

