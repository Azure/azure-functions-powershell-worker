using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class ContextHttpResponse
    {
#region properties
        public string StatusCode {get; set;} = "200";
        public MapField<string, string> Headers {get; set;} = new MapField<string,string>();
        public TypedData Body {get; set;} = new TypedData { String = "" };
        public bool EnableContentNegotiation {get; set;}
#endregion
#region Helper functions for user to use to set data
        public ContextHttpResponse Header(string field, string value) =>
            SetHeader(field, value);
        public ContextHttpResponse SetHeader(string field, string value)
        {
            Headers.Add(field, value);
            return this;
        }

        public string GetHeader(string field) =>
            Headers[field];

        public ContextHttpResponse RemoveHeader(string field)
        {
            Headers.Remove(field);
            return this;
        }

        public ContextHttpResponse Status(int statusCode) =>
            SetStatus(statusCode);
        public ContextHttpResponse Status(string statusCode) =>
            SetStatus(statusCode);
        public ContextHttpResponse SetStatus(int statusCode) =>
            SetStatus(statusCode);
        public ContextHttpResponse SetStatus(string statusCode)
        {
            StatusCode = statusCode;
            return this;
        }

        public ContextHttpResponse Type(string type) =>
            SetHeader("content-type", type);
        public ContextHttpResponse SetContentType(string type) =>
            SetHeader("content-type", type);

        public ContextHttpResponse Send(int val)
        {
            Body = new TypedData
            {
                Int = val
            };
            return this;
        }
        public ContextHttpResponse Send(double val)
        {
            Body = new TypedData
            {
                Double = val
            };
            return this;
        }
        public ContextHttpResponse Send(string val)
        {
            Body = new TypedData
            {
                String = val
            };
            return this;
        }
        public ContextHttpResponse Json(string val) {
            Body = new TypedData
            {
                Json = val
            };
            return Type("application/json");
        }
#endregion
    }
}