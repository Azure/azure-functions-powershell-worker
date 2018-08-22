using System.Management.Automation;
using Google.Protobuf;
using Microsoft.Azure.Functions.PowerShellWorker;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Net.Http;
using static Microsoft.Azure.WebJobs.Script.Grpc.Messages.TypedData;
using System;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    public class TypeConverter
    {
        public static object FromTypedData (TypedData data)
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

        public static TypedData ToTypedData (string bindingName, BindingInfo binding, object psobject)
        {
            switch (binding.Type)
            {
                case "json":

                    if(!LanguagePrimitives.TryConvertTo<string>(
                        psobject,
                        out string jsonVal))
                    {
                        throw new PSInvalidCastException();
                    }
                    return new TypedData()
                    {
                        Json = jsonVal
                    };
                
                case "bytes":

                    if(!LanguagePrimitives.TryConvertTo<ByteString>(
                        psobject,
                        out ByteString bytesVal))
                    {
                        throw new PSInvalidCastException();
                    }
                    return new TypedData()
                    {
                        Bytes = bytesVal
                    };

                case "double":

                    if(!LanguagePrimitives.TryConvertTo<double>(
                        psobject,
                        out double doubleVal))
                    {
                        throw new PSInvalidCastException();
                    }
                    return new TypedData()
                    {
                        Double = doubleVal
                    };

                case "http":

                    if(!LanguagePrimitives.TryConvertTo<HttpResponseContext>(
                        psobject, 
                        out HttpResponseContext httpVal))
                    {
                        throw new PSInvalidCastException();
                    }
                    return new TypedData()
                    {
                        Http = ToRpcHttp(httpVal)
                    };
                
                case "int":

                    if(!LanguagePrimitives.TryConvertTo<int>(
                        psobject,
                        out int intVal))
                    {
                        throw new PSInvalidCastException();
                    }
                    return new TypedData()
                    {
                        Int = intVal
                    };

                case "stream":

                    if(!LanguagePrimitives.TryConvertTo<ByteString>(
                        psobject,
                        out ByteString streamVal))
                    {
                        throw new PSInvalidCastException();
                    }
                    return new TypedData()
                    {
                        Stream = streamVal
                    };

                case "string":

                    if(!LanguagePrimitives.TryConvertTo<string>(
                        psobject,
                        out string stringVal))
                    {
                        throw new PSInvalidCastException();
                    }
                    return new TypedData()
                    {
                        String = stringVal
                    };
                default:
                    throw new PSInvalidCastException("could not parse type");
            }
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
                httpRequestContext.Body = FromTypedData(rpcHttp.Body);
            }

            if (rpcHttp.RawBody != null)
            {
                httpRequestContext.Body = FromTypedData(rpcHttp.RawBody);
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