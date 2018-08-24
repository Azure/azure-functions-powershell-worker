//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Google.Protobuf.Collections;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class HttpRequestContext
    {
        public object Body {get; set;}
        public MapField<string, string> Headers {get; set;}
        public string Method {get; set;}
        public string Url {get; set;}
        public string OriginalUrl {get; set;}
        public MapField<string, string> Params {get; set;}
        public MapField<string, string> Query {get; set;}
        public object RawBody {get; set;}
    }
}