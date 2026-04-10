//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Marks a PowerShell function for indexing as an Azure Function (V2 programming model).
    /// Apply this attribute to the function's [CmdletBinding()] or param block.
    /// </summary>
    /// <example>
    /// function HttpExample {
    ///     [AzFunction()]
    ///     param(
    ///         [HttpTrigger(AuthLevel = "Anonymous", Route = "hello")]
    ///         $Request
    ///     )
    ///     ...
    /// }
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class AzFunctionAttribute : Attribute
    {
        /// <summary>
        /// Optional function name override. If not specified, the PowerShell function name is used.
        /// </summary>
        public string Name { get; set; }
    }
}
