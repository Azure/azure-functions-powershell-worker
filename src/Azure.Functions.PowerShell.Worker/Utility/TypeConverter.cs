using System.Management.Automation;
using Google.Protobuf;
using Microsoft.Azure.Functions.PowerShellWorker;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using static Microsoft.Azure.WebJobs.Script.Grpc.Messages.TypedData;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    public class TypeConverter
    {
        public static ContextHttpRequest ToContextHttp (RpcHttp rpcHttp)
        {
            return new ContextHttpRequest
            {
                Method = rpcHttp.Method,
                Url = rpcHttp.Url,
                OriginalUrl = rpcHttp.Url,
                Headers = rpcHttp.Headers,
                Params = rpcHttp.Params,
                Body = rpcHttp.Body,
                RawBody = rpcHttp.RawBody,
                Query = rpcHttp.Query
            };
        }

        public static RpcHttp ToRpcHttp (ContextHttpResponse contextHttpResponse)
        {
            var rpcHttp = new RpcHttp
            {
                StatusCode = contextHttpResponse.StatusCode,
                Body = contextHttpResponse.Body,
                EnableContentNegotiation = contextHttpResponse.EnableContentNegotiation
            };
            rpcHttp.Headers.Add(contextHttpResponse.Headers);

            return rpcHttp;
        }

        public static object FromTypedData (TypedData data)
        {
            switch (data.DataCase)
            {
                case DataOneofCase.Json:
                    return data.Json;
                case DataOneofCase.Bytes:
                    return data.Bytes;
                case DataOneofCase.Double:
                    return data.Double;
                case DataOneofCase.Http:
                    return data.Http;
                case DataOneofCase.Int:
                    return data.Int;
                case DataOneofCase.Stream:
                    return data.Stream;
                case DataOneofCase.String:
                    return data.String;
                default:
                    // possibly throw?
                    return null;
            }
        }

        public static TypedData ToTypedData (string bindingName, BindingInfo binding, PSObject psobject)
        {
            switch (binding.Type)
            {
                case "json":

                    if(!LanguagePrimitives.TryConvertTo<string>(
                        psobject.Properties[bindingName]?.Value, 
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
                        psobject.Properties[bindingName]?.Value, 
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
                        psobject.Properties[bindingName]?.Value, 
                        out double doubleVal))
                    {
                        throw new PSInvalidCastException();
                    }
                    return new TypedData()
                    {
                        Double = doubleVal
                    };

                case "http":

                    if(!LanguagePrimitives.TryConvertTo<RpcHttp>(
                        psobject.Properties[bindingName]?.Value, 
                        out RpcHttp httpVal))
                    {
                        throw new PSInvalidCastException();
                    }
                    return new TypedData()
                    {
                        Http = httpVal
                    };
                
                case "int":

                    if(!LanguagePrimitives.TryConvertTo<int>(
                        psobject.Properties[bindingName]?.Value, 
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
                        psobject.Properties[bindingName]?.Value, 
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
                        psobject.Properties[bindingName]?.Value, 
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
    }
}