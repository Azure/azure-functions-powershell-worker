//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using CSharpx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Microsoft.Azure.Functions.PowerShellWorker.Commands
{
    /// <summary>
    /// Registers an Azure Functions with the new Powershell programming model
    /// </summary>
    /// <remarks>
    /// </remarks>
    [Cmdlet(VerbsLifecycle.Register, "AzureFunction")]
    public sealed class RegisterAzureFunctionCommand : PSCmdlet
    {
        /// <summary>
        /// The name of the Azure Function to register
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// The names of the Bindings you want to associate with this Function
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public object Bindings { get; set; }

        /// <summary>
        /// ProcessRecord override.
        /// </summary>
        protected override void ProcessRecord()
        {
            WorkerIndexingHelper.RegisterFunction(Name, ProcessBindings(Bindings));
        }

        private List<string> ProcessBindings(object bindings)
        {
            bool isBindingsEnumerable = LanguagePrimitives.IsObjectEnumerable(Bindings);
            if (isBindingsEnumerable)
            {
                return (Bindings as List<object>).Select(x => x.ToString()).ToList();
            }
            else
            {
                return new List<string> { bindings.ToString() };
            }
        }
    }
}
