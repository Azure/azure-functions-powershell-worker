//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    using System;
    using System.Text.RegularExpressions;

    internal static class FunctionsWorkerRuntimeVersionValidator
    {
        public static string GetErrorMessage()
        {
            const string versionVariableName = "FUNCTIONS_WORKER_RUNTIME_VERSION";
            var requestedVersion = Environment.GetEnvironmentVariable(versionVariableName);
            return GetErrorMessage(requestedVersion, versionVariableName);
        }

        private static string GetErrorMessage(string requestedVersion, string versionVariableName)
        {
            if (requestedVersion != null
                // Assuming this code is running on Functions runtime v2, allow
                // PowerShell version 6 only (ignoring leading and trailing spaces, and the optional ~ in front of 6)
                && !Regex.IsMatch(requestedVersion, @"^\s*~?6\s*$"))
            {
                return string.Format(
                            PowerShellWorkerStrings.InvalidFunctionsWorkerRuntimeVersion,
                            versionVariableName,
                            requestedVersion);
            }

            return null;
        }
    }
}
