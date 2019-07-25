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
                // A valid entry is of the form:
                //     'ModuleName'='MajorVersion.*'
                // or
                //     'ModuleName'='ExactVersion'
                var name = (string)entry.Key;
                var version = (string)entry.Value;

                ValidateModuleName(name);

                // Look for the 'MajorVersion.*' pattern
                var match = Regex.Match(version, @"^(\d+)\.\*$");
                if (match.Success)
                {
                    yield return new DependencyManifestEntry(
                        name,
                        VersionSpecificationType.MajorVersion,
                        match.Groups[1].Value);
                }
                else
                {
                    // This is a very basic sanity check of the format that allows
                    // us detect some obviously wrong cases: make sure it starts with digits,
                    // does not contain * anywhere, and ends with a word character.
                    // Not even trying to match the actual version format rules.
                    if (!Regex.IsMatch(version, @"^\d+([^\*]*?\w)?$"))
                    {
                        var errorMessage = string.Format(PowerShellWorkerStrings.InvalidVersionFormat, version, "MajorVersion.*");
                        throw new ArgumentException(errorMessage);
                    }

                    yield return new DependencyManifestEntry(
                        name,
                        VersionSpecificationType.ExactVersion,
                        version);
                }
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
    }
}
