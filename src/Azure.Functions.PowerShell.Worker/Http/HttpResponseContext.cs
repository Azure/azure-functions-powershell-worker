//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// Custom type represent the context of the Http response.
    /// </summary>
    public class HttpResponseContext : IEquatable<HttpResponseContext>
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
        public Hashtable Headers { get; set; } = new Hashtable();

        /// <summary>
        /// Gets or sets the StatusCode of the Http response.
        /// </summary>
        public string StatusCode { get; set; } = "200";

        /// <summary>
        /// Compare with another HttpResponseContext object.
        /// </summary>
        public bool Equals(HttpResponseContext other)
        {
            bool sameHeaders = true;
            foreach (DictionaryEntry dictionaryEntry in Headers)
            {
                if (!other.Headers.ContainsKey(dictionaryEntry.Key)
                    || dictionaryEntry.Value != other.Headers[dictionaryEntry.Key])
                {
                    sameHeaders = false;
                    break;
                }
            }

            return ContentType == other.ContentType
                && EnableContentNegotiation == other.EnableContentNegotiation
                && StatusCode == other.StatusCode
                && sameHeaders
                && (Body == other.Body || Body.Equals(other.Body));
        }
    }
}