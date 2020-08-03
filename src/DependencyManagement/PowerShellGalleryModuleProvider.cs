//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Management.Automation;
    using System.Xml;

    using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    internal class PowerShellGalleryModuleProvider : IModuleProvider
    {
        private readonly ILogger _logger;

        private readonly IPowerShellGallerySearchInvoker _searchInvoker;

        public PowerShellGalleryModuleProvider(ILogger logger, IPowerShellGallerySearchInvoker searchInvoker = null)
        {
            _logger =  logger ?? throw new ArgumentNullException(nameof(logger));
            _searchInvoker = searchInvoker ?? new PowerShellGallerySearchInvoker();
        }

        /// <summary>
        /// Returns the latest module version from the PSGallery for the given module name and major version.
        /// </summary>
        public string GetLatestPublishedModuleVersion(string moduleName, string majorVersion)
        {
            // The PowerShellGallery uri to query for the latest module version.
            const string PowerShellGalleryFindPackagesByIdUri =
                "https://www.powershellgallery.com/api/v2/FindPackagesById()?id=";

            Uri address = new Uri($"{PowerShellGalleryFindPackagesByIdUri}'{moduleName}'");

            var expectedVersionStart = majorVersion + ".";

            Version latestVersion = null;

            do
            {
                var stream = _searchInvoker.Invoke(address);
                if (stream == null)
                {
                    break;
                }

                // Load up the XML response
                XmlDocument doc = new XmlDocument();
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    doc.Load(reader);
                }

                // Add the namespaces for the gallery xml content
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("a", "http://www.w3.org/2005/Atom");
                nsmgr.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices");
                nsmgr.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

                XmlNode root = doc.DocumentElement;
                latestVersion = GetLatestVersion(root, nsmgr, expectedVersionStart, latestVersion);

                // The response may be paginated. In this case, the current page
                // contains a link to the next page.
                address = GetNextLink(root, nsmgr);
            }
            while (address != null);

            return latestVersion?.ToString();
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
                .AddParameter("ErrorAction", "Stop");

            pwsh.InvokeAndClearCommands();
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

        private static Version GetLatestVersion(
            XmlNode root, XmlNamespaceManager namespaceManager, string expectedVersionStart, Version latestVersion)
        {
            var versions = root.SelectNodes("/a:feed/a:entry/m:properties[d:IsPrerelease = \"false\"]/d:Version", namespaceManager);
            if (versions != null)
            {
                foreach (XmlNode prop in versions)
                {
                    if (prop.FirstChild.Value.StartsWith(expectedVersionStart)
                        && Version.TryParse(prop.FirstChild.Value, out var thisVersion))
                    {
                        if (latestVersion == null || thisVersion > latestVersion)
                        {
                            latestVersion = thisVersion;
                        }
                    }
                }
            }

            return latestVersion;
        }

        private static Uri GetNextLink(XmlNode root, XmlNamespaceManager namespaceManager)
        {
            var nextLink = root.SelectNodes("/a:feed/a:link[@rel=\"next\"]/@href", namespaceManager);
            return nextLink.Count == 1 ? new Uri(nextLink[0].Value) : null;
        }
    }
}
