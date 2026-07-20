//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// FunctionLoader holds metadata of functions.
    /// </summary>
    internal static class FunctionLoader
    {
        private const string ProfileFileName = "profile.ps1";

        private static readonly Dictionary<string, AzFunctionInfo> LoadedFunctions = new Dictionary<string, AzFunctionInfo>();

        internal static string FunctionAppRootPath { get; private set; }
        internal static string FunctionAppProfilePath { get; private set; }
        internal static string FunctionModulePath { get; private set; }

        /// <summary>
        /// Query for function metadata can happen in parallel.
        /// </summary>
        internal static AzFunctionInfo GetFunctionInfo(string functionId)
        {
            if (LoadedFunctions.TryGetValue(functionId, out AzFunctionInfo funcInfo))
            {
                return funcInfo;
            }

            throw new InvalidOperationException(string.Format(PowerShellWorkerStrings.FunctionNotLoaded, functionId));
        }

        /// <summary>
        /// Returns true if the function with the given functionId is already loaded.
        /// </summary>
        public static bool IsLoaded(string functionId)
        {
            return LoadedFunctions.ContainsKey(functionId);
        }

        /// <summary>
        /// This method runs once per 'FunctionLoadRequest' during the code start of the worker.
        /// It will always run synchronously because we process 'FunctionLoadRequest' synchronously.
        /// </summary>
        internal static void LoadFunction(FunctionLoadRequest request)
        {
            LoadedFunctions.Add(request.FunctionId, new AzFunctionInfo(request.Metadata));
        }

        /// <summary>
        /// Get all loaded functions.
        /// </summary>
        internal static IEnumerable<AzFunctionInfo> GetLoadedFunctions()
        {
            return LoadedFunctions.Values;
        }

        /// <summary>
        /// Clear all loaded functions.
        /// </summary>
        internal static void ClearLoadedFunctions()
        {
            LoadedFunctions.Clear();
        }

        /// <summary>
        /// Setup the well known paths about the FunctionApp.
        /// This method is called only once during the code start.
        /// </summary>
        internal static void SetupWellKnownPaths(FunctionLoadRequest request, string managedDependenciesPath)
        {
            // Resolve the FunctionApp root path
            FunctionAppRootPath = Path.GetFullPath(Path.Join(request.Metadata.Directory, ".."));

            // Resolve module paths
            var appLevelModulesPath = Path.Join(FunctionAppRootPath, "Modules");
            var workerLevelModulesPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            FunctionModulePath = $"{appLevelModulesPath}{Path.PathSeparator}{workerLevelModulesPath}";

            // Add the managed dependencies folder path
            if (managedDependenciesPath != null)
            {
                FunctionModulePath = $"{managedDependenciesPath}{Path.PathSeparator}{FunctionModulePath}";
            }

            // Resolve the FunctionApp profile path
            FunctionAppProfilePath = ResolveFunctionAppProfilePath(FunctionAppRootPath);
        }

        /// <summary>
        /// Resolve the path to the function app 'profile.ps1', if one exists.
        /// </summary>
        /// <remarks>
        /// The profile file name is fixed and known, so we resolve it with a direct existence
        /// check rather than a filtered directory enumeration. A filtered
        /// Directory.EnumerateFiles(root, "profile.ps1", ...).FirstOrDefault() can throw on some
        /// content-share file systems under .NET 10 (10.0.9 / 10.0.10) when there is no matching
        /// file: the OS-level filter reports a zero-match as STATUS_OBJECT_NAME_NOT_FOUND, which
        /// surfaces as "Could not find file 'C:\home\site\wwwroot'." and terminates the worker for
        /// apps that do not include a profile.ps1. See issues #1146 and #1147.
        /// File.Exists does not go through the filtered FileSystemEnumerator code path and simply
        /// returns false when the file is absent.
        /// </remarks>
        private static string ResolveFunctionAppProfilePath(string functionAppRootPath)
        {
            try
            {
                if (!Directory.Exists(functionAppRootPath))
                {
                    return null;
                }

                // Fast path: exact match (case-insensitive on Windows, and the common casing elsewhere).
                var profilePath = Path.Combine(functionAppRootPath, ProfileFileName);
                if (File.Exists(profilePath))
                {
                    return profilePath;
                }

                // Fallback: preserve case-insensitive matching on case-sensitive file systems
                // without relying on an OS-level filtered enumeration.
                foreach (var file in Directory.EnumerateFiles(functionAppRootPath))
                {
                    if (string.Equals(Path.GetFileName(file), ProfileFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Treat an inability to resolve the profile as "no profile" rather than a
                // terminating failure. The worker functions correctly without a profile.ps1.
            }

            return null;
        }
    }
}
