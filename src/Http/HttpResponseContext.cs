//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Net;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// Custom type represent the context of the Http response.
    /// </summary>
    public class HttpResponseContext
    {
        /// <summary>
        /// Gets or sets the Body of the Http response.
        /// </summary>
        public object Body { get; set; }

        /// <summary>
        /// Gets or sets the ContentType of the Http response.
        /// </summary>
        public string ContentType { get; set; } = "text/plain";

        /// <summary>
        /// Gets or sets the EnableContentNegotiation of the Http response.
        /// </summary>
        public bool EnableContentNegotiation { get; set; } = false;

        /// <summary>
        /// Gets or sets the Headers of the Http response.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the StatusCode of the Http response.
        /// </summary>
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    }
}
