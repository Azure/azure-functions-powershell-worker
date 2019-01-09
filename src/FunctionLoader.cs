//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// FunctionLoader holds metadata of functions.
    /// </summary>
    internal class FunctionLoader
    {
        private readonly Dictionary<string, AzFunctionInfo> _loadedFunctions = new Dictionary<string, AzFunctionInfo>();

        internal static string FunctionAppRootPath { get; private set; }
        internal static string FunctionAppProfilePath { get; private set; }
        internal static string FunctionModulePath { get; private set; }

        /// <summary>
        /// Query for function metadata can happen in parallel.
        /// </summary>
        internal AzFunctionInfo GetFunctionInfo(string functionId)
        {
            if (_loadedFunctions.TryGetValue(functionId, out AzFunctionInfo funcInfo))
            {
                return funcInfo;
            }

            throw new InvalidOperationException($"Function with the ID '{functionId}' was not loaded.");
        }

        /// <summary>
        /// This method runs once per 'FunctionLoadRequest' during the code start of the worker.
        /// It will always run synchronously because we process 'FunctionLoadRequest' synchronously.
        /// </summary>
        internal void LoadFunction(FunctionLoadRequest request)
        {
            _loadedFunctions.Add(request.FunctionId, new AzFunctionInfo(request.Metadata));
        }

        /// <summary>
        /// Sets up paths to powershell runtime resources
        /// </summary>
        /// <param name="functionBaseDirectory"> Function base directory</param>
        /// <param name="managedModulePath">Managed module path</param>
        internal static void SetupRuntimePaths(string functionBaseDirectory, string managedModulePath)
        {
            SetupWellKnownPaths(functionBaseDirectory);

            // Setup managed module path only after setting up well know paths.
            // This is important for preserving the priority (override) of function app modules over managed modules.
            TrySetupManagedModulePath(managedModulePath);
        }

        /// <summary>
        /// Setup the well known paths about the FunctionApp.
        /// This method is called only once during the code start.
        /// </summary>
        private static void SetupWellKnownPaths(string functionBaseDirectory)
        {
            // Resolve the FunctionApp root path
            FunctionAppRootPath = Path.GetFullPath(Path.Join(functionBaseDirectory, ".."));
            // Resolve module paths
            var appLevelModulesPath = Path.Join(FunctionAppRootPath, "Modules");
            var workerLevelModulesPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            FunctionModulePath = $"{appLevelModulesPath}{Path.PathSeparator}{workerLevelModulesPath}";

            // Resolve the FunctionApp profile path
            var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
            var profiles = Directory.EnumerateFiles(FunctionAppRootPath, "profile.ps1", options);
            FunctionAppProfilePath = profiles.FirstOrDefault();
        }

        /// <summary>
        /// Validates and appends managed module path (if supplied) to PsModulePath
        /// </summary>
        /// <param name="managedModulePath">Managed module path</param>
        private static void TrySetupManagedModulePath(string managedModulePath)
        {
            if (string.IsNullOrEmpty(managedModulePath))
            {
                return; // no-op : for case in which no managed module path is supplied.
            }

            if (!Directory.Exists(managedModulePath))
            {
                // If a path is supplied, it should be a valid path
                throw new ArgumentException("Invalid managed module path: '{0}'", managedModulePath);
            }

            FunctionModulePath = $"{FunctionModulePath}{Path.PathSeparator}{managedModulePath}";
        }
    }
}
