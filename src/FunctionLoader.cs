//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// FunctionLoader holds metadata of functions.
    /// </summary>
    internal class FunctionLoader
    {
        private readonly Dictionary<string, AzFunctionInfo> _loadedFunctions = new Dictionary<string, AzFunctionInfo>();
        private const string latestAzModulePath = @"D:\Program Files (x86)\ManagedDependencies\PowerShell\AzPSModules\1.0.0";

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
        /// Setup the well known paths about the FunctionApp.
        /// This method is called only once during the code start.
        /// </summary>
        internal static void SetupWellKnownPaths(FunctionLoadRequest request)
        {
            // Resolve the FunctionApp root path
            FunctionAppRootPath = Path.GetFullPath(Path.Join(request.Metadata.Directory, ".."));
            // Resolve module paths
            var appLevelModulesPath = Path.Join(FunctionAppRootPath, "Modules");
            var workerLevelModulesPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            FunctionModulePath = $"{appLevelModulesPath}{Path.PathSeparator}{workerLevelModulesPath}";
            AddLatestAzModulesPath();
            // Resolve the FunctionApp profile path
            var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
            var profiles = Directory.EnumerateFiles(FunctionAppRootPath, "profile.ps1", options);
            FunctionAppProfilePath = profiles.FirstOrDefault();
        }

        /// <summary>
        /// Adds the latest AzModules path to FunctionModulePath
        /// </summary>
        private static void AddLatestAzModulesPath()
        {
            if (!string.IsNullOrWhiteSpace(FunctionModulePath)
                && !string.IsNullOrWhiteSpace(latestAzModulePath)
                && Platform.IsWindows)
            {
                FunctionModulePath = $"{FunctionModulePath}{Path.PathSeparator}{latestAzModulePath}";
            }
        }
    }
}
