//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using NJsonSchema.Infrastructure;
using ILogger = Microsoft.Azure.Functions.PowerShellWorker.Utility.ILogger;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace  Microsoft.Azure.Functions.PowerShellWorker
{
    internal class RequestProcessor
    {
        private readonly FunctionLoader _functionLoader;
        private readonly MessagingStream _msgStream;
        private readonly PowerShellManagerPool _powershellPool;
        private readonly DependencyManager _dependencyManager;

        // Indicate whether the FunctionApp has been initialized.
        private bool _isFunctionAppInitialized;

        // Central repository for acquiring PowerShell modules.
        private const string Repository = "PSGallery";

        // Az module name
        private const string AzModuleName = "Az";

        // Holds the exception object if an error is encountered while
        // initializing PowerShell or downloading the function app dependencies.
        internal Exception _unrecoverableFunctionLoadException { get; set; }

        internal RequestProcessor(MessagingStream msgStream)
        {
            _msgStream = msgStream;
            _powershellPool = new PowerShellManagerPool(msgStream);
            _functionLoader = new FunctionLoader();
            _dependencyManager = new DependencyManager();
        }

        internal async Task ProcessRequestLoop()
        {
            StreamingMessage request, response;
            while (await _msgStream.MoveNext())
            {
                request = _msgStream.GetCurrentMessage();
                switch (request.ContentCase)
                {
                    case StreamingMessage.ContentOneofCase.WorkerInitRequest:
                        response = ProcessWorkerInitRequest(request);
                        break;
                    case StreamingMessage.ContentOneofCase.FunctionLoadRequest:
                        response = ProcessFunctionLoadRequest(request);
                        break;
                    case StreamingMessage.ContentOneofCase.InvocationRequest:
                        response = ProcessInvocationRequest(request);
                        break;
                    default:
                        string errorMsg = string.Format(PowerShellWorkerStrings.UnsupportedMessage, request.ContentCase);
                        throw new InvalidOperationException(errorMsg);
                }

                if (response != null)
                {
                    _msgStream.Write(response);
                }
            }
        }

        internal StreamingMessage ProcessWorkerInitRequest(StreamingMessage request)
        {
            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.WorkerInitResponse,
                out StatusResult status);

            return response;
        }

        /// <summary>
        /// Method to process a FunctionLoadRequest.
        /// FunctionLoadRequest should be processed sequentially. There is no point to process FunctionLoadRequest
        /// concurrently as a FunctionApp doesn't include a lot functions in general. Having this step sequential
        /// will make the Runspace-level initialization easier and more predictable.
        /// </summary>
        internal StreamingMessage ProcessFunctionLoadRequest(StreamingMessage request)
        {
            FunctionLoadRequest functionLoadRequest = request.FunctionLoadRequest;

            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.FunctionLoadResponse,
                out StatusResult status);
            response.FunctionLoadResponse.FunctionId = functionLoadRequest.FunctionId;

            // When a functionLoadRequest comes in, we check to see if a dependency download has failed in a previous call
            // or if PowerShell could not be initialized. If this is the case, mark this as a failed request
            // and submit the exception to the Host (runtime).
            if (_unrecoverableFunctionLoadException != null)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = _unrecoverableFunctionLoadException.ToRpcException();
                return response;
            }

            try
            {
                // If functionLoadRequest.ManagedDependencyEnabled is true,
                // process the function app dependencies defined in functionAppRoot\Requirements.psd1.
                // These dependencies are installed via Save-Module once PowerShell has been initialized.
                if (functionLoadRequest.ManagedDependencyEnabled && !DependencyManager.FunctionAppDependenciesInstalled)
                {
                    _dependencyManager.SetFunctionAppDependencies(functionLoadRequest);

                    if (DependencyManager.Dependencies?.Count > 0)
                    {
                        response.FunctionLoadResponse.IsDependencyDownloaded = true;
                    }
                }

                // Ideally, the initialization should happen when processing 'WorkerInitRequest', however, the 'WorkerInitRequest'
                // message doesn't provide the file path of the FunctionApp. That information is not available until the first
                // 'FunctionLoadRequest' comes in. Therefore, we run initialization here.
                if (!_isFunctionAppInitialized)
                {
                    FunctionLoader.SetupWellKnownPaths(functionLoadRequest, DependencyManager.DependenciesPath);
                    _powershellPool.Initialize(request.RequestId, InstallManagedDependencyModule);
                    _isFunctionAppInitialized = true;
                }

                // Load the metadata of the function.
                _functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                // If an exception is thrown while installing the function app dependencies,
                // this will get wrapped in a DependencyInstallationException.
                if (e is DependencyInstallationException)
                {
                    e = e.InnerException;
                    response.FunctionLoadResponse.IsDependencyDownloaded = false;
                }

                // If a exception takes place during PowerShell initialization or while installing
                // the function app dependencies, cache it so we can reuse it in future calls.
                _unrecoverableFunctionLoadException = e;

                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
                return response;
            }
            return response;
        }

        /// <summary>
        /// Method to process a InvocationRequest.
        /// This method checks out a worker from the pool, and then starts the actual invocation in a threadpool thread.
        /// </summary>
        internal StreamingMessage ProcessInvocationRequest(StreamingMessage request)
        {
            AzFunctionInfo functionInfo = null;
            PowerShellManager psManager = null;

            try
            {
                functionInfo = _functionLoader.GetFunctionInfo(request.InvocationRequest.FunctionId);
                psManager = _powershellPool.CheckoutIdleWorker(request, functionInfo);
                Task.Run(() => ProcessInvocationRequestImpl(request, functionInfo, psManager));
            }
            catch (Exception e)
            {
                _powershellPool.ReclaimUsedWorker(psManager);

                StreamingMessage response = NewStreamingMessageTemplate(
                    request.RequestId,
                    StreamingMessage.ContentOneofCase.InvocationResponse,
                    out StatusResult status);

                response.InvocationResponse.InvocationId = request.InvocationRequest.InvocationId;
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();

                return response;
            }

            return null;
        }

        /// <summary>
        /// Implementation method to actual invoke the corresponding function.
        /// InvocationRequest messages are processed in parallel when there are multiple PowerShellManager instances in the pool.
        /// </summary>
        private void ProcessInvocationRequestImpl(StreamingMessage request, AzFunctionInfo functionInfo, PowerShellManager psManager)
        {
            InvocationRequest invocationRequest = request.InvocationRequest;
            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.InvocationResponse,
                out StatusResult status);
            response.InvocationResponse.InvocationId = invocationRequest.InvocationId;

            try
            {
                // Invoke the function and return a hashtable of out binding data
                Hashtable results = functionInfo.Type == AzFunctionType.OrchestrationFunction
                    ? InvokeOrchestrationFunction(psManager, functionInfo, invocationRequest)
                    : InvokeSingleActivityFunction(psManager, functionInfo, invocationRequest);

                BindOutputFromResult(response.InvocationResponse, functionInfo, results);
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }
            finally
            {
                _powershellPool.ReclaimUsedWorker(psManager);
            }

            _msgStream.Write(response);
        }

        #region Helper_Methods

        /// <summary>
        /// Create an object of 'StreamingMessage' as a template, for further update.
        /// </summary>
        private StreamingMessage NewStreamingMessageTemplate(string requestId, StreamingMessage.ContentOneofCase msgType, out StatusResult status)
        {
            // Assume success. The state of the status object can be changed in the caller.
            status = new StatusResult() { Status = StatusResult.Types.Status.Success };
            var response = new StreamingMessage() { RequestId = requestId };

            switch (msgType)
            {
                case StreamingMessage.ContentOneofCase.WorkerInitResponse:
                    response.WorkerInitResponse = new WorkerInitResponse() { Result = status };
                    break;
                case StreamingMessage.ContentOneofCase.FunctionLoadResponse:
                    response.FunctionLoadResponse = new FunctionLoadResponse() { Result = status };
                    break;
                case StreamingMessage.ContentOneofCase.InvocationResponse:
                    response.InvocationResponse = new InvocationResponse() { Result = status };
                    break;
                default:
                    throw new InvalidOperationException("Unreachable code.");
            }

            return response;
        }

        /// <summary>
        /// Invoke an orchestration function.
        /// </summary>
        private Hashtable InvokeOrchestrationFunction(PowerShellManager psManager, AzFunctionInfo functionInfo, InvocationRequest invocationRequest)
        {
            throw new NotImplementedException(PowerShellWorkerStrings.DurableFunctionNotSupported);
        }

        /// <summary>
        /// Invoke a regular function or an activity function.
        /// </summary>
        private Hashtable InvokeSingleActivityFunction(PowerShellManager psManager, AzFunctionInfo functionInfo, InvocationRequest invocationRequest)
        {
            // Bundle all TriggerMetadata into Hashtable to send down to PowerShell
            var triggerMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (var dataItem in invocationRequest.TriggerMetadata)
            {
                // MapField<K, V> is case-sensitive, but powershell is case-insensitive,
                // so for keys differ only in casing, the first wins.
                if (!triggerMetadata.ContainsKey(dataItem.Key))
                {
                    triggerMetadata.Add(dataItem.Key, dataItem.Value.ToObject());
                }
            }

            return psManager.InvokeFunction(functionInfo, triggerMetadata, invocationRequest.InputData);
        }

        /// <summary>
        /// Set the 'ReturnValue' and 'OutputData' based on the invocation results appropriately.
        /// </summary>
        private void BindOutputFromResult(InvocationResponse response, AzFunctionInfo functionInfo, Hashtable results)
        {
            switch (functionInfo.Type)
            {
                case AzFunctionType.RegularFunction:
                    // Set out binding data and return response to be sent back to host
                    foreach (KeyValuePair<string, ReadOnlyBindingInfo> binding in functionInfo.OutputBindings)
                    {
                        string outBindingName = binding.Key;
                        ReadOnlyBindingInfo bindingInfo = binding.Value;

                        object outValue = results[outBindingName];
                        object transformedValue = Utils.TransformOutBindingValueAsNeeded(outBindingName, bindingInfo, outValue);
                        TypedData dataToUse = transformedValue.ToTypedData();

                        // if one of the bindings is '$return' we need to set the ReturnValue
                        if(string.Equals(outBindingName, AzFunctionInfo.DollarReturn, StringComparison.OrdinalIgnoreCase))
                        {
                            response.ReturnValue = dataToUse;
                            continue;
                        }

                        ParameterBinding paramBinding = new ParameterBinding()
                        {
                            Name = outBindingName,
                            Data = dataToUse
                        };

                        response.OutputData.Add(paramBinding);
                    }
                    break;

                case AzFunctionType.OrchestrationFunction:
                case AzFunctionType.ActivityFunction:
                    response.ReturnValue = results[AzFunctionInfo.DollarReturn].ToTypedData();
                    break;

                default:
                    throw new InvalidOperationException("Unreachable code.");
            }
        }

        /// <summary>
        /// Installs managed dependencies modules specified in Requirements.psd1 for the function app.
        /// If an exception is raised during installation, wrap it in a DependencyInstallationException.
        /// This exception will get unwrapped in caller method.
        /// </summary>
        private void InstallManagedDependencyModule(System.Management.Automation.PowerShell pwsh, ILogger logger)
        {
            try
            {
                RunInstallManagedDependencyModule(pwsh, logger);
                DependencyManager.FunctionAppDependenciesInstalled = true;
            }
            catch (Exception e)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToInstallFuncAppDependencies);
                var dependencyInstallationException = new DependencyInstallationException(errorMsg, e);
                throw dependencyInstallationException;
            }
        }

        /// <summary>
        /// Installs managed dependencies modules specified in Requirements.psd1 for the function app.
        /// </summary>
        private void RunInstallManagedDependencyModule(System.Management.Automation.PowerShell pwsh, ILogger logger)
        {
            if (DependencyManager.Dependencies == null || DependencyManager.Dependencies.Count == 0)
            {
                // If there are no dependencies to install, log and return.
                logger.Log(LogLevel.Trace, PowerShellWorkerStrings.FunctionAppDoesNotHaveDependentModulesToInstall, isUserLog: true);
                return;
            }

            // Install the dependencies
            logger.Log(LogLevel.Trace, PowerShellWorkerStrings.InstallingFunctionAppDependentModules, isUserLog: true);

            foreach (var module in DependencyManager.Dependencies)
            {
                var moduleName = module.Name;
                var path = module.Path;
                var majorVersion = module.MajorVersion;

                // Get the latest supported version for the given major version.
                var latestVersion = GetModuleLatestSupportedVersion(pwsh, logger, moduleName, majorVersion);

                if (string.IsNullOrEmpty(latestVersion))
                {
                    // If a latest version was not found, error out.
                    var errorMsg = string.Format(PowerShellWorkerStrings.CannotFindModuleVersion, moduleName, majorVersion);
                    var argException = new ArgumentException(errorMsg);
                    logger.Log(LogLevel.Error, errorMsg, argException, isUserLog: true);

                    throw argException;
                }

                if (!DependencyManagementUtils.IsValidMajorVersion(majorVersion, latestVersion))
                {
                    // The requested major version is greater than the latest major supported version.
                    var errorMsg = string.Format(PowerShellWorkerStrings.InvalidModuleMajorVersion, moduleName, majorVersion);
                    var argException = new ArgumentException(errorMsg);
                    logger.Log(LogLevel.Error, errorMsg, argException, isUserLog: true);

                    throw argException;
                }

                // Before installing the module, check to see if it is already installed at the given path.
                var moduleVersionFolderPath = Path.Combine(path, moduleName, latestVersion);
                if (Directory.Exists(moduleVersionFolderPath))
                {
                    // The latest version is already installed, log and return.
                    var logMsg = string.Format(PowerShellWorkerStrings.ModuleIsAlreadyInstalled, moduleName, latestVersion);
                    logger.Log(LogLevel.Trace, logMsg, isUserLog: true);

                    return;
                }

                // Save-Module is able to download versions of Az module side-by-size. However, the dependent modules get
                // overwritten with the latest version. As a result, the previous Az version will not longer work.
                // If this is the case, empty the Az directory before running Save-Module.
                if (moduleName.Equals(AzModuleName, StringComparison.OrdinalIgnoreCase))
                {
                    var azModulePath = Path.Join(path, moduleName);
                    DependencyManagementUtils.EmptyDirectory(azModulePath);
                }

                // Save the module to the given path
                SaveModuleCommand(pwsh, logger, moduleName, latestVersion, path);
            }
        }

        /// <summary>
        /// Downloads a PowerShell module from the PSGallery to the given path.
        /// </summary>
        internal void SaveModuleCommand(
            System.Management.Automation.PowerShell pwsh,
            ILogger logger,
            string moduleName,
            string version,
            string path)
        {
            Exception exception = null;

            // If the destination path does not exist, create it.
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            try
            {
                pwsh.AddCommand("PowerShellGet\\Save-Module")
                    .AddParameter("Repository", Repository)
                    .AddParameter("Name", moduleName)
                    .AddParameter("RequiredVersion", version)
                    .AddParameter("path", path)
                    .AddParameter("Force", true)
                    .AddParameter("ErrorAction", "Stop")
                    .InvokeAndClearCommands();
            }
            catch (Exception e)
            {
                exception = e;
                throw;
            }
            finally
            {
                if (pwsh.HadErrors)
                {
                    string errorMsg = string.Format(PowerShellWorkerStrings.FailToInstallModule, moduleName, version);
                    logger.Log(LogLevel.Error, errorMsg, exception, isUserLog: true);
                }
                else
                {
                    var message = string.Format(PowerShellWorkerStrings.ModuleHasBeenInstalled, moduleName, version);
                    logger.Log(LogLevel.Trace, message, isUserLog: true);
                }

                // Clean up
                pwsh.AddCommand("Microsoft.PowerShell.Core\\Remove-Module")
                    .AddParameter("Name", "PackageManagement, PowerShellGet")
                    .AddParameter("Force", true)
                    .AddParameter("ErrorAction", "SilentlyContinue")
                    .InvokeAndClearCommands();
            }
        }

        /// <summary>
        /// Return the latest PowerShell module version for the given module name and major version.
        /// </summary>
        internal string GetModuleLatestSupportedVersion(
            System.Management.Automation.PowerShell pwsh,
            ILogger logger,
            string moduleName,
            string majorVersion)
        {
            Exception exception = null;
            Collection<PSObject> results;
            try
            {
                results = pwsh.AddCommand("PowerShellGet\\Find-Module")
                    .AddParameter("Repository", Repository)
                    .AddParameter("Name", moduleName)
                    .AddParameter("AllVersions", true)
                    .AddParameter("ErrorAction", "Stop")
                    .InvokeAndClearCommands<PSObject>();
            }
            catch (Exception e)
            {
                exception = e;
                throw;
            }
            finally
            {
                if (pwsh.HadErrors)
                {
                    string errorMsg = string.Format(PowerShellWorkerStrings.FailToGetModuleVersionInformation, moduleName);
                    logger.Log(LogLevel.Error, errorMsg, exception, isUserLog: true);
                }
            }

            bool foundLatestVersion = false;
            var latestVersion = new Version("0.0");

            if (results?.Count > 0)
            {
                // Iterate through the list of results and find the latest supported version
                foreach (PSObject result in results)
                {
                    string versionValue = (string)result.Properties["version"].Value;
                    var version = new Version(versionValue);

                    var versionResult = version.CompareTo(latestVersion);
                    if (versionResult > 0)
                    {
                        latestVersion = version;
                        foundLatestVersion = true;
                    }
                }
            }

            if (!foundLatestVersion)
            {
                return null;
            }

            return latestVersion.ToString();
        }
    }

        #endregion
}
