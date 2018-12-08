//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    /// <summary>
    /// The implementation of 'Write-FunctionOutput'.
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "FunctionOutput", DefaultParameterSetName = TeeOutputParameterSet)]
    public class WriteFunctionOutputCommand : PSCmdlet
    {
        private const string TeeOutputParameterSet = "TeeOutput";
        private const string TraceOutputParameterSet = "TraceOutput";
        internal const string OutputTag = "__FunctionOutput__";

        /// <summary>
        /// Object to process.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public object InputObject { get; set; }

        /// <summary>
        /// Indicate that this cmdlet is going to tee the input.
        /// </summary>
        [Parameter(ParameterSetName = TraceOutputParameterSet)]
        public SwitchParameter Trace { get; set; }

        private SteppablePipeline _stepPipeline = null;
        private static string[] _tags = new string[] { OutputTag };

        /// <summary>
        /// BeginProcessing method.
        /// </summary>
        protected override void BeginProcessing()
        {
            if (ParameterSetName == TeeOutputParameterSet)
            {
                var commandsToStep = ScriptBlock.Create("param($outString, $writeFuncOutput) & $outString -Stream | & $writeFuncOutput -Trace");
                var outString = SessionState.InvokeCommand.GetCommand("Out-String", CommandTypes.Cmdlet);
                var writeFuncOutput = SessionState.InvokeCommand.GetCommand("Write-FunctionOutput", CommandTypes.Cmdlet);

                _stepPipeline = commandsToStep.GetSteppablePipeline(CommandOrigin.Internal, new object[] {outString, writeFuncOutput});
                _stepPipeline.Begin(this);
            }
        }

        /// <summary>
        /// ProcessRecord method.
        /// </summary>
        protected override void ProcessRecord()
        {
            if (ParameterSetName == TeeOutputParameterSet)
            {
                _stepPipeline.Process(InputObject);
                WriteObject(InputObject);
            }
            else
            {
                WriteInformation(InputObject, _tags);
            }
        }

        /// <summary>
        /// EndProcessing method.
        /// </summary>
        protected override void EndProcessing()
        {
            if (ParameterSetName == TeeOutputParameterSet)
            {
                _stepPipeline.End();
            }
        }
    }
}
