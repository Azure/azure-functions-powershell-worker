//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.IO;
    using System.Net.Http;

    internal class PowerShellGallerySearchInvoker : IPowerShellGallerySearchInvoker
    {
        public Stream Invoke(Uri uri)
        {
            var retryCount = 3;
            while (true)
            {
                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = client.GetAsync(uri).Result;

                        // Throw is not a successful request
                        response.EnsureSuccessStatusCode();

                        return response.Content.ReadAsStreamAsync().Result;
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
        }
    }
}
