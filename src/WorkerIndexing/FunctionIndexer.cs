//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker.WorkerIndexing
{
    /// <summary>
    /// Discovers Azure Functions defined using the V2 attribute-based programming model
    /// by scanning PowerShell files and parsing the AST (no code execution needed).
    /// </summary>
    internal static class FunctionIndexer
    {
        private const string FunctionLanguagePowerShell = "powershell";

        // Directories to skip when scanning for function files
        private static readonly HashSet<string> ExcludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".vscode",
            ".venv",
            "node_modules",
            "__pycache__",
            "bin",
            "obj",
        };

        /// <summary>
        /// Scans the function app directory for PowerShell files containing V2 function definitions.
        /// Returns RpcFunctionMetadata for each discovered function.
        /// </summary>
        internal static IEnumerable<RpcFunctionMetadata> IndexFunctions(string functionAppDirectory, ILogger logger)
        {
            var results = new List<RpcFunctionMetadata>();
            var scriptFiles = DiscoverScriptFiles(functionAppDirectory);

            foreach (var scriptFile in scriptFiles)
            {
                try
                {
                    var functions = IndexFunctionsInFile(scriptFile, functionAppDirectory);
                    results.AddRange(functions);
                }
                catch (Exception ex)
                {
                    logger?.Log(
                        isUserOnlyLog: false,
                        Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level.Warning,
                        string.Format(PowerShellWorkerStrings.FailedToIndexFile, scriptFile, ex.Message));
                }
            }

            return results;
        }

        /// <summary>
        /// Indexes all V2 functions defined in a single PowerShell file.
        /// </summary>
        internal static IEnumerable<RpcFunctionMetadata> IndexFunctionsInFile(string scriptFile, string functionAppDirectory)
        {
            var scriptAst = Parser.ParseFile(scriptFile, out _, out ParseError[] errors);
            if (errors != null && errors.Length > 0)
            {
                var errorMessages = string.Join(Environment.NewLine, errors.Select(e => e.Message));
                throw new InvalidOperationException(
                    string.Format(PowerShellWorkerStrings.FailToParseScript, scriptFile, errorMessages));
            }

            var results = new List<RpcFunctionMetadata>();

            // Find all function definitions with [AzFunction()] attribute
            var functionDefs = scriptAst.FindAll(
                ast => ast is FunctionDefinitionAst,
                searchNestedScriptBlocks: false)
                .Cast<FunctionDefinitionAst>();

            foreach (var funcDef in functionDefs)
            {
                var azFuncAttr = GetAzFunctionAttribute(funcDef);
                if (azFuncAttr == null)
                {
                    continue;
                }

                var metadata = BuildFunctionMetadata(funcDef, azFuncAttr, scriptFile, functionAppDirectory);
                results.Add(metadata);
            }

            return results;
        }

        #region Private helpers

        /// <summary>
        /// Recursively discovers all .ps1/.psm1 files in the function app directory,
        /// excluding well-known directories and paths matching .funcignore patterns.
        /// </summary>
        private static IEnumerable<string> DiscoverScriptFiles(string rootDirectory)
        {
            var files = new List<string>();
            var ignorePatterns = LoadFuncIgnorePatterns(rootDirectory);
            DiscoverScriptFilesRecursive(rootDirectory, rootDirectory, ignorePatterns, files);
            return files;
        }

        private static void DiscoverScriptFilesRecursive(
            string directory,
            string rootDirectory,
            List<Regex> ignorePatterns,
            List<string> files)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    var ext = Path.GetExtension(file);
                    if (ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".psm1", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!IsIgnored(file, rootDirectory, ignorePatterns))
                        {
                            files.Add(file);
                        }
                    }
                }

                foreach (var subDir in Directory.EnumerateDirectories(directory))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (!ExcludedDirectories.Contains(dirName) &&
                        !IsIgnored(subDir, rootDirectory, ignorePatterns))
                    {
                        DiscoverScriptFilesRecursive(subDir, rootDirectory, ignorePatterns, files);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have permission to access
            }
            catch (DirectoryNotFoundException)
            {
                // Directory may have been removed during scan
            }
        }

        /// <summary>
        /// Loads patterns from a .funcignore file in the given directory.
        /// Returns an empty list if no .funcignore file exists.
        /// Each non-empty, non-comment line is converted to a Regex pattern.
        /// </summary>
        internal static List<Regex> LoadFuncIgnorePatterns(string rootDirectory)
        {
            var patterns = new List<Regex>();
            var funcIgnorePath = Path.Combine(rootDirectory, ".funcignore");

            if (!File.Exists(funcIgnorePath))
            {
                return patterns;
            }

            foreach (var line in File.ReadAllLines(funcIgnorePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                {
                    continue;
                }

                var regexPattern = GlobToRegex(trimmed);
                patterns.Add(new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }

            return patterns;
        }

        /// <summary>
        /// Checks whether a file or directory path matches any of the .funcignore patterns.
        /// Patterns are tested against both the file/directory name and its relative path.
        /// </summary>
        private static bool IsIgnored(string fullPath, string rootDirectory, List<Regex> patterns)
        {
            if (patterns.Count == 0)
            {
                return false;
            }

            var name = Path.GetFileName(fullPath);
            var relativePath = fullPath.Substring(rootDirectory.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');

            foreach (var pattern in patterns)
            {
                if (pattern.IsMatch(name) || pattern.IsMatch(relativePath))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts a simple glob pattern to a regex pattern.
        /// Supports * (any chars except /), ** (any chars including /), and ? (single char).
        /// </summary>
        internal static string GlobToRegex(string glob)
        {
            var regexParts = new System.Text.StringBuilder("^");
            int i = 0;

            while (i < glob.Length)
            {
                char c = glob[i];

                if (c == '*')
                {
                    if (i + 1 < glob.Length && glob[i + 1] == '*')
                    {
                        // ** matches everything including directory separators
                        regexParts.Append(".*");
                        i += 2;
                        // Skip trailing / after **
                        if (i < glob.Length && (glob[i] == '/' || glob[i] == '\\'))
                        {
                            i++;
                        }
                    }
                    else
                    {
                        // * matches everything except directory separators
                        regexParts.Append("[^/\\\\]*");
                        i++;
                    }
                }
                else if (c == '?')
                {
                    regexParts.Append("[^/\\\\]");
                    i++;
                }
                else
                {
                    regexParts.Append(Regex.Escape(c.ToString()));
                    i++;
                }
            }

            regexParts.Append("$");
            return regexParts.ToString();
        }

        /// <summary>
        /// Finds the [AzFunction()] attribute on a function definition, if present.
        /// Checks both the function body's param block attributes and the function's attributes.
        /// </summary>
        private static AttributeAst GetAzFunctionAttribute(FunctionDefinitionAst funcDef)
        {
            // In PowerShell, function-level attributes are on the param block:
            //   function Foo { [AzFunction()] param(...) }
            var paramBlock = funcDef.Body?.ParamBlock;
            if (paramBlock?.Attributes != null)
            {
                foreach (var attr in paramBlock.Attributes)
                {
                    if (AttributeToBindingConverter.IsAzFunctionAttribute(attr))
                    {
                        return attr;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Builds RpcFunctionMetadata from a function definition AST.
        /// </summary>
        private static RpcFunctionMetadata BuildFunctionMetadata(
            FunctionDefinitionAst funcDef,
            AttributeAst azFuncAttr,
            string scriptFile,
            string functionAppDirectory)
        {
            var functionName = AttributeToBindingConverter.GetFunctionNameOverride(azFuncAttr)
                               ?? funcDef.Name;
            var relativeFile = Path.GetFileName(scriptFile);

            var metadata = new RpcFunctionMetadata
            {
                Name = functionName,
                FunctionId = Guid.NewGuid().ToString(),
                EntryPoint = funcDef.Name,
                ScriptFile = scriptFile,
                Directory = functionAppDirectory,
                Language = FunctionLanguagePowerShell,
                IsProxy = false,
            };

            // Mark as worker-indexed so AzFunctionInfo can distinguish V2 from V1
            metadata.Properties.Add("WorkerIndexed", "true");

            var triggerNames = new List<string>();
            var bindingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Extract bindings from parameter attributes
            var paramBlock = funcDef.Body?.ParamBlock;
            if (paramBlock?.Parameters != null)
            {
                foreach (var param in paramBlock.Parameters)
                {
                    var paramName = param.Name.VariablePath.UserPath;

                    foreach (var attr in param.Attributes)
                    {
                        if (!(attr is AttributeAst attrAst))
                        {
                            continue;
                        }

                        if (!AttributeToBindingConverter.IsBindingAttribute(attrAst))
                        {
                            continue;
                        }

                        // Validate unique binding names
                        if (!bindingNames.Add(paramName))
                        {
                            throw new InvalidOperationException(
                                string.Format(PowerShellWorkerStrings.DuplicateBindingName, functionName, relativeFile, paramName));
                        }

                        // Validate required properties
                        foreach (var missing in AttributeToBindingConverter.GetMissingRequiredProperties(attrAst))
                        {
                            var bindingType = AttributeToBindingConverter.GetBindingType(attrAst) ?? attrAst.TypeName.Name;
                            throw new InvalidOperationException(
                                string.Format(PowerShellWorkerStrings.MissingRequiredBindingProperty,
                                    functionName, relativeFile, paramName, bindingType, missing));
                        }

                        var bindingInfo = AttributeToBindingConverter.ConvertToBindingInfo(attrAst);
                        var rawBinding = AttributeToBindingConverter.ConvertToRawBinding(attrAst, paramName);

                        metadata.Bindings.Add(paramName, bindingInfo);
                        metadata.RawBindings.Add(rawBinding);

                        if (AttributeToBindingConverter.IsTriggerAttribute(attrAst))
                        {
                            triggerNames.Add(paramName);
                        }
                    }
                }
            }

            // Validate exactly one trigger
            if (triggerNames.Count == 0)
            {
                throw new InvalidOperationException(
                    string.Format(PowerShellWorkerStrings.NoTriggerBinding, functionName, relativeFile));
            }
            if (triggerNames.Count > 1)
            {
                throw new InvalidOperationException(
                    string.Format(PowerShellWorkerStrings.MultipleTriggerBindings,
                        functionName, relativeFile, string.Join(", ", triggerNames)));
            }

            // Add implicit output bindings based on trigger type
            var triggerParamAttr = GetTriggerAttribute(funcDef);
            if (triggerParamAttr != null)
            {
                var triggerTypeName = triggerParamAttr.TypeName.Name;
                if (triggerTypeName.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
                {
                    triggerTypeName = triggerTypeName.Substring(0, triggerTypeName.Length - "Attribute".Length);
                }

                foreach (var (name, info, rawBinding) in AttributeToBindingConverter.GetImplicitOutputBindings(triggerTypeName))
                {
                    if (bindingNames.Add(name))
                    {
                        metadata.Bindings.Add(name, info);
                        metadata.RawBindings.Add(rawBinding);
                    }
                }
            }

            return metadata;
        }

        /// <summary>
        /// Finds the trigger attribute on a function's parameters.
        /// </summary>
        private static AttributeAst GetTriggerAttribute(FunctionDefinitionAst funcDef)
        {
            var paramBlock = funcDef.Body?.ParamBlock;
            if (paramBlock?.Parameters == null) return null;

            foreach (var param in paramBlock.Parameters)
            {
                foreach (var attr in param.Attributes)
                {
                    if (attr is AttributeAst attrAst && AttributeToBindingConverter.IsTriggerAttribute(attrAst))
                    {
                        return attrAst;
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
