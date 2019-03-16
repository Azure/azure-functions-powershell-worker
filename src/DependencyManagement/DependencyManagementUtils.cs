//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Net;
using System.Xml;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyManagementUtils
    {
        /// <summary>
        /// Deletes the contents at the given directory.
        /// </summary>
        internal static void EmptyDirectory(string path)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(path);

                if (directoryInfo.Exists)
                {
                    var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);

                        // Remove any problematic file attributes.
                        fileInfo.Attributes = fileInfo.Attributes &
                                              ~(FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System);
                        fileInfo.Delete();
                    }

                    foreach (var subDirectory in directoryInfo.GetDirectories())
                    {
                        subDirectory.Delete(true);
                    }
                }
            }
            catch (Exception)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToClenupModuleDestinationPath, path);
                var invalidOperationException = new InvalidOperationException(errorMsg);
                throw invalidOperationException;
            }
        }

        /// <summary>
        /// Returns the latest module version from the PSGallery for the given module name and major version.
        /// </summary>
        internal static string GetModuleLatestSupportedVersion(string moduleName, string majorVersion)
        {
            Uri address = new Uri("https://www.powershellgallery.com/api/v2/FindPackagesById()?id='" + moduleName + "'");
            int configuredRetries = 3;
            int noOfRetries = 1;

            string latestVersionForMajorVersion = null;

            while (noOfRetries <= configuredRetries)
            {
                try
                {
                    HttpWebRequest request = WebRequest.Create(address) as HttpWebRequest;
                    using (HttpWebResponse response = request?.GetResponse() as HttpWebResponse)
                    {
                        // Load up the XML response
                        XmlDocument doc = new XmlDocument();
                        using (XmlReader reader = XmlReader.Create(response?.GetResponseStream()))
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
                            for (int i = 0; i < props.Count; i++)
                            {
                                if (props[i].FirstChild.Value.StartsWith(majorVersion))
                                {
                                    latestVersionForMajorVersion = props[i].FirstChild.Value;
                                }
                            }
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    WebException webEx = ex as WebException;
                    if (webEx == null || noOfRetries >= configuredRetries)
                    {
                        throw;
                    }

                    // Only retry the web exception
                    if (ShouldRetry(webEx))
                    {
                        noOfRetries++;
                    }
                }
            }

            // If we could not find the latest module version error out.
            if (string.IsNullOrEmpty(latestVersionForMajorVersion))
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.CannotFindModuleVersion, moduleName, majorVersion);
                var argException = new ArgumentException(errorMsg);
                throw argException;
            }

            return latestVersionForMajorVersion;
        }

        /// <summary>
        /// Returns true if the given WebException status matches one of the following:
        /// SendFailure, ConnectFailure, UnknownError or Timeout.
        /// </summary>
        private static bool ShouldRetry(WebException webEx)
        {
            if (webEx == null)
            {
                return false;
            }

            if (webEx.Status == WebExceptionStatus.SendFailure ||
                webEx.Status == WebExceptionStatus.ConnectFailure ||
                webEx.Status == WebExceptionStatus.UnknownError ||
                webEx.Status == WebExceptionStatus.Timeout)
            {
                return true;
            }

            return false;
        }
    }
}
