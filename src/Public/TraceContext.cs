//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Google.Protobuf.Collections;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// Custom TraceContext constructed from the RpcTraceContext member received from the host.
    /// </summary>
    internal class TraceContext
    {
        public TraceContext(string traceParent, string traceState, MapField<string, string> attributes)
        {
            Traceparent = traceParent;
            Tracestate = traceState;
            Attributes = GetCaseInsensitiveAttributes(attributes);
        }

        public string Traceparent { get; }

        public string Tracestate { get; }

        public Hashtable Attributes { get; }

        private Hashtable GetCaseInsensitiveAttributes(MapField<string, string> attributes)
        {
            Hashtable caseInsensitiveAttributes = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach(KeyValuePair<string, string> keyValue in attributes)
            {
                caseInsensitiveAttributes.Add(keyValue.Key, keyValue.Value);
            }

            return caseInsensitiveAttributes;
        }
    }
}
