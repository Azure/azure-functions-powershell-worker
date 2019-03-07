//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Xml;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyManagementUtils
    {
        // PSGallery url to query for the latest module version.
        private const string PsGalleryUri = "https://www.powershellgallery.com/api/v2/FindPackagesById()?id='moduleName'";

        /// <summary>
        /// Returns true if the given majorVersion is less or equal to the major version in latestSupportedVersion.
        /// </summary>
        internal static bool IsValidMajorVersion(string majorVersion, string latestSupportedVersion)
        {
            // A Version object cannot be created with a single digit so add a '.0' to it.
            var requestedVersion = new Version(majorVersion + ".0");
            var latestVersion = new Version(latestSupportedVersion);

            var result = (requestedVersion.Major <= latestVersion.Major);

            return result;
        }

        /// <summary>
        /// Deletes the contents at the given directory.
        /// </summary>
        internal static void EmptyDirectory(string path)
        {
            var directoryInfo = new DirectoryInfo(path);

            if (directoryInfo.Exists)
            {
                foreach (var file in directoryInfo.GetFiles())
                {
                    file.Delete();
                }

                foreach (var directory in directoryInfo.GetDirectories())
                {
                    directory.Delete(true);
                }
            }
        }

        /// <summary>
        /// Returns the latest module version from the PSGallery for the given module name and major version.
        /// </summary>
        internal static string GetModuleLatestSupportedVersion(string moduleName, string majorVersion)
        {
            var basedAddress = PsGalleryUri.Replace("moduleName", moduleName);
            var psGalleryUri = new Uri(basedAddress);
            HttpWebRequest request = WebRequest.Create(psGalleryUri) as HttpWebRequest;

            string requestContent = null;

            // Get the response response.
            using (HttpWebResponse response = request?.GetResponse() as HttpWebResponse)
            {
                var stream = response?.GetResponseStream();
                if (stream != null)
                {
                    StreamReader reader = new StreamReader(stream);
                    requestContent = reader.ReadToEnd();
                }
            }

            if (string.IsNullOrWhiteSpace(requestContent))
            {
                return null;
            }

            // Load up the XML response.
            XmlDocument doc = new XmlDocument();
            XmlReaderSettings settings = new XmlReaderSettings();
            using (XmlReader reader = XmlReader.Create(new StringReader(requestContent), settings))
            {
                doc.Load(reader);
            }

            // Add the namespaces for the gallery xml content.
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ps", "http://www.w3.org/2005/Atom");
            nsmgr.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices");
            nsmgr.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

            // Find the version information.
            XmlNode root = doc.DocumentElement;
            var props = root.SelectNodes("//m:properties/d:Version", nsmgr);

            if (props == null || props.Count == 0)
            {
                return null;
            }

            var latestVersionForMajorVersion = default(string);

            for (int i = 0; i < props.Count; i++)
            {
                if (props[i].FirstChild.Value.StartsWith(majorVersion))
                {
                    latestVersionForMajorVersion = props[i].FirstChild.Value;
                }
            }

            return latestVersionForMajorVersion;
        }
    }
}
