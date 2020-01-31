//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using System.Text;

    internal class ErrorRecordFormatter
    {
        private const string TruncationPostfix = "...";

        private readonly PowerShell _pwsh = PowerShell.Create();

        /// <summary>
        /// maxSize limits the maximum size of the formatted error string (in characters).
        /// The rest will be truncated. This value should be high enough to allow the result
        /// contain the most important and relevant information, but low enough to create
        /// no problems for the communication channels used to propagate this data.
        /// The default value is somewhat arbitrary but satisfies both conditions.
        /// </summary>
        public string Format(ErrorRecord errorRecord, int maxSize = 1 * 1024 * 1024)
        {
            var errorDetails = _pwsh.AddCommand("Microsoft.PowerShell.Utility\\Get-Error")
                                    .AddParameter("InputObject", errorRecord)
                                    .AddCommand("Microsoft.PowerShell.Utility\\Out-String")
                                    .InvokeAndClearCommands<string>();

            var result = new StringBuilder(capacity: Math.Min(1024, maxSize));

            result.AppendLine(errorRecord.Exception.Message);
            result.Append(errorDetails.Single());
            if (result.Length > maxSize)
            {
                var charactersToRemove = result.Length + TruncationPostfix.Length - maxSize;
                result.Remove(result.Length - charactersToRemove, charactersToRemove);
                result.Append(TruncationPostfix);
            }

            return result.ToString();
        }
    }
}
