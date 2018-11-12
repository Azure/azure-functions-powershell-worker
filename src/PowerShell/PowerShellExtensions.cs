//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.ObjectModel;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    internal static class PowerShellExtensions
    {
        public static void InvokeAndClearCommands(this PowerShell pwsh)
        {
            try
            {
                pwsh.Invoke();
            }
            finally
            {
                pwsh.Streams.ClearStreams();
                pwsh.Commands.Clear();
            }
        }

        public static Collection<T> InvokeAndClearCommands<T>(this PowerShell pwsh)
        {
            try
            {
                var result = pwsh.Invoke<T>();
                return result;
            }
            finally
            {
                pwsh.Streams.ClearStreams();
                pwsh.Commands.Clear();
            }
        }

        public static string FormatObjectToString(this PowerShell pwsh, object inputObject)
        {
            // PowerShell's `Out-String -InputObject` handles collections differently
            // than when receiving InputObjects from the pipeline. (i.e. `$collection | Out-String`).
            // That is why we need `Write-Output` here. See related GitHub issue here:
            // https://github.com/PowerShell/PowerShell/issues/8246
            return pwsh.AddCommand("Write-Output")
                .AddParameter("InputObject", inputObject)
                .AddCommand("Out-String")
                .InvokeAndClearCommands<string>()[0];
        }
    }
}
