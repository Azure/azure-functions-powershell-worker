//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class HttpResponseContext
    {
        public object Body {get; set;}
        public string ContentType {get; set;} = "text/plain";
        public bool EnableContentNegotiation {get; set;} = false;
        public Hashtable Headers {get; set;} = new Hashtable();
        public string StatusCode {get; set;} = "200";
    }
}