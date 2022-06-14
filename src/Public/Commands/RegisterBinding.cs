//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System;
using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Functions.PowerShellWorker.Commands
{
    /// <summary>
    /// Registers an Azure Functions with the new Powershell programming model
    /// </summary>
    /// <remarks>
    /// </remarks>
    [Cmdlet(VerbsLifecycle.Register, "Binding")]
    public sealed class RegisterBindingCommand : PSCmdlet
    {
        /// <summary>
        /// The name of the Azure Functions Binding to register
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// The type of this binding
        /// </summary>
        [Parameter()]
        public string Type { get; set; }

        /// <summary>
        /// The direction of this binding
        /// </summary>
        [Parameter()]
        public string Direction { get; set; }

        /// <summary>
        /// The data type of this binding
        /// </summary>
        [Parameter()]
        public string DataType { get; set; }

        /// <summary>
        /// The auth level of this binding
        /// </summary>
        [Parameter()]
        public object OtherInfo { get; set; }

        /// <summary>
        /// ProcessRecord override.
        /// </summary>
        protected override void ProcessRecord()
        {
            BindingInfo info = new BindingInfo();
            info.Type = Type;
            info.Direction = ParseDirection(Direction);
            info.DataType = ParseDataType(DataType);

            WorkerIndexingHelper.RegisterBinding(Name, info);
        }

        private BindingInfo.Types.DataType ParseDataType(string dataType)
        {
            switch (dataType)
            {
                case "string":
                    return BindingInfo.Types.DataType.String;
                case "binary":
                    return BindingInfo.Types.DataType.Binary;
                case "stream":
                    return BindingInfo.Types.DataType.Stream;
                default:
                    return BindingInfo.Types.DataType.Undefined;
            }
        }

        private BindingInfo.Types.Direction ParseDirection(string direction)
        {
            switch(direction)
            {
                case "in":
                    return BindingInfo.Types.Direction.In;
                case "out":
                    return BindingInfo.Types.Direction.Out;
                case "inout":
                    return BindingInfo.Types.Direction.Inout;
                default:
                    string errorMsg = string.Format(PowerShellWorkerStrings.InvalidBindingDirection, direction);
                    ErrorRecord er = new ErrorRecord(
                        new InvalidOperationException(errorMsg),
                        nameof(PowerShellWorkerStrings.BindingNameNotExist),
                        ErrorCategory.InvalidOperation,
                        targetObject: direction);

                    this.ThrowTerminatingError(er);
                    break;
            }
            return BindingInfo.Types.Direction.Inout;
        }
    }
}
