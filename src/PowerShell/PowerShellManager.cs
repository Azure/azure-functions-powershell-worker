//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Management.Automation.Runspaces;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    internal class PowerShellManager
    {
        private const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly static object[] s_argumentsGetJobs = new object[] { null, false, false, null };
        private readonly static MethodInfo s_methodGetJobs = typeof(JobManager).GetMethod(
            "GetJobs",
            NonPublicInstance,
            binder: null,
            callConvention: CallingConventions.Any,
            new Type[] { typeof(Cmdlet), typeof(bool), typeof(bool), typeof(string[]) },
            modifiers: null);

        private readonly ILogger _logger;
        private readonly PowerShell _pwsh;
        private bool _runspaceInited;

        /// <summary>
        /// Gets the Runspace InstanceId.
        /// </summary>
        internal Guid InstanceId => _pwsh.Runspace.InstanceId;

        /// <summary>
        /// Gets the associated logger.
        /// </summary>
        internal ILogger Logger => _logger;

        static PowerShellManager()
        {
            // Set the type accelerators for 'HttpResponseContext' and 'HttpResponseContext'.
            // We probably will expose more public types from the worker in future for the interop between worker and the 'PowerShellWorker' module.
            // But it's most likely only 'HttpResponseContext' and 'HttpResponseContext' are supposed to be used directly by users, so we only add
            // type accelerators for these two explicitly.
            var accelerator = typeof(PSObject).Assembly.GetType("System.Management.Automation.TypeAccelerators");
            var addMethod = accelerator.GetMethod("Add", new Type[] { typeof(string), typeof(Type) });
            addMethod.Invoke(null, new object[] { "HttpResponseContext", typeof(HttpResponseContext) });
            addMethod.Invoke(null, new object[] { "HttpRequestContext", typeof(HttpRequestContext) });
        }

        /// <summary>
        /// Constructor for setting the basic fields.
        /// </summary>
        private PowerShellManager(ILogger logger, PowerShell pwsh, int id)
        {
            _logger = logger;
            _pwsh = pwsh;
            _pwsh.Runspace.Name = $"PowerShellManager{id}";
        }

        /// <summary>
        /// Create a PowerShellManager instance but defer the Initialization.
        /// </summary>
        /// <remarks>
        /// This constructor is only for creating the very first PowerShellManager instance.
        /// The initialization work is deferred until all prerequisites are ready, such as
        /// the dependent modules are downloaded and all Az functions are loaded.
        /// </remarks>
        internal PowerShellManager(ILogger logger, PowerShell pwsh)
            : this(logger, pwsh, id: 1)
        {
        }

        /// <summary>
        /// Create a PowerShellManager instance and initialize it.
        /// </summary>
        internal PowerShellManager(ILogger logger, int id)
            : this(logger, Utils.NewPwshInstance(), id)
        {
            // Initialize the Runspace
            Initialize();
        }

        /// <summary>
        /// Extra initialization of the Runspace.
        /// </summary>
        internal void Initialize()
        {
            if (!_runspaceInited)
            {
                // Register stream events
                RegisterStreamEvents();
                // Deploy functions from the function App
                DeployAzFunctionToRunspace();
                // Run the profile.ps1
                InvokeProfile(FunctionLoader.FunctionAppProfilePath);

                _runspaceInited = true;
            }
        }

        /// <summary>
        /// Setup Stream event listeners.
        /// </summary>
        private void RegisterStreamEvents()
        {
            var streamHandler = new StreamHandler(_logger);
            _pwsh.Streams.Debug.DataAdding += streamHandler.DebugDataAdding;
            _pwsh.Streams.Error.DataAdding += streamHandler.ErrorDataAdding;
            _pwsh.Streams.Information.DataAdding += streamHandler.InformationDataAdding;
            _pwsh.Streams.Progress.DataAdding += streamHandler.ProgressDataAdding;
            _pwsh.Streams.Verbose.DataAdding += streamHandler.VerboseDataAdding;
            _pwsh.Streams.Warning.DataAdding += streamHandler.WarningDataAdding;
        }

        /// <summary>
        /// Create the PowerShell function that is equivalent to the 'scriptFile' when possible.
        /// </summary>
        private void DeployAzFunctionToRunspace()
        {
            foreach (AzFunctionInfo functionInfo in FunctionLoader.GetLoadedFunctions())
            {
                if (functionInfo.FuncScriptBlock != null)
                {
                    // Create PS constant function for the Az function.
                    // Constant function cannot be changed or removed, it stays till the session ends.
                    _pwsh.AddCommand("New-Item")
                            .AddParameter("Path", @"Function:\")
                            .AddParameter("Name", functionInfo.DeployedPSFuncName)
                            .AddParameter("Value", functionInfo.FuncScriptBlock)
                            .AddParameter("Options", "Constant")
                         .InvokeAndClearCommands();
                }
            }
        }

        /// <summary>
        /// This method invokes the FunctionApp's profile.ps1.
        /// </summary>
        internal void InvokeProfile(string profilePath)
        {
            Exception exception = null;
            if (profilePath == null)
            {
                string noProfileMsg = string.Format(PowerShellWorkerStrings.FileNotFound, "profile.ps1", FunctionLoader.FunctionAppRootPath);
                _logger.Log(LogLevel.Trace, noProfileMsg);
                return;
            }

            try
            {
                // Import-Module on a .ps1 file will evaluate the script in the global scope.
                _pwsh.AddCommand(Utils.ImportModuleCmdletInfo)
                        .AddParameter("Name", profilePath)
                        .AddParameter("PassThru", Utils.BoxedTrue)
                     .AddCommand(Utils.RemoveModuleCmdletInfo)
                        .AddParameter("Force", Utils.BoxedTrue)
                        .AddParameter("ErrorAction", "SilentlyContinue")
                     .InvokeAndClearCommands();
            }
            catch (Exception e)
            {
                exception = e;
                throw;
            }
            finally
            {
                if (_pwsh.HadErrors)
                {
                    string errorMsg = string.Format(PowerShellWorkerStrings.FailToRunProfile, profilePath);
                    _logger.Log(LogLevel.Error, errorMsg, exception, isUserOnlyLog: true);
                }
            }
        }

        /// <summary>
        /// Execution a function fired by a trigger or an activity function scheduled by an orchestration.
        /// </summary>
        internal Hashtable InvokeFunction(
            AzFunctionInfo functionInfo,
            Hashtable triggerMetadata,
            IList<ParameterBinding> inputData)
        {
            string scriptPath = functionInfo.ScriptPath;
            string entryPoint = functionInfo.EntryPoint;

            Hashtable outputBindings = FunctionMetadata.GetOutputBindingHashtable(_pwsh.Runspace.InstanceId);

            try
            {
                if (string.IsNullOrEmpty(entryPoint))
                {
                    _pwsh.AddCommand(functionInfo.DeployedPSFuncName ?? scriptPath);
                }
                else
                {
                    // If an entry point is defined, we import the script module.
                    _pwsh.AddCommand(Utils.ImportModuleCmdletInfo)
                            .AddParameter("Name", scriptPath)
                         .InvokeAndClearCommands();

                    _pwsh.AddCommand(entryPoint);
                }

                // Set arguments for each input binding parameter
                foreach (ParameterBinding binding in inputData)
                {
                    string bindingName = binding.Name;
                    if (functionInfo.FuncParameters.TryGetValue(bindingName, out PSScriptParamInfo paramInfo))
                    {
                        var bindingInfo = functionInfo.InputBindings[bindingName];
                        var valueToUse = Utils.TransformInBindingValueAsNeeded(paramInfo, bindingInfo, binding.Data.ToObject());
                        _pwsh.AddParameter(bindingName, valueToUse);
                    }
                }

                // Gives access to additional Trigger Metadata if the user specifies TriggerMetadata
                if(functionInfo.HasTriggerMetadataParam)
                {
                    _pwsh.AddParameter(AzFunctionInfo.TriggerMetadata, triggerMetadata);
                }

                Collection<object> pipelineItems = _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Trace-PipelineObject")
                                                        .InvokeAndClearCommands<object>();

                Hashtable result = new Hashtable(outputBindings, StringComparer.OrdinalIgnoreCase);

                /*
                 * TODO: See GitHub issue #82. We are not settled on how to handle the Azure Functions concept of the $returns Output Binding
                if (pipelineItems != null && pipelineItems.Count > 0)
                {
                    // If we would like to support Option 1 from #82, use the following 3 lines of code:                    
                    object[] items = new object[pipelineItems.Count];
                    pipelineItems.CopyTo(items, 0);
                    result.Add(AzFunctionInfo.DollarReturn, items);

                    // If we would like to support Option 2 from #82, use this line:
                    result.Add(AzFunctionInfo.DollarReturn, pipelineItems[pipelineItems.Count - 1]);
                }
                */

                return result;
            }
            finally
            {
                outputBindings.Clear();
                ResetRunspace();
            }
        }

        private void ResetRunspace()
        {
            var jobs = (List<Job2>)s_methodGetJobs.Invoke(_pwsh.Runspace.JobManager, s_argumentsGetJobs);
            if (jobs != null && jobs.Count > 0)
            {
                // Clean up jobs started during the function execution.
                _pwsh.AddCommand(Utils.RemoveJobCmdletInfo)
                        .AddParameter("Force", Utils.BoxedTrue)
                        .AddParameter("ErrorAction", "SilentlyContinue")
                     .InvokeAndClearCommands(jobs);
            }

            // We need to clean up new global variables generated from the invocation.
            // After turning 'run.ps1' to PowerShell function, if '$script:<var-name>' is used, that variable
            // will be made a global variable because there is no script scope from the file.
            //
            // We don't use 'ResetRunspaceState' because it does more than needed:
            //  - reset the current path;
            //  - reset the debugger (this causes breakpoints not work properly);
            //  - create new event manager and transaction manager;
            // We should only remove the new global variables and does nothing else.
            Utils.CleanupGlobalVariables(_pwsh);
        }
    }
}
