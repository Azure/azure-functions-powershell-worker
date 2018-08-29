//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Google.Protobuf.Collections;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// Custom type represent the context of the in-coming Http request.
    /// </summary>
    public class HttpRequestContext : IEquatable<HttpRequestContext>
    {
        /// <summary>
        /// Gets the Body of the Http request.
        /// </summary>
        public object Body { get; internal set; }

        /// <summary>
        /// Gets the Headers of the Http request.
        /// </summary>
        public MapField<string, string> Headers { get; internal set; }

        /// <summary>
        /// Gets the Method of the Http request.
        /// </summary>
        public string Method { get; internal set; }

        /// <summary>
        /// Gets the Url of the Http request.
        /// </summary>
        public string Url { get; internal set; }

        /// <summary>
        /// Gets the Params of the Http request.
        /// </summary>
        public MapField<string, string> Params { get; internal set; }

        /// <summary>
        /// Gets the Query of the Http request.
        /// </summary>
        public MapField<string, string> Query { get; internal set; }

        /// <summary>
        /// Gets the RawBody of the Http request.
        /// </summary>
        public object RawBody { get; internal set; }

        /// <summary>
        /// Compare with another HttpRequestContext object.
        /// </summary>
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