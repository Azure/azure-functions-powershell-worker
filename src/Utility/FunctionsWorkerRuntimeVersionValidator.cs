//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    using System;
    using System.Text.RegularExpressions;

    internal static class FunctionsWorkerRuntimeVersionValidator
    {
        private const string VersionVariableName = "FUNCTIONS_WORKER_RUNTIME_VERSION";

        public static string GetErrorMessage()
        {
            var requestedVersion = Environment.GetEnvironmentVariable(VersionVariableName);
            return GetErrorMessage(requestedVersion);
        }

        internal static string GetErrorMessage(string requestedVersion)
        {
            if (requestedVersion != null
                // Assuming this code is running on Functions runtime v2, allow
                // PowerShell version 6 only (ignoring leading and trailing spaces, and the optional ~ in front of 6)
                && !Regex.IsMatch(requestedVersion, @"^\s*~?6\s*$"))
            {
                return string.Format(
                            PowerShellWorkerStrings.InvalidFunctionsWorkerRuntimeVersion,
                            VersionVariableName,
                            requestedVersion);
            }

            return null;
        }
    }
}
