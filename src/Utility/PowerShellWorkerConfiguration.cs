//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    using System;

    internal static class PowerShellWorkerConfiguration
    {
        public static string GetString(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        public static int? GetInt(string name)
        {
            var value = GetString(name);
            return string.IsNullOrEmpty(value) ? default(int?) : int.Parse(value);
        }

        public static TimeSpan? GetTimeSpan(string name)
        {
            var value = GetString(name);
            return string.IsNullOrEmpty(value) ? default(TimeSpan?) : TimeSpan.Parse(value);
        }
    }
}
