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
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal static class TypeExtensions
    {
        private static HttpRequestContext ToHttpRequestContext (this RpcHttp rpcHttp)
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

        internal static object ToObject(this TypedData data)
        {
            if (data == null)
            {
                return null;
            }

            switch (data.DataCase)
            {
                case TypedData.DataOneofCase.Json:
                    return DeserializeJson(data.Json);
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

        private static JsonSerializerSettings setting =
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 10 };
        private static object DeserializeJson(string json)
        {
            var obj = JsonConvert.DeserializeObject(json, setting);
            switch (obj)
            {
                case JObject dict:
                    return new Hashtable(dict.ToObject<Hashtable>(), StringComparer.OrdinalIgnoreCase);
                case JArray list:
                    return list.ToObject<object[]>();
                default:
                    return obj;
            }
        }

        internal static RpcException ToRpcException(this Exception exception)
        {
            return new RpcException
            {
                Message = exception.Message,
                Source = exception.Source ?? "",
                StackTrace = exception.StackTrace ?? ""
            };
        }

        private static RpcHttp ToRpcHttp(this HttpResponseContext httpResponseContext, PowerShellManager psHelper)
        {
            var rpcHttp = new RpcHttp
            {
                StatusCode = httpResponseContext.StatusCode
            };

            if (httpResponseContext.Body != null)
            {
                rpcHttp.Body = httpResponseContext.Body.ToTypedData(psHelper);
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

        internal static TypedData ToTypedData(this object value, PowerShellManager psHelper)
        {
            if (value is TypedData self)
            {
                return self;
            }

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
                    typedData.Http = http.ToRpcHttp(psHelper);
                    break;
                case string str:
                    if (IsValidJson(str)) { typedData.Json = str; } else { typedData.String = str; }
                    break;
                default:
                    if (psHelper == null) { throw new ArgumentNullException(nameof(psHelper)); }
                    typedData.Json = psHelper.ConvertToJson(value);   
                    break;             
            }
            return typedData;
        }

        private static bool IsValidJson(string str)
        {
            str = str.Trim();
            int len = str.Length;
            if (len < 2)
            {
                return false;
            }

            if ((str[0] == '{' && str[len - 1] == '}') ||
                (str[0] == '[' && str[len - 1] == ']'))
            {
                try
                {
                    JToken.Parse(str);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore all exceptions
                }
            }

            return false;
        }
    }
}
