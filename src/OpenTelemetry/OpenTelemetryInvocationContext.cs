using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.PowerShellWorker.OpenTelemetry
{
    internal class OpenTelemetryInvocationContext
    {
        public OpenTelemetryInvocationContext(string invocationId, string traceParent, string traceState)
        {
            InvocationId = invocationId;
            TraceParent = traceParent;
            TraceState = traceState;
        }

        public bool isValid()
        {
            return InvocationId != null && TraceParent != null;
        }

        public string InvocationId { get; set; }
        public string TraceParent { get; set; }
        public string TraceState { get; set; }
    }
}
