//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class FunctionLoader
    {
        private readonly MapField<string, AzFunctionInfo> _loadedFunctions = new MapField<string, AzFunctionInfo>();

        internal static string FunctionAppRootLocation { get; set; }
        internal static string FunctionAppProfileLocation { get; set; } = null;
        internal static string FunctionAppModulesLocation { get; set; } = null;

        internal AzFunctionInfo GetFunctionInfo(string functionId)
        {
            if (_loadedFunctions.TryGetValue(functionId, out AzFunctionInfo funcInfo))
            {
                return funcInfo;
            }

            throw new InvalidOperationException($"Function with the ID '{functionId}' was not loaded.");
        }

        /// <summary>
        /// Runs once per Function in a Function App. Loads the Function info into the Function Loader
        /// </summary>
        internal void LoadFunction(FunctionLoadRequest request)
        {
            // TODO: catch "load" issues at "func start" time.
            // ex. Script doesn't exist, entry point doesn't exist
            _loadedFunctions.Add(request.FunctionId, new AzFunctionInfo(request.Metadata));
        }

        /// <summary>
        /// Sets up well-known paths like the Function App root,
        /// the Function App 'Modules' folder,
        /// and the Function App's profile.ps1
        /// </summary>
        internal static void SetupWellKnownPaths(string functionAppRootLocation)
        {
            FunctionLoader.FunctionAppRootLocation = functionAppRootLocation;
            FunctionLoader.FunctionAppModulesLocation = Path.Combine(functionAppRootLocation, "Modules");

            // Find the profile.ps1 in the Function App root if it exists
            List<string> profiles = Directory.EnumerateFiles(functionAppRootLocation, "profile.ps1", new EnumerationOptions {
                MatchCasing = MatchCasing.CaseInsensitive
            }).ToList();
            if (profiles.Count() > 0)
            {
                FunctionLoader.FunctionAppProfileLocation = profiles[0];
            }
        }
    }

    internal enum AzFunctionType
    {
        None = 0,
        RegularFunction = 1,
        OrchestrationFunction = 2,
        ActivityFunction = 3
    }

    internal class AzFunctionInfo
    {
        private const string OrchestrationTrigger = "orchestrationTrigger";
        private const string ActivityTrigger = "activityTrigger";

        internal const string TriggerMetadata = "TriggerMetadata";
        internal const string DollarReturn = "$return";

        internal readonly string Directory;
        internal readonly string EntryPoint;
        internal readonly string FunctionName;
        internal readonly string ScriptPath;
        internal readonly AzFunctionType Type;
        internal readonly MapField<string, BindingInfo> AllBindings;
        internal readonly MapField<string, BindingInfo> OutputBindings;

        internal AzFunctionInfo(RpcFunctionMetadata metadata)
        {
            FunctionName = metadata.Name;
            Directory = metadata.Directory;
            EntryPoint = metadata.EntryPoint;
            ScriptPath = metadata.ScriptFile;

            AllBindings = new MapField<string, BindingInfo>();
            OutputBindings = new MapField<string, BindingInfo>();

            foreach (var binding in metadata.Bindings)
            {
                string bindingName = binding.Key;
                BindingInfo bindingInfo = binding.Value;

                AllBindings.Add(bindingName, bindingInfo);

                // PowerShell doesn't support the 'InOut' type binding
                if (bindingInfo.Direction == BindingInfo.Types.Direction.In)
                {
                    switch (bindingInfo.Type)
                    {
                        case OrchestrationTrigger:
                            Type = AzFunctionType.OrchestrationFunction;
                            break;
                        case ActivityTrigger:
                            Type = AzFunctionType.ActivityFunction;
                            break;
                        default:
                            Type = AzFunctionType.RegularFunction;
                            break;
                    }
                    continue;
                }

                if (bindingInfo.Direction == BindingInfo.Types.Direction.Out)
                {
                    OutputBindings.Add(bindingName, bindingInfo);
                }
            }
        }
    }
}
