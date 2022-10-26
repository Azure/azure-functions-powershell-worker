//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Microsoft.Azure.Functions.PowerShellWorker.WorkerIndexing
{
    internal class WorkerIndexingHelper
    {
        // TODO: Follow up with the PowerShell on why we get a CommandNotFoundException when using the module qualified cmdlet name.
        //const string GetFunctionsMetadataCmdletName = "AzureFunctions.PowerShell.SDK\\Get-FunctionsMetadata";
        const string GetFunctionsMetadataCmdletName = "Get-FunctionsMetadata";
        internal static IEnumerable<RpcFunctionMetadata> IndexFunctions(string baseDir)
        {
            List<RpcFunctionMetadata> indexedFunctions = new List<RpcFunctionMetadata>();

            // This is not the correct way to deal with getting a runspace for the cmdlet. 

            // Firstly, creating a runspace is expensive. If we are going to generate a runspace, it should be done on 
            // the function load request so that it can be created while the host is processing. 

            // Secondly, this assumes that the AzureFunctions.PowerShell.SDK module is present on the machine/VM's 
            // PSModulePath. On an Azure instance, it will not be. What we need to do here is move the call 
            // to SetupAppRootPathAndModulePath in RequestProcessor to the init request, and then use the 
            // _firstPwshInstance to invoke the Get-FunctionsMetadata command. The only issue with this is that
            // SetupAppRootPathAndModulePath needs the initial function init request in order to know if managed
            // dependencies are enabled in this function app.

            // Proposed solutions: 
            // 1. Pass ManagedDependencyEnabled flag in the worker init request
            // 2. Change the flow, so that _firstPwshInstance is initialized in worker init with the PSModulePath 
            //    assuming that managed dependencies are enabled, and then revert the PSModulePath in the first function
            //    init request should the managed dependencies not be enabled. 
            // 3. Continue using a new runspace for invoking Get-FunctionsMetadata, but initialize it in worker init and
            //    point the PsModulePath to the module path bundled with the worker. 


            InitialSessionState initial = InitialSessionState.CreateDefault();
            Runspace runspace = RunspaceFactory.CreateRunspace(initial);
            runspace.Open();
            System.Management.Automation.PowerShell _powershell = System.Management.Automation.PowerShell.Create();
            _powershell.Runspace = runspace;

            _powershell.AddCommand(GetFunctionsMetadataCmdletName).AddArgument(baseDir);
            string outputString = string.Empty;
            foreach (PSObject rawMetadata in _powershell.Invoke())
            {
                if (outputString != string.Empty)
                {
                    throw new Exception(PowerShellWorkerStrings.GetFunctionsMetadataMultipleResultsError);
                }
                outputString = rawMetadata.ToString();
            }
            _powershell.Commands.Clear();

            List<FunctionInformation> functionInformations = JsonConvert.DeserializeObject<List<FunctionInformation>>(outputString);

            foreach(FunctionInformation fi in functionInformations)
            {
                indexedFunctions.Add(fi.ConvertToRpc());
            }

            return indexedFunctions;
        }
    }
}
