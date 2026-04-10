//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// A generic input binding attribute for third-party or unsupported input binding types.
    /// Use this when no built-in input binding attribute exists for your binding extension.
    /// </summary>
    /// <example>
    /// param(
    ///     [GenericInputBinding(Type = "daprState", Connection = "DaprConn",
    ///         Properties = @("stateStore=myStore", "key=myKey"))]
    ///     $State
    /// )
    /// </example>
    public sealed class GenericInputBindingAttribute : InputBindingBaseAttribute
    {
        /// <summary>
        /// The binding type string (e.g., "daprState", "graphWebhookSubscription").
        /// </summary>
        public string Type { get; set; }

        /// <inheritdoc />
        public override string BindingType => Type;

        /// <summary>
        /// Additional binding properties as "key=value" strings.
        /// </summary>
        public string Properties { get; set; }
    }
}
