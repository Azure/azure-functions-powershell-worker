//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    // Represents a contract for the 
    internal interface IExternalInvoker
    {
        // Method to invoke an orchestration using the external Durable SDK
        void Invoke();
    }
}
