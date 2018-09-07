//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.IO;
using System.Management.Automation;

using Google.Protobuf;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Newtonsoft.Json;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal static class TypeExtensions
    {
        static HttpRequestContext ToHttpRequestContext (this RpcHttp rpcHttp)
        {
            var httpRequestContext =  new HttpRequestContext
            {
                Method = rpcHttp.Method,
                Url = rpcHttp.Url
            };

            if (rpcHttp.Headers != null)
            {
                foreach (var pair in rpcHttp.Headers)
                {
                    httpRequestContext.Headers.TryAdd(pair.Key, pair.Value);
                }
            }

            if (rpcHttp.Params != null)
            {
                foreach (var pair in rpcHttp.Params)
                {
                    httpRequestContext.Params.TryAdd(pair.Key, pair.Value);
                }
            }

            if (rpcHttp.Query != null)
            {
                foreach (var pair in rpcHttp.Query)
                {
                    httpRequestContext.Query.TryAdd(pair.Key, pair.Value);
                }
            }

            if (rpcHttp.Body != null)
            {
                httpRequestContext.Body = rpcHttp.Body.ToObject();
            }

            if (rpcHttp.RawBody != null)
            {
                httpRequestContext.RawBody = rpcHttp.RawBody.ToObject();
            }

            return httpRequestContext;
        }

        public static object ToObject (this TypedData data)
        {
            if (data == null)
            {
                return null;
            }

            switch (data.DataCase)
            {
                case TypedData.DataOneofCase.Json:
                    var hashtable = JsonConvert.DeserializeObject<Hashtable>(data.Json);
                    return new Hashtable(hashtable, StringComparer.OrdinalIgnoreCase);
                case TypedData.DataOneofCase.Bytes:
                    return data.Bytes.ToByteArray();
                case TypedData.DataOneofCase.Double:
                    return data.Double;
                case TypedData.DataOneofCase.Http:
                    return data.Http.ToHttpRequestContext();
                case TypedData.DataOneofCase.Int:
                    return data.Int;
                case TypedData.DataOneofCase.Stream:
                    return data.Stream.ToByteArray();
                case TypedData.DataOneofCase.String:
                    return data.String;
                case TypedData.DataOneofCase.None:
                    return null;
                default:
                    return new InvalidOperationException("Data Case was not set.");
            }
        }

        public static RpcException ToRpcException (this Exception exception)
        {
            return new RpcException
            {
                Message = exception.Message,
                Source = exception.Source ?? "",
                StackTrace = exception.StackTrace ?? ""
            };
        }

        static RpcHttp ToRpcHttp (this HttpResponseContext httpResponseContext)
        {
            var rpcHttp = new RpcHttp
            {
                StatusCode = httpResponseContext.StatusCode
            };

            if (httpResponseContext.Body != null)
            {
                rpcHttp.Body = httpResponseContext.Body.ToTypedData();
            }

            // Add all the headers. ContentType is separated for convenience
            foreach (var item in httpResponseContext.Headers)
            {
                rpcHttp.Headers.Add(item.Key, item.Value);
            }

            // Allow the user to set content-type in the Headers
            if (!rpcHttp.Headers.ContainsKey("content-type"))
            {
                rpcHttp.Headers.Add("content-type", httpResponseContext.ContentType);
            }

            return rpcHttp;
        }

        public static TypedData ToTypedData(this object value)
        {
            TypedData typedData = new TypedData();

            if (value == null)
            {
                return typedData;
            }

            switch (value)
            {
                case double d:
                    typedData.Double = d;
                    break;
                case long l:
                    typedData.Int = l;
                    break;
                case int i:
                    typedData.Int = i;
                    break;
                case byte[] arr:
                    typedData.Bytes = ByteString.CopyFrom(arr);
                    break;
                case Stream s:
                    typedData.Stream = ByteString.FromStream(s);
                    break;
                case HttpResponseContext http:
                    typedData.Http = http.ToRpcHttp();
                    break;
                case IDictionary hashtable:
                    typedData.Json = JsonConvert.SerializeObject(hashtable);
                    break;
                default:
                    // Handle everything else as a string
                    var str = value.ToString();

                    // Attempt to parse the string into json. If it fails,
                    // fallback to storing as a string
                    try
                    {
                        JsonConvert.DeserializeObject(str);
                        typedData.Json = str;
                    }
                    catch
                    {
                        typedData.String = str;
                    }
                    break;
            }
            return typedData;
        }
    }
}
