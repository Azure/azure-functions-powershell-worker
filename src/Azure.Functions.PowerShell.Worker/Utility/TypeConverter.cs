using System.Management.Automation;
using Google.Protobuf;
using Microsoft.Azure.Functions.PowerShellWorker;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Net.Http;
using static Microsoft.Azure.WebJobs.Script.Grpc.Messages.TypedData;
using System;
using Newtonsoft.Json;
using System.Collections;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    public class TypeConverter
    {
        public static object ToObject (TypedData data)
        {
            switch (data.DataCase)
            {
                case DataOneofCase.Json:
                    // consider doing ConvertFrom-Json
                    return data.Json;
                case DataOneofCase.Bytes:
                    return data.Bytes;
                case DataOneofCase.Double:
                    return data.Double;
                case DataOneofCase.Http:
                    return ToHttpContext(data.Http);
                case DataOneofCase.Int:
                    return data.Int;
                case DataOneofCase.Stream:
                    return data.Stream;
                case DataOneofCase.String:
                    return data.String;
                case DataOneofCase.None:
                    return null;
                default:
                    return new InvalidOperationException("Data Case was not set.");
            }
        }

        public static TypedData ToTypedData(object value)
        {
            TypedData typedData = new TypedData();

            if (value == null)
            {
                return typedData;
            }

            if (LanguagePrimitives.TryConvertTo<byte[]>(
                        value, out byte[] arr))
            {
                typedData.Bytes = ByteString.CopyFrom(arr);
            }
            else if(LanguagePrimitives.TryConvertTo<HttpResponseContext>(
                        value, out HttpResponseContext http))
            {
                typedData.Http = ToRpcHttp(http);
            }
            else if (LanguagePrimitives.TryConvertTo<Hashtable>(
                        value, out Hashtable hashtable))
            {
                    typedData.Json = JsonConvert.SerializeObject(hashtable);
            }
            else if (LanguagePrimitives.TryConvertTo<string>(
                        value, out string str))
            {
                try
                {
                    typedData.Json = JsonConvert.SerializeObject(str);
                }
                catch
                {
                    typedData.String = str;
                }
            }
            return typedData;
        }

        public static HttpRequestContext ToHttpContext (RpcHttp rpcHttp)
        {
            var httpRequestContext =  new HttpRequestContext
            {
                Method = rpcHttp.Method,
                Url = rpcHttp.Url,
                OriginalUrl = rpcHttp.Url,
                Headers = rpcHttp.Headers,
                Params = rpcHttp.Params,
                Query = rpcHttp.Query
            };

            if (rpcHttp.Body != null)
            {
                httpRequestContext.Body = ToObject(rpcHttp.Body);
            }

            if (rpcHttp.RawBody != null)
            {
                httpRequestContext.Body = ToObject(rpcHttp.RawBody);
            }

            return httpRequestContext;
        }

        public static RpcHttp ToRpcHttp (HttpResponseContext httpResponseContext)
        {
            var rpcHttp = new RpcHttp
            {
                StatusCode = httpResponseContext.StatusCode?? "200"
            };

            if (httpResponseContext.Body != null)
            {
                rpcHttp.Body = httpResponseContext.Body;
            }

            rpcHttp.Headers.Add(httpResponseContext.Headers);

            return rpcHttp;
        }

        public static RpcException ToRpcException (Exception exception)
        {
            return new RpcException
            {
                Message = exception?.Message,
                Source = exception?.Source ?? "",
                StackTrace = exception?.StackTrace ?? ""
            };
        }
    }
}