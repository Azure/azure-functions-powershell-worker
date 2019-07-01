//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation.Language;
    using System.Text;
    using System.Text.RegularExpressions;

    internal class DependencyManifest
    {
        private const string RequirementsPsd1FileName = "requirements.psd1";

        private const string AzModuleName = "Az";

        // The list of managed dependencies supported in Azure Functions.
        private static readonly List<string> SupportedManagedDependencies = new List<string> { AzModuleName };

        private readonly string _functionAppRootPath;

        public DependencyManifest(string functionAppRootPath)
        {
            if (string.IsNullOrWhiteSpace(functionAppRootPath))
            {
                throw new ArgumentException("Argument is null or empty", nameof(functionAppRootPath));
            }

            _functionAppRootPath = functionAppRootPath;
        }

        public IEnumerable<DependencyManifestEntry> GetEntries()
        {
            var hashtable = ParsePowerShellDataFile();

            foreach (DictionaryEntry entry in hashtable)
            {
                // A valid entry is of the form: 'ModuleName'='MajorVersion.*"
                var name = (string)entry.Key;
                var version = (string)entry.Value;

                ValidateModuleName(name);

                yield return new DependencyManifestEntry(name, GetMajorVersion(version));
            }
        }

        /// <summary>
        /// Parses the given PowerShell (psd1) data file.
        /// Returns a Hashtable representing the key value pairs.
        /// </summary>
        private Hashtable ParsePowerShellDataFile()
        {
            // Path to requirements.psd1 file.
            var requirementsFilePath = Path.Join(_functionAppRootPath, RequirementsPsd1FileName);

            if (!File.Exists(requirementsFilePath))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.FileNotFound, RequirementsPsd1FileName, _functionAppRootPath);
                throw new ArgumentException(errorMessage);
            }

            // Try to parse the requirements.psd1 file.
            var ast = Parser.ParseFile(requirementsFilePath, out _, out ParseError[] errors);

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

            Ast hashtableAst = ast.Find(x => x is HashtableAst, false);
            Hashtable hashtable = hashtableAst?.SafeGetValue() as Hashtable;

            if (hashtable == null)
            {
                string errorMsg = string.Format(PowerShellWorkerStrings.InvalidPowerShellDataFile, RequirementsPsd1FileName);
                throw new ArgumentException(errorMsg);
            }

            return hashtable;
        }

        /// <summary>
        /// Validate that the module name is not null or empty,
        /// and ensure that the module is a supported dependency.
        /// </summary>
        private static void ValidateModuleName(string name)
        {
            // Validate the name property.
            if (string.IsNullOrEmpty(name))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.DependencyPropertyIsNullOrEmpty, "name");
                throw new ArgumentException(errorMessage);
            }

            // If this is not a supported module, error out.
            if (!SupportedManagedDependencies.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.ManagedDependencyNotSupported, name);
                throw new ArgumentException(errorMessage);
            }
        }

        /// <summary>
        /// Parses the given string version and extracts the major version.
        /// Please note that the only version we currently support is of the form '1.*'.
        /// </summary>
        private static string GetMajorVersion(string version)
        {
            ValidateVersionFormat(version);
            return version.Split(".")[0];
        }

        private static void ValidateVersionFormat(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.DependencyPropertyIsNullOrEmpty, "version");
                throw new ArgumentException(errorMessage);
            }

            if (!IsValidVersionFormat(version))
            {
                var errorMessage = string.Format(PowerShellWorkerStrings.InvalidVersionFormat, "MajorVersion.*");
                throw new ArgumentException(errorMessage);
            }
        }

        /// <summary>
        /// Validates the given version format. Currently, we only support 'Number.*'.
        /// </summary>
        private static bool IsValidVersionFormat(string version)
        {
            var pattern = @"^(\d){1,2}(\.)(\*)";
            return Regex.IsMatch(version, pattern);
        }
    }
}
