using System.Collections;
using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class HttpResponseContext
    {
        public string StatusCode {get; set;} = "200";
        public Hashtable Headers {get; set;} = new Hashtable();
        public object Body {get; set;}
        public string ContentType {get; set;} = "text/plain";
        public bool EnableContentNegotiation {get; set;} = false;
    }
}