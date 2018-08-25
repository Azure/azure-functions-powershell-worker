//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Google.Protobuf.Collections;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class HttpRequestContext : IEquatable<HttpRequestContext>
    {
        public object Body {get; set;}
        public MapField<string, string> Headers {get; set;}
        public string Method {get; set;}
        public string Url {get; set;}
        public MapField<string, string> Params {get; set;}
        public MapField<string, string> Query {get; set;}
        public object RawBody {get; set;}

        public bool Equals(HttpRequestContext other)
        {
            return Method == other.Method
                && Url == other.Url
                && Headers.Equals(other.Headers)
                && Params.Equals(other.Params)
                && Query.Equals(other.Query)
                && (Body == other.Body || Body.Equals(other.Body))
                && (RawBody == other.RawBody || RawBody.Equals(other.RawBody));
        }
    }
}