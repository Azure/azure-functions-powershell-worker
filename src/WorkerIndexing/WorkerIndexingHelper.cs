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
        internal static IEnumerable<RpcFunctionMetadata> IndexFunctions(string baseDir)
        {
            List<RpcFunctionMetadata> indexedFunctions = new List<RpcFunctionMetadata>();

            InitialSessionState initial = InitialSessionState.CreateDefault();
            Runspace runspace = RunspaceFactory.CreateRunspace(initial);
            runspace.Open();
            System.Management.Automation.PowerShell ps = System.Management.Automation.PowerShell.Create();
            ps.Runspace = runspace;

            ps.AddCommand("Get-FunctionsMetadata").AddArgument(baseDir);
            string outputString = string.Empty;
            foreach (PSObject rawMetadata in ps.Invoke())
            {
                if (outputString != string.Empty)
                {
                    throw new Exception("Multiple results from metadata cmdlet");
                }
                outputString = rawMetadata.ToString();
                //Console.WriteLine(rawMetadata.ToString());
            }
            ps.Commands.Clear();

            List<FunctionInformation> functionInformations = JsonConvert.DeserializeObject<List<FunctionInformation>>(outputString);

            foreach(FunctionInformation fi in functionInformations)
            {
                indexedFunctions.Add(fi.ConvertToRpc());
            }

            return indexedFunctions;
        }
    }
}
