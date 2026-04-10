//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// A generic output binding attribute for third-party or unsupported output binding types.
    /// Use this when no built-in output binding attribute exists for your binding extension.
    /// </summary>
    /// <example>
    /// param(
    ///     [GenericOutputBinding(Type = "daprPublish", Connection = "DaprConn",
    ///         Properties = @("pubSubName=myPubSub", "topic=myTopic"))]
    ///     $Output
    /// )
    /// </example>
    public sealed class GenericOutputBindingAttribute : OutputBindingBaseAttribute
    {
        /// <summary>
        /// The binding type string (e.g., "daprPublish", "twilioSms").
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
