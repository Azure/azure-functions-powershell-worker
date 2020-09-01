//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    internal class InitialSessionStateProvider
    {
        private static InitialSessionState s_iss;

        public static InitialSessionState GetInstance()
        {
            if (s_iss == null)
            {
                if (FunctionLoader.FunctionAppRootPath == null)
                {
                    throw new InvalidOperationException(PowerShellWorkerStrings.FunctionAppRootNotResolved);
                }

                s_iss = InitialSessionState.CreateDefault();

                s_iss.EnvironmentVariables.Add(
                    new SessionStateVariableEntry(
                        "PSModulePath",
                        FunctionLoader.FunctionModulePath,
                        description: null));

                // Setting the execution policy on macOS and Linux throws an exception so only update it on Windows
                if (Platform.IsWindows)
                {
                    // This sets the execution policy on Windows to Unrestricted which is required to run the user's function scripts on
                    // Windows client versions. This is needed if a user is testing their function locally with the func CLI.
                    s_iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
                }
            }

            return s_iss;
        }
    }
}
