//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PowerShell.Commands;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    internal class Utils
    {
        internal readonly static CmdletInfo ImportModuleCmdletInfo = new CmdletInfo("Import-Module", typeof(ImportModuleCommand));
        internal readonly static CmdletInfo RemoveModuleCmdletInfo = new CmdletInfo("Remove-Module", typeof(RemoveModuleCommand));
        internal readonly static CmdletInfo RemoveJobCmdletInfo = new CmdletInfo("Remove-Job", typeof(RemoveJobCommand));
        internal readonly static CmdletInfo OutStringCmdletInfo = new CmdletInfo("Out-String", typeof(OutStringCommand));
        internal readonly static CmdletInfo WriteInformationCmdletInfo = new CmdletInfo("Write-Information", typeof(WriteInformationCommand));

        internal readonly static object BoxedTrue = (object)true;
        internal readonly static object BoxedFalse = (object)false;

        private const string VariableDriveRoot = @"Variable:\";
        private static InitialSessionState s_iss;
        private static HashSet<string> s_globalVariables;

        /// <summary>
        /// Create a new PowerShell instance using our singleton InitialSessionState instance.
        /// </summary>
        internal static PowerShell NewPwshInstance()
        {
            if (s_iss == null)
            {
                s_iss = InitialSessionState.CreateDefault();

                if (!AreDurableFunctionsEnabled())
                {
                    // TODO: This is probably not necessary, but this is how it was
                    // before introducing durable functions, so leaving it here until we test thoroughly.
                    s_iss.ThreadOptions = PSThreadOptions.UseCurrentThread;
                }

                if (FunctionLoader.FunctionAppRootPath != null)
                {
                    s_iss.EnvironmentVariables.Add(
                        new SessionStateVariableEntry(
                            "PSModulePath",
                            FunctionLoader.FunctionModulePath,
                            description: null));
                }

                // Setting the execution policy on macOS and Linux throws an exception so only update it on Windows
                if(Platform.IsWindows)
                {
                    // This sets the execution policy on Windows to Unrestricted which is required to run the user's function scripts on
                    // Windows client versions. This is needed if a user is testing their function locally with the func CLI.
                    s_iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
                }
            }

            var pwsh = PowerShell.Create(s_iss);
            if (s_globalVariables == null)
            {
                // Get the names of the built-in global variables
                ICollection<PSVariable> globalVars = GetGlobalVariables(pwsh);
                s_globalVariables = new HashSet<string>(globalVars.Count + 3, StringComparer.OrdinalIgnoreCase)
                {
                    // These 3 variables are not in the built-in variables in a fresh Runspace,
                    // but they show up after we evaluate the 'profile.ps1' in the global scope.
                    "PSScriptRoot", "PSCommandPath", "MyInvocation"
                };

                foreach (PSVariable var in globalVars)
                {
                    s_globalVariables.Add(var.Name);
                }
            }

            return pwsh;
        }

        /// <summary>
        /// Clean up the global variables added by the function invocation.
        /// </summary>
        internal static void CleanupGlobalVariables(PowerShell pwsh)
        {
            List<string> varsToRemove = null;
            ICollection<PSVariable> globalVars = GetGlobalVariables(pwsh);

            foreach (PSVariable var in globalVars)
            {
                // The variable is one of the built-in global variables.
                if (s_globalVariables.Contains(var.Name)) { continue; }

                // We cannot remove a constant variable, so leave it as is.
                if (var.Options.HasFlag(ScopedItemOptions.Constant)) { continue; }

                // The variable is exposed by a module. We don't remove modules, so leave it as is.
                if (var.Module != null) { continue; }

                // The variable is not a regular PSVariable.
                // It's likely not created by the user, so leave it as is.
                if (var.GetType() != typeof(PSVariable)) { continue; }

                if (varsToRemove == null)
                {
                    // Create a list only if it's needed.
                    varsToRemove = new List<string>();
                }

                // Add the variable path.
                varsToRemove.Add($"{VariableDriveRoot}{var.Name}");
            }

            if (varsToRemove != null)
            {
                // Remove the global variable added by the function invocation.
                pwsh.Runspace.SessionStateProxy.InvokeProvider.Item.Remove(
                    varsToRemove.ToArray(),
                    recurse: true,
                    force: true,
                    literalPath: true);
            }
        }

        private static ICollection<PSVariable> GetGlobalVariables(PowerShell pwsh)
        {
            PSObject item = pwsh.Runspace.SessionStateProxy.InvokeProvider.Item.Get(VariableDriveRoot)[0];
            return (ICollection<PSVariable>)item.BaseObject;
        }

        /// <summary>
        /// Helper method to do additional transformation on the input value based on the type constraints specified in the script.
        /// </summary>
        internal static object TransformInBindingValueAsNeeded(PSScriptParamInfo paramInfo, ReadOnlyBindingInfo bindingInfo, object value)
        {
            switch (bindingInfo.Type)
            {
                case "blob":
                case "blobTrigger":
                    // For blob input, the input data could be an array of bytes or a string. Documentation says
                    // that a user can specify the 'dataType' to make the input data of the expected type. (see
                    // https://docs.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings#binding-datatype-property)
                    //
                    // However, I found this setting is somewhat broken:
                    //  1. For the 'blobTrigger' type in-binding, the input data is always byte array and the host doesn't
                    //     respect the "dataType" setting in 'function.json'.
                    //  2. For the 'blob' type in-binding, the input data type is 'String' by default. When specified, the
                    //    'binary' and 'string' dataType are respected, but 'stream' dataType is not respected.
                    //
                    // Due to (1), we should be reasonably smart and transform the data appropriately when type constraint is
                    // specified on the input parameter to indicate the expected data type.
                    Type paramType = paramInfo.ParamType;
                    if (value is byte[] blobBytes)
                    {
                        if (paramType == typeof(string))
                        {
                            // String is expected, so convert the byte array to string using UTF8 encoding.
                            // This is a best effort attempt, as we don't know the true encoding.
                            value = Encoding.UTF8.GetString(blobBytes);
                        }
                        else if (paramType == typeof(Stream))
                        {
                            // Stream is expected, so convert the byte array to a MemoryStream.
                            value = new MemoryStream(blobBytes);
                        }
                    }

                    // When the input is 'String', we don't attempt to convert it to bytes because if the blob
                    // is in fact a binary file, it's impossible to get the right bytes back even if we use the
                    // same encoding used by the host when converting those bytes to string.

                    break;
                default:
                    break;
            }

            return value;
        }

        /// <summary>
        /// Helper method to do additional transformation on the output value.
        /// </summary>
        internal static object TransformOutBindingValueAsNeeded(string bindingName, ReadOnlyBindingInfo bindingInfo, object value)
        {
            switch (bindingInfo.Type)
            {
                case "http":
                    // Try converting the value to HttpResponseContext if it's not already an object of such type.
                    if (value is HttpResponseContext)
                    {
                        break;
                    }

                    try
                    {
                        value = LanguagePrimitives.ConvertTo<HttpResponseContext>(value);
                    }
                    catch (PSInvalidCastException ex)
                    {
                        string errorMsg = string.Format(PowerShellWorkerStrings.FailToConvertToHttpResponseContext, bindingName, ex.Message);
                        throw new InvalidOperationException(errorMsg);
                    }

                    break;
                default:
                    break;
            }

            return value;
        }

        internal static bool AreDurableFunctionsEnabled()
        {
            return PowerShellWorkerConfiguration.GetBoolean("PSWorkerEnableExperimentalDurableFunctions") ?? true;
        }

        internal static string GetPowerShellVersion(PowerShell pwsh)
        {
            const string versionTableVariableName = "PSVersionTable";
            const string versionPropertyName = "PSVersion";

            var versionTableVariable = GetGlobalVariables(pwsh).First(v => string.CompareOrdinal(v.Name, versionTableVariableName) == 0);
            var versionTable = (PSVersionHashTable)versionTableVariable.Value;
            return versionTable[versionPropertyName].ToString();
        }
    }
}
