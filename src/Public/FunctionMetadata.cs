//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// Function metadata for the PowerShellWorker module to query.
    /// </summary>
    public static class FunctionMetadata
    {
        internal static ConcurrentDictionary<Guid, ReadOnlyDictionary<string, ReadOnlyBindingInfo>> OutputBindingCache
            = new ConcurrentDictionary<Guid, ReadOnlyDictionary<string, ReadOnlyBindingInfo>>();

        /// <summary>
        /// Get the binding metadata for the given Runspace instance id.
        /// </summary>
        public static ReadOnlyDictionary<string, ReadOnlyBindingInfo> GetOutputBindingInfo(Guid runspaceInstanceId)
        {
            ReadOnlyDictionary<string, ReadOnlyBindingInfo> outputBindings = null;
            OutputBindingCache.TryGetValue(runspaceInstanceId, out outputBindings);
            return outputBindings;
        }

        /// <summary>
        /// Helper method to set the output binding metadata for the function that is about to run.
        /// </summary>
        internal static void RegisterFunctionMetadata(Guid instanceId, AzFunctionInfo functionInfo)
        {
            var outputBindings = functionInfo.OutputBindings;
            OutputBindingCache.AddOrUpdate(instanceId, outputBindings, (key, value) => outputBindings);
        }

        /// <summary>
        /// Helper method to clear the output binding metadata for the function that has done running.
        /// </summary>
        internal static void UnregisterFunctionMetadata(Guid instanceId)
        {
            OutputBindingCache.TryRemove(instanceId, out _);
        }
    }
}
