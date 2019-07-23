//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using System.Net.Http;
    using System.Xml;

    using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    internal class PowerShellGalleryModuleProvider : IModuleProvider
    {
        /// <summary>
        /// Returns the latest module version from the PSGallery for the given module name and major version.
        /// </summary>
        public string GetLatestPublishedModuleVersion(string moduleName, string majorVersion)
        {
            // The PowerShellGallery uri to query for the latest module version.
            const string PowerShellGalleryFindPackagesByIdUri =
                "https://www.powershellgallery.com/api/v2/FindPackagesById()?id=";

            Uri address = new Uri($"{PowerShellGalleryFindPackagesByIdUri}'{moduleName}'");

            string latestMajorVersion = null;
            Stream stream = null;

            var retryCount = 3;
            while (true)
            {
                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = client.GetAsync(address).Result;

                        // Throw is not a successful request
                        response.EnsureSuccessStatusCode();

                        stream = response.Content.ReadAsStreamAsync().Result;
                        break;
                    }
                    catch (Exception)
                    {
                        if (retryCount <= 0)
                        {
                            throw;
                        }

                        retryCount--;
                    }
                }
            }

            if (stream != null)
            {
                // Load up the XML response
                XmlDocument doc = new XmlDocument();
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    doc.Load(reader);
                }

                // Add the namespaces for the gallery xml content
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("ps", "http://www.w3.org/2005/Atom");
                nsmgr.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices");
                nsmgr.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

                // Find the version information
                XmlNode root = doc.DocumentElement;
                var props = root.SelectNodes("//m:properties/d:Version", nsmgr);

                if (props != null && props.Count > 0)
                {
                    foreach (XmlNode prop in props)
                    {
                        if (prop.FirstChild.Value.StartsWith(majorVersion))
                        {
                            latestMajorVersion = prop.FirstChild.Value;
                        }
                    }
                }
            }

            return latestMajorVersion;
        }

        /// <summary>
        /// Save the specified module locally.
        /// </summary>
        public void SaveModule(PowerShell pwsh, string moduleName, string version, string path)
        {
            // Save-Module cmdlet name.
            const string SaveModuleCmdletName = "PowerShellGet\\Save-Module";

            // Central repository for acquiring PowerShell modules.
            const string Repository = "PSGallery";

            pwsh.AddCommand(SaveModuleCmdletName)
                .AddParameter("Repository", Repository)
                .AddParameter("Name", moduleName)
                .AddParameter("RequiredVersion", version)
                .AddParameter("AllowPrerelease", Utils.BoxedTrue)
                .AddParameter("Path", path)
                .AddParameter("Force", Utils.BoxedTrue)
                .AddParameter("ErrorAction", "Stop")
                .InvokeAndClearCommands();
        }

        /// <summary>
        /// Remove modules that might have been imported for the purpose
        /// of saving modules from the PowerShell gallery.
        /// </summary>
        public void Cleanup(PowerShell pwsh)
        {
            pwsh.AddCommand(Utils.RemoveModuleCmdletInfo)
                .AddParameter("Name", "PackageManagement, PowerShellGet")
                .AddParameter("Force", Utils.BoxedTrue)
                .AddParameter("ErrorAction", "SilentlyContinue")
                .InvokeAndClearCommands();
        }
    }
}
