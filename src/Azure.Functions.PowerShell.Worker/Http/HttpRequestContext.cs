using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class HttpRequestContext
    {
        public string Method {get; set;}
        public string Url {get; set;}
        public string OriginalUrl {get; set;}
        public MapField<string, string> Headers {get; set;}
        public MapField<string, string> Query {get; set;}
        public MapField<string, string> Params {get; set;}
        public object Body {get; set;}
        public object RawBody {get; set;}
    }
}