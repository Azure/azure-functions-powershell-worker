//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;

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

            throw new InvalidOperationException(string.Format(PowerShellWorkerStrings.FunctionNotLoaded, functionId));
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

            // Add the managed dependencies folder path
            if (DependencyManager.DependenciesPath != null)
            {
                FunctionModulePath = $"{DependencyManager.DependenciesPath}{Path.PathSeparator}{FunctionModulePath}";
            }

            // Resolve the FunctionApp profile path
            var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
            var profiles = Directory.EnumerateFiles(FunctionAppRootPath, "profile.ps1", options);
            FunctionAppProfilePath = profiles.FirstOrDefault();
        }
    }
}
