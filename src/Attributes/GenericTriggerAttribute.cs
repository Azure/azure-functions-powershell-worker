//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// A generic trigger binding attribute for third-party or unsupported trigger types.
    /// Use this when no built-in trigger attribute exists for your binding extension.
    /// </summary>
    /// <example>
    /// param(
    ///     [GenericTrigger(Type = "kafkaTrigger", Connection = "KafkaConn",
    ///         Properties = @("brokerList=myBroker", "topic=myTopic", "consumerGroup=myGroup"))]
    ///     $Message
    /// )
    /// </example>
    public sealed class GenericTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <summary>
        /// The binding type string (e.g., "kafkaTrigger", "daprBindingTrigger").
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
