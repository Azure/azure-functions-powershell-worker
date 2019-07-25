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
    using System.Management.Automation.Language;
    using System.Text;
    using System.Text.RegularExpressions;

    internal class DependencyManifest
    {
        private const string RequirementsPsd1FileName = "requirements.psd1";

        private readonly string _functionAppRootPath;

        private readonly int _maxDependencyEntries;

        public DependencyManifest(string functionAppRootPath, int maxDependencyEntries = 10)
        {
            if (string.IsNullOrWhiteSpace(functionAppRootPath))
            {
                throw new ArgumentException("Argument is null or empty", nameof(functionAppRootPath));
            }

            _functionAppRootPath = functionAppRootPath;
            _maxDependencyEntries = maxDependencyEntries;
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

                yield return new DependencyManifestEntry(
                    name,
                    VersionSpecificationType.MajorVersion,
                    GetMajorVersion(version));
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

            if (hashtable.Count > _maxDependencyEntries)
            {
                var message = string.Format(PowerShellWorkerStrings.TooManyDependencies, RequirementsPsd1FileName, hashtable.Count, _maxDependencyEntries);
                throw new ArgumentException(message);
            }

            return hashtable;
        }

        /// <summary>
        /// Validate that the module name is not null or empty.
        /// </summary>
        private static void ValidateModuleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(PowerShellWorkerStrings.DependencyNameIsNullOrEmpty);
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
            if (version == null)
            {
                throw new ArgumentNullException(version);
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
            var pattern = @"^(\d)+(\.)(\*)";
            return Regex.IsMatch(version, pattern);
        }
    }
}
