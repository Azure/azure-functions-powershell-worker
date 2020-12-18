//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Management.Automation;

    internal class DurableActivityErrorHandler
    {
        public static void Handle(Cmdlet cmdlet, string errorMessage)
        {
            const string ErrorId = "Functions.Durable.ActivityFailure";
            var exception = new ActivityFailureException(errorMessage);
            var errorRecord = new ErrorRecord(exception, ErrorId, ErrorCategory.NotSpecified, null);
            cmdlet.WriteError(errorRecord);
        }
    }
}
