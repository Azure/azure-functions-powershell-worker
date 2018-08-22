using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class HttpResponseContext
    {
#region properties
        public string StatusCode {get; set;} = "200";
        public MapField<string, string> Headers {get; set;} = new MapField<string,string>();
        public TypedData Body {get; set;} = new TypedData { String = "" };
        public bool EnableContentNegotiation {get; set;} = false;
#endregion
#region Helper functions for user to use to set data
        public HttpResponseContext Header(string field, string value) =>
            SetHeader(field, value);
        public HttpResponseContext SetHeader(string field, string value)
        {
            Headers.Add(field, value);
            return this;
        }

        public string GetHeader(string field) =>
            Headers[field];

        public HttpResponseContext RemoveHeader(string field)
        {
            Headers.Remove(field);
            return this;
        }

        public HttpResponseContext Status(int statusCode) =>
            SetStatus(statusCode);
        public HttpResponseContext Status(string statusCode) =>
            SetStatus(statusCode);
        public HttpResponseContext SetStatus(int statusCode) =>
            SetStatus(statusCode);
        public HttpResponseContext SetStatus(string statusCode)
        {
            StatusCode = statusCode;
            return this;
        }

        public HttpResponseContext Type(string type) =>
            SetHeader("content-type", type);
        public HttpResponseContext SetContentType(string type) =>
            SetHeader("content-type", type);

        public HttpResponseContext Send(int val)
        {
            Body = new TypedData
            {
                Int = val
            };
            return this;
        }
        public HttpResponseContext Send(double val)
        {
            Body = new TypedData
            {
                Double = val
            };
            return this;
        }
        public HttpResponseContext Send(string val)
        {
            Body = new TypedData
            {
                String = val
            };
            return this;
        }
        public HttpResponseContext Json(string val) {
            Body = new TypedData
            {
                Json = val
            };
            return Type("application/json");
        }
#endregion
    }
}