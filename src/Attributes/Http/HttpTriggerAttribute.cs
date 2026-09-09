//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines an HTTP trigger binding for a V2 PowerShell function.
    /// </summary>
    /// <example>
    /// param(
    ///     [HttpTrigger(AuthLevel = "Anonymous", Methods = @("GET", "POST"), Route = "hello")]
    ///     $Request
    /// )
    /// </example>
    public sealed class HttpTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "httpTrigger";
        /// <inheritdoc />
        public override string ImplicitOutputBindingType => "http";
        /// <inheritdoc />
        public override string ArrayProperties => "Methods";

        /// <summary>
        /// The authorization level for the HTTP trigger.
        /// Valid values: "Anonymous", "Function", "Admin". Defaults to "Function".
        /// </summary>
        public string AuthLevel { get; set; } = "Function";

        /// <summary>
        /// The HTTP methods that the function responds to.
        /// If not specified, the function responds to all methods.
        /// </summary>
        public string Methods { get; set; }

        /// <summary>
        /// The route template for the HTTP trigger.
        /// If not specified, the function name is used as the route.
        /// </summary>
        public string Route { get; set; }
    }
}
