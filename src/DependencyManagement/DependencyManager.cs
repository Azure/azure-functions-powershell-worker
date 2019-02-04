//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyManager
    {
        // The list of dependent modules for the function app.
        internal static List<DependencyInfo> Dependencies { get; private set; }

        // Keep track of whether the function app dependencies have been installed.
        internal static bool FunctionAppDependenciesInstalled { get; set; }

        // Requirements.psd1 file name.
        private const string RequirementsPsd1FileName = "Requirements.psd1";

        // The list of managed dependencies supported in Azure Functions.
        internal static readonly List<string> SupportedManagedDependencies = new List<string>(){"Az"};

        internal DependencyManager()
        {
            Dependencies = new List<DependencyInfo>();
        }

        /// <summary>
        /// Process Function App dependencies if specified in FunctionLoadRequest.DependencyDownloadRequest.
        /// </summary>
        internal void SetFunctionAppDependencies(FunctionLoadRequest request)
        {
            // Resolve the FunctionApp root path.
            var functionAppRootPath = Path.GetFullPath(Path.Join(request.Metadata.Directory, ".."));

            // Location where the dependent modules will be installed.
            var modulesFolderPath = Path.Join(functionAppRootPath, "Modules");

            // Path to Requirements.psd1 file.
            var requirementsFilePath = Path.Join(functionAppRootPath, RequirementsPsd1FileName);

            // Parse and process Requirements.psd1.
            Hashtable entries = ParsePowerShellDataFile(requirementsFilePath);
            foreach (DictionaryEntry entry in entries)
            {
                var name = (string)entry.Key;
                var version = (string)entry.Value;

                // Validates that the module name.
                ValidateModuleName(name);

                // Get the module major version.
                var majorVersion = GetMajorVersion(version);

                // Create a DependencyInfo object and add it to the list of dependencies to install.
                var dependencyInfo = new DependencyInfo(name, majorVersion, modulesFolderPath, true);
                Dependencies.Add(dependencyInfo);
            }
        }

        /// <summary>
        /// Parses the given string version and extracts the major version.
        /// Please note that the only version we currently support is of the form '1.*'.
        /// </summary>
        internal string GetMajorVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                var errorMessage = String.Format(PowerShellWorkerStrings.DependencyPropertyIsNullOrEmpty, "version");
                throw new ArgumentException(errorMessage);
            }

            // Validate that version is in the correct format: 'MajorVersion.*'
            if (!IsValidVersionFormat(version))
            {
                var errorMessage = String.Format(PowerShellWorkerStrings.InvalidVersionFormat, "MajorVersion.*");
                throw new ArgumentException(errorMessage);
            }

            // Return the major version.
            return version.Split(".")[0];
        }

        /// <summary>
        /// Parses the given PowerShell (psd1) data file.
        /// Returns a Hashtable representing the key value pairs.
        /// </summary>

        internal Hashtable ParsePowerShellDataFile(string filePath)
        {
            // Validate the file path.
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                var errorMessage = String.Format(PowerShellWorkerStrings.FileNotFound, RequirementsPsd1FileName);
                throw new ArgumentException(errorMessage);
            }

            // Try to parse the Requirements.psd1 file.
            var ast = Parser.ParseFile(filePath, out _, out ParseError[] errors);

            if (errors?.Length > 0)
            {
                var stringBuilder = new StringBuilder();
                foreach (var error in errors)
                {
                    stringBuilder.AppendLine(error.Message);
                }
                string errorMsg = stringBuilder.ToString();
                throw new ArgumentException(string.Format(PowerShellWorkerStrings.FailToParseScript, RequirementsPsd1FileName, errorMsg));
            }

            var hashtableAst = ast.Find(x => x is HashtableAst, false);
            try
            {
                var hashtable = (Hashtable)hashtableAst?.SafeGetValue();
                return hashtable;
            }
            catch
            {
                string errorMsg = string.Format(PowerShellWorkerStrings.InvalidPowerShellDataFile, RequirementsPsd1FileName);
                throw new ArgumentException(errorMsg);
            }
        }

        /// <summary>
        /// Validates the given version format. Currently, we only support 'Number.*'.
        /// </summary>
        internal bool IsValidVersionFormat(string version)
        {
            var pattern = @"^(\d){1,2}(\.)(\*)";
            var regex = new Regex(pattern);
            return regex.IsMatch(version);
        }

        /// <summary>
        /// Validate that the module name is not null or empty,
        /// and ensure that the module is a supported dependency.
        /// </summary>
        internal void ValidateModuleName(string name)
        {
            // Validate the name property.
            if (string.IsNullOrWhiteSpace(name))
            {
                var errorMessage = String.Format(PowerShellWorkerStrings.DependencyPropertyIsNullOrEmpty, "name");
                throw new ArgumentException(errorMessage);
            }

            // If this is not a supported module, error out.
            if (!SupportedManagedDependencies.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                var errorMessage = String.Format(PowerShellWorkerStrings.ManagedDependencyNotSupported, name);
                throw new ArgumentException(errorMessage);
            }
        }
    }
}

