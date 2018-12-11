//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Google.Protobuf.Collections;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// Function metadata for the PowerShellWorker module to query.
    /// </summary>
    public static class FunctionMetadata
    {
        internal static ConcurrentDictionary<Guid, ReadOnlyDictionary<string, BindingInfo>> OutputBindingCache
            = new ConcurrentDictionary<Guid, ReadOnlyDictionary<string, BindingInfo>>();

        /// <summary>
        /// Get the binding metadata for the given Runspace instance id.
        /// </summary>
        public static ReadOnlyDictionary<string, BindingInfo> GetOutputBindingInfo(Guid runspaceInstanceId)
        {
            ReadOnlyDictionary<string, BindingInfo> outputBindings = null;
            OutputBindingCache.TryGetValue(runspaceInstanceId, out outputBindings);
            return outputBindings;
        }
    }
}
