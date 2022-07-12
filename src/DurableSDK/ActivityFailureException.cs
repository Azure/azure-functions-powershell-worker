//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;

    /// <summary>
    /// Durable activity failure exception.
    /// </summary>
    public class ActivityFailureException : Exception
    {
        public ActivityFailureException()
        {
        }

        public ActivityFailureException(string message)
            : base(message)
        {
        }
        public ActivityFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
