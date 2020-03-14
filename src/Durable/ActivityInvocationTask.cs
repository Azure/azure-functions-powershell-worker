//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Mixing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    public class ActivityInvocationTask
    {
        public string Name { get; }

        public ActivityInvocationTask(string name)
        {
            Name = name;
        }
    }
}
