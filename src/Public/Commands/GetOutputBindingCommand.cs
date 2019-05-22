//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Microsoft.Azure.Functions.PowerShellWorker.Commands
{
    /// <summary>
    /// Gets the hashtable of the output bindings set so far.
    /// </summary>
    /// <remarks>
    /// .EXAMPLE
    ///   PS > Get-OutputBinding
    ///   Gets the hashtable of all the output bindings set so far.
    /// .EXAMPLE
    ///   PS > Get-OutputBinding -Name res
    ///   Gets the hashtable of specific output binding.
    /// .EXAMPLE
    ///   PS > Get-OutputBinding -Name r*
    ///   Gets the hashtable of output bindings that match the wildcard.
    /// </remarks>
    [Cmdlet(VerbsCommon.Get, "OutputBinding")]
    public sealed class GetOutputBindingCommand : PSCmdlet
    {
        /// <summary>
        /// The name of the output binding you want to get. Supports wildcards.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [SupportsWildcards]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; } = "*";

        /// <summary>
        /// Clear all stored output binding values.
        /// </summary>
        [Parameter]
        public SwitchParameter Purge { get; set; }

        private Hashtable _outputBindings;
        private Hashtable _retHashtable;

        /// <summary>
        /// BeginProcessing override.
        /// </summary>
        protected override void BeginProcessing()
        {
            _retHashtable = new Hashtable(StringComparer.OrdinalIgnoreCase);
            _outputBindings = FunctionMetadata.GetOutputBindingHashtable(Runspace.DefaultRunspace.InstanceId);
        }

        /// <summary>
        /// ProcessRecord override.
        /// </summary>
        protected override void ProcessRecord()
        {
            if (_outputBindings == null || _outputBindings.Count == 0)
            {
                return;
            }

            var namePattern = new WildcardPattern(Name);
            foreach (DictionaryEntry entry in _outputBindings)
            {
                var bindingName = (string)entry.Key;

                if (namePattern.IsMatch(bindingName) && !_retHashtable.ContainsKey(bindingName))
                {
                    _retHashtable.Add(bindingName, entry.Value);
                }
            }
        }

        /// <summary>
        /// ProcessRecord override.
        /// </summary>
        protected override void EndProcessing()
        {
            if (Purge.IsPresent)
            {
                _outputBindings.Clear();
            }

            WriteObject(_retHashtable);
        }
    }
}
