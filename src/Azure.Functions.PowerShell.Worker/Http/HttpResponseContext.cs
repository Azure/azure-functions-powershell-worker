//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class HttpResponseContext : IEquatable<HttpResponseContext>
    {
        public object Body {get; set;}
        public string ContentType {get; set;} = "text/plain";
        public bool EnableContentNegotiation {get; set;} = false;
        public Hashtable Headers {get; set;} = new Hashtable();
        public string StatusCode {get; set;} = "200";

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