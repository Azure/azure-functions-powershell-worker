//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Management.Automation;

    internal class DurableActivityErrorHandler
    {
        public static void Handle(Cmdlet cmdlet, string errorMessage)
        {
            CreateAndWriteError(errorMessage, cmdlet.WriteError);
        }

        internal static void CreateAndWriteError(string errorMessage, Action<ErrorRecord> writeError)
        {
            // This error is supposed to be documented and become a part of the public contract.
            // When Function authors implement error handling in PowerShell, they may rely on
            // the structure and the content of this error, specifically:
            //   - ErrorId
            //   - Exception type
            //   - Exception message
            //   - Error category
            // If you make changes in any of these, make sure they are backward-compatible,
            // so the existing Functions are not.

            const string ErrorId = "Functions.Durable.ActivityFailure";
            var exception = new ActivityFailureException(errorMessage);
            var errorRecord = new ErrorRecord(exception, ErrorId, ErrorCategory.NotSpecified, null);
            writeError(errorRecord);
        }
    }
}
