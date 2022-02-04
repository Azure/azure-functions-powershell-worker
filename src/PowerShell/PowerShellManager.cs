//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Azure.Functions.PowerShellWorker.Durable;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;
    using System.Text;

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

        private readonly PowerShell _pwsh;
        private bool _runspaceInited;

        private readonly ErrorRecordFormatter _errorRecordFormatter = new ErrorRecordFormatter();

        /// <summary>
        /// Gets the Runspace InstanceId.
        /// </summary>
        internal Guid InstanceId => _pwsh.Runspace.InstanceId;

        /// <summary>
        /// Gets the associated logger.
        /// </summary>
        internal ILogger Logger { get; }

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
            Logger = logger;
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
            var streamHandler = new StreamHandler(Logger, _errorRecordFormatter);
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
                Logger.Log(isUserOnlyLog: false, LogLevel.Trace, noProfileMsg);
                return;
            }

            var profileExecutionHadErrors = false;

            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Import-Module on a .ps1 file will evaluate the script in the global scope.
                _pwsh.AddCommand(Utils.ImportModuleCmdletInfo)
                        .AddParameter("Name", profilePath)
                     .AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Trace-PipelineObject")
                     .InvokeAndClearCommands();

                profileExecutionHadErrors = _pwsh.HadErrors;

                _pwsh.AddCommand(Utils.RemoveModuleCmdletInfo)
                        .AddParameter("FullyQualifiedName", profilePath)
                        .AddParameter("Force", Utils.BoxedTrue)
                        .AddParameter("ErrorAction", "SilentlyContinue")
                     .InvokeAndClearCommands();

                Logger.Log(isUserOnlyLog: false, LogLevel.Trace, string.Format(PowerShellWorkerStrings.ProfileInvocationCompleted, stopwatch.ElapsedMilliseconds));
            }
            catch (Exception e)
            {
                exception = e;
                throw;
            }
            finally
            {
                if (profileExecutionHadErrors || _pwsh.HadErrors)
                {
                    string errorMsg = string.Format(PowerShellWorkerStrings.ErrorsWhileExecutingProfile, profilePath);
                    Logger.Log(isUserOnlyLog: true, LogLevel.Error, errorMsg, exception);
                }
            }
        }

        public Hashtable InvokeFunction(
            AzFunctionInfo functionInfo,
            Hashtable triggerMetadata,
            TraceContext traceContext,
            RetryContext retryContext,
            IList<ParameterBinding> inputData,
            FunctionInvocationPerformanceStopwatch stopwatch)
        {
            var outputBindings = FunctionMetadata.GetOutputBindingHashtable(_pwsh.Runspace.InstanceId);
            var durableController = new DurableController(functionInfo.DurableFunctionInfo, _pwsh);

            try
            {

                durableController.BeforeFunctionInvocation(inputData);

                AddEntryPointInvocationCommand(functionInfo);
                stopwatch.OnCheckpoint(FunctionInvocationPerformanceStopwatch.Checkpoint.FunctionCodeReady);

                SetInputBindingParameterValues(functionInfo, inputData, durableController, triggerMetadata, traceContext, retryContext);
                stopwatch.OnCheckpoint(FunctionInvocationPerformanceStopwatch.Checkpoint.InputBindingValuesReady);

                /* This has been moved to the DF SDK (although it should also be moved down within the worker)
                 * if (!durableController.ShouldSuppressPipelineTraces())
                {
                    _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Trace-PipelineObject");
                }*/

                stopwatch.OnCheckpoint(FunctionInvocationPerformanceStopwatch.Checkpoint.InvokingFunctionCode);
                Logger.Log(isUserOnlyLog: false, LogLevel.Trace, CreateInvocationPerformanceReportMessage(functionInfo.FuncName, stopwatch));

                try
                {

                    return durableController.TryInvokeOrchestrationFunction(out var result)
                                ? result
                                : InvokeNonOrchestrationFunction(durableController, outputBindings);
                }
                catch (RuntimeException e)
                {
                    ErrorAnalysisLogger.Log(Logger, e.ErrorRecord, isException: true);
                    Logger.Log(isUserOnlyLog: true, LogLevel.Error, GetFunctionExceptionMessage(e));
                    throw;
                }
                catch (OrchestrationFailureException e)
                {
                    if (e.InnerException is IContainsErrorRecord inner)
                    {
                        Logger.Log(isUserOnlyLog: true, LogLevel.Error, GetFunctionExceptionMessage(inner));
                    }
                    throw;
                }
            }
            finally
            {
                durableController.AfterFunctionInvocation();
                outputBindings.Clear();
                ResetRunspace();
            }
        }

        private void SetInputBindingParameterValues(
            AzFunctionInfo functionInfo,
            IEnumerable<ParameterBinding> inputData,
            DurableController durableController,
            Hashtable triggerMetadata,
            TraceContext traceContext,
            RetryContext retryContext)
        {
            foreach (var binding in inputData)
            {
                if (functionInfo.FuncParameters.TryGetValue(binding.Name, out var paramInfo))
                {
                    if (!durableController.TryGetInputBindingParameterValue(binding.Name, out var valueToUse))
                    {
                        var bindingInfo = functionInfo.InputBindings[binding.Name];
                        valueToUse = Utils.TransformInBindingValueAsNeeded(paramInfo, bindingInfo, binding.Data.ToObject());
                        _pwsh.AddParameter(binding.Name, valueToUse);
                    }
                    else
                    {
                        // move this further down in the worker
                        // _pwsh.AddParameter(binding.Name, valueToUse);

                    }

                }
            }

            // Gives access to additional Trigger Metadata if the user specifies TriggerMetadata
            if (functionInfo.HasTriggerMetadataParam)
            {
                _pwsh.AddParameter(AzFunctionInfo.TriggerMetadata, triggerMetadata);
            }

            if (functionInfo.HasTraceContextParam)
            {
                _pwsh.AddParameter(AzFunctionInfo.TraceContext, traceContext);
            }

            if (functionInfo.HasRetryContextParam)
            {
                _pwsh.AddParameter(AzFunctionInfo.RetryContext, retryContext);
            }
        }

        /// <summary>
        /// Execution a function fired by a trigger or an activity function scheduled by an orchestration.
        /// </summary>
        private Hashtable InvokeNonOrchestrationFunction(DurableController durableController, IDictionary outputBindings)
        {
            var pipelineItems = _pwsh.InvokeAndClearCommands<object>();
            var result = new Hashtable(outputBindings, StringComparer.OrdinalIgnoreCase);
            durableController.AddPipelineOutputIfNecessary(pipelineItems, result);
            return result;
        }

        private void AddEntryPointInvocationCommand(AzFunctionInfo functionInfo)
        {
            if (string.IsNullOrEmpty(functionInfo.EntryPoint))
            {
                _pwsh.AddCommand(functionInfo.DeployedPSFuncName ?? functionInfo.ScriptPath);
            }
            else
            {
                // If an entry point is defined, we import the script module.
                _pwsh.AddCommand(Utils.ImportModuleCmdletInfo)
                    .AddParameter("Name", functionInfo.ScriptPath)
                    .InvokeAndClearCommands();

                _pwsh.AddCommand(functionInfo.EntryPoint);
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

        private string GetFunctionExceptionMessage(IContainsErrorRecord exception)
        {
            return $"EXCEPTION: {_errorRecordFormatter.Format(exception.ErrorRecord)}";
        }

        private static string CreateInvocationPerformanceReportMessage(string functionName, FunctionInvocationPerformanceStopwatch stopwatch)
        {
            var performanceDetails = FormatPerformanceDetails(stopwatch);

            return string.Format(
                PowerShellWorkerStrings.FunctionInvocationStarting,
                functionName,
                stopwatch.TotalMilliseconds,
                performanceDetails);
        }

        private static StringBuilder FormatPerformanceDetails(FunctionInvocationPerformanceStopwatch stopwatch)
        {
            var performanceDetails = new StringBuilder(1024);
            foreach (var (key, value) in stopwatch.CheckpointMilliseconds)
            {
                performanceDetails.Append(key);
                performanceDetails.Append(": ");
                performanceDetails.Append(value);
                performanceDetails.Append(" ms; ");
            }

            return performanceDetails;
        }
    }
}
