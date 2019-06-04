//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyManagementUtils
    {
        // The PowerShellGallery uri to query for the latest module version.
        private const string PowerShellGalleryFindPackagesByIdUri = "https://www.powershellgallery.com/api/v2/FindPackagesById()?id=";

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
                    IEnumerable<string> files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);

                        // Remove any problematic file attributes.
                        fileInfo.Attributes = fileInfo.Attributes &
                                              ~(FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System);
                        fileInfo.Delete();
                    }

                    foreach (DirectoryInfo subDirectory in directoryInfo.GetDirectories())
                    {
                        subDirectory.Delete(true);
                    }
                }
            }
            catch (Exception)
            {
                var errorMsg = string.Format(PowerShellWorkerStrings.FailToClenupModuleDestinationPath, path);
                throw new InvalidOperationException(errorMsg);
            }
        }

        /// <summary>
        /// Sets/prepares the destination path where the function app dependencies will be installed.
        /// </summary>
        internal static void SetDependenciesDestinationPath(string path)
        {
            // Save-Module supports downloading side-by-size module versions. However, we only want to keep one version at the time.
            // If the ManagedDependencies folder exits, remove all its contents.
            if (Directory.Exists(path))
            {
                EmptyDirectory(path);
            }
            else
            {
                // If the destination path does not exist, create it.
                // If the user does not have write access to the path, an exception will be raised.
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception e)
                {
                    var errorMsg = string.Format(PowerShellWorkerStrings.FailToCreateFunctionAppDependenciesDestinationPath, path, e.Message);
                    throw new InvalidOperationException(errorMsg);
                }
            }
        }


        /// <summary>
        /// Returns the latest module version from the PSGallery for the given module name and major version.
        /// </summary>
        internal static string GetModuleLatestSupportedVersion(string moduleName, string majorVersion)
        {
            Uri address = new Uri($"{PowerShellGalleryFindPackagesByIdUri}'{moduleName}'");
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
                        if (response != null)
                        {
                            // Load up the XML response
                            XmlDocument doc = new XmlDocument();
                            using (XmlReader reader = XmlReader.Create(response.GetResponseStream()))
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
                            break;
                        }
                    }
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
