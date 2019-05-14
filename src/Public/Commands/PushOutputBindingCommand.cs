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
    /// Sets the value for the specified output binding.
    /// </summary>
    /// <remarks>
    /// When running in the Functions runtime, this cmdlet is aware of the output bindings
    /// defined for the function that is invoking this cmdlet. Hence, it's able to decide
    /// whether an output binding accepts singleton value only or a collection of values.
    ///
    /// For example, the HTTP output binding only accepts one response object, while the
    /// queue output binding can accept one or multiple queue messages.
    ///
    /// With this knowledge, the 'Push-OutputBinding' cmdlet acts differently based on the
    /// value specified for '-Name':
    ///
    /// - If the specified name cannot be resolved to a valid output binding, then an error
    ///   will be thrown;
    ///
    /// - If the output binding corresponding to that name accepts a collection of values,
    ///   then it's allowed to call 'Push-OutputBinding' with the same name repeatedly in
    ///   the function script to push multiple values;
    ///
    /// - If the output binding corresponding to that name only accepts a singleton value,
    ///   then the second time calling 'Push-OutputBinding' with that name will result in
    ///   an error, with detailed message about why it failed.
    ///
    /// .EXAMPLE
    ///   PS > Push-OutputBinding -Name response -Value "output #1"
    ///   The output binding of "response" will have the value of "output #1"
    /// .EXAMPLE
    ///   PS > Push-OutputBinding -Name response -Value "output #2"
    ///   The output binding is 'http', which accepts a singleton value only.
    ///   So an error will be thrown from this second run.
    /// .EXAMPLE
    ///   PS > Push-OutputBinding -Name response -Value "output #3" -Clobber
    ///   The output binding is 'http', which accepts a singleton value only.
    ///   But you can use '-Clobber' to override the old value.
    ///   The output binding of "response" will now have the value of "output #3"
    /// .EXAMPLE
    ///   PS > Push-OutputBinding -Name outQueue -Value "output #1"
    ///   The output binding of "outQueue" will have the value of "output #1"
    /// .EXAMPLE
    ///   PS > Push-OutputBinding -Name outQueue -Value "output #2"
    ///   The output binding is 'queue', which accepts multiple output values.
    ///   The output binding of "outQueue" will now have a list with 2 items: "output #1", "output #2"
    /// .EXAMPLE
    ///   PS > Push-OutputBinding -Name outQueue -Value @("output #3", "output #4")
    ///   When the value is a collection, the collection will be unrolled and elements of the collection
    ///   will be added to the list. The output binding of "outQueue" will now have a list with 4 items:
    ///   "output #1", "output #2", "output #3", "output #4".
    /// </remarks>
    [Cmdlet(VerbsCommon.Push, "OutputBinding")]
    public sealed class PushOutputBindingCommand : PSCmdlet
    {
        /// <summary>
        /// The name of the output binding you want to set.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// The value of the output binding you want to set.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true)]
        public object Value { get; set; }

        /// <summary>
        /// (Optional) If specified, will force the value to be set for a specified output binding.
        /// </summary>
        [Parameter]
        public SwitchParameter Clobber { get; set; }

        private ReadOnlyBindingInfo _bindingInfo;
        private DataCollectingBehavior _behavior;
        private Hashtable _outputBindings;

        /// <summary>
        /// BeginProcessing override.
        /// </summary>
        protected override void BeginProcessing()
        {
            _bindingInfo = GetBindingInfo(Name);
            _behavior = GetDataCollectingBehavior(_bindingInfo);
            _outputBindings = FunctionMetadata.GetOutputBindingHashtable(Runspace.DefaultRunspace.InstanceId);
        }

        /// <summary>
        /// ProcessRecord override.
        /// </summary>
        protected override void ProcessRecord()
        {
            if (!_outputBindings.ContainsKey(Name))
            {
                switch (_behavior)
                {
                    case DataCollectingBehavior.Singleton:
                        _outputBindings[Name] = Value;
                        return;

                    case DataCollectingBehavior.Collection:
                        var newValue = MergeCollection(oldData: null, newData: Value);
                        _outputBindings[Name] = newValue;
                        return;

                    default:
                        throw new InvalidOperationException(
                            string.Format(PowerShellWorkerStrings.UnrecognizedBehavior, _behavior.ToString()));
                }
            }

            // Key already exists in _outputBindings
            switch (_behavior)
            {
                case DataCollectingBehavior.Singleton:
                    if (Clobber.IsPresent)
                    {
                        _outputBindings[Name] = Value;
                    }
                    else
                    {
                        string errorMsg = string.Format(PowerShellWorkerStrings.OutputBindingAlreadySet, Name, _bindingInfo.Type);
                        ErrorRecord er = new ErrorRecord(
                            new InvalidOperationException(errorMsg),
                            nameof(PowerShellWorkerStrings.OutputBindingAlreadySet),
                            ErrorCategory.InvalidOperation,
                            targetObject: _bindingInfo.Type);

                        this.ThrowTerminatingError(er);
                    }
                    break;

                case DataCollectingBehavior.Collection:
                    object oldValue = Clobber.IsPresent ? null : _outputBindings[Name];
                    object newValue = MergeCollection(oldData: oldValue, newData: Value);
                    _outputBindings[Name] = newValue;
                    break;

                default:
                    throw new InvalidOperationException(
                        string.Format(PowerShellWorkerStrings.UnrecognizedBehavior, _behavior.ToString()));
            }
        }

        /// <summary>
        /// Helper private function that resolve the name to the corresponding binding information.
        /// </summary>
        private ReadOnlyBindingInfo GetBindingInfo(string name)
        {
            Guid currentRunspaceId = Runspace.DefaultRunspace.InstanceId;
            var bindingMap = FunctionMetadata.GetOutputBindingInfo(currentRunspaceId);

            // If the instance id doesn't get us back a binding map, then we are not running in one of the PS worker's Runspace(s).
            // This could happen when a custom Runspace is created in the function script, and 'Push-OutputBinding' is called in that Runspace.
            if (bindingMap == null)
            {
                string errorMsg = PowerShellWorkerStrings.DontPushOutputOutsideWorkerRunspace;
                ErrorRecord er = new ErrorRecord(
                    new InvalidOperationException(errorMsg),
                    nameof(PowerShellWorkerStrings.DontPushOutputOutsideWorkerRunspace),
                    ErrorCategory.InvalidOperation,
                    targetObject: currentRunspaceId);

                this.ThrowTerminatingError(er);
            }

            if (!bindingMap.TryGetValue(name, out ReadOnlyBindingInfo bindingInfo))
            {
                string errorMsg = string.Format(PowerShellWorkerStrings.BindingNameNotExist, name);
                ErrorRecord er = new ErrorRecord(
                    new InvalidOperationException(errorMsg),
                    nameof(PowerShellWorkerStrings.BindingNameNotExist),
                    ErrorCategory.InvalidOperation,
                    targetObject: name);

                this.ThrowTerminatingError(er);
            }

            return bindingInfo;
        }

        /// <summary>
        /// Helper private function that maps an output binding to a data collecting behavior.
        /// </summary>
        private DataCollectingBehavior GetDataCollectingBehavior(ReadOnlyBindingInfo bindingInfo)
        {
            switch (bindingInfo.Type)
            {
                case "http": return DataCollectingBehavior.Singleton;
                case "blob": return DataCollectingBehavior.Singleton;

                case "sendGrid": return DataCollectingBehavior.Singleton;
                case "onedrive": return DataCollectingBehavior.Singleton;
                case "outlook":  return DataCollectingBehavior.Singleton;
                case "notificationHub": return DataCollectingBehavior.Singleton;

                case "excel": return DataCollectingBehavior.Collection;
                case "table": return DataCollectingBehavior.Collection;
                case "queue": return DataCollectingBehavior.Collection;
                case "eventHub": return DataCollectingBehavior.Collection;
                case "documentDB":  return DataCollectingBehavior.Collection;
                case "mobileTable": return DataCollectingBehavior.Collection;
                case "serviceBus":  return DataCollectingBehavior.Collection;
                case "signalR":     return DataCollectingBehavior.Collection;
                case "twilioSms":   return DataCollectingBehavior.Collection;
                case "graphWebhookSubscription": return DataCollectingBehavior.Collection;

                // Be conservative on new output bindings
                default: return DataCollectingBehavior.Singleton;
            }
        }

        /// <summary>
        /// Combine the new data with the existing data for a output binding with 'Collection' behavior.
        /// Here is what this command does:
        /// - when there is no existing data
        ///   - if the new data is considered enumerable by PowerShell,
        ///     then all its elements get added to a List[object], and that list is returned.
        ///   - otherwise, the new data is returned intact.
        ///
        /// - when there is existing data
        ///   - if the existing data is a singleton, then a List[object] is created and the existing data
        ///     is added to the list.
        ///   - otherwise, the existing data is already a List[object]
        ///   - Then, depending on whether the new data is enumerable or not, its elements or itself will also be added to the list.
        ///   - That list is returned.
        /// </summary>
        private object MergeCollection(object oldData, object newData)
        {
            bool isNewDataEnumerable = LanguagePrimitives.IsObjectEnumerable(newData);
            if (oldData == null && !isNewDataEnumerable)
            {
                return newData;
            }

            var list = oldData as List<object>;
            if (list == null)
            {
                list = new List<object>();
                if (oldData != null)
                {
                    list.Add(oldData);
                }
            }

            if (isNewDataEnumerable)
            {
                var newDataEnumerable = LanguagePrimitives.GetEnumerable(newData);
                foreach (var item in newDataEnumerable)
                {
                    list.Add(item);
                }
            }
            else
            {
                list.Add(newData);
            }

            return list;
        }

        private enum DataCollectingBehavior
        {
            Singleton,
            Collection
        }
    }
}
