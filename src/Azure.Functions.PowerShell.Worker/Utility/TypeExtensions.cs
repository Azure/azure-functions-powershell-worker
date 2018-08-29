//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
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
                Url = rpcHttp.Url,
                Headers = rpcHttp.Headers,
                Params = rpcHttp.Params,
                Query = rpcHttp.Query
            };

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
                    return JsonConvert.DeserializeObject<Hashtable>(data.Json);
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
                Message = exception?.Message,
                Source = exception?.Source ?? "",
                StackTrace = exception?.StackTrace ?? ""
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
            foreach (DictionaryEntry item in httpResponseContext.Headers)
            {
                rpcHttp.Headers.Add(item.Key.ToString(), item.Value.ToString());
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

            if (LanguagePrimitives.TryConvertTo(value, out byte[] arr))
            {
                typedData.Bytes = ByteString.CopyFrom(arr);
            }
            else if(LanguagePrimitives.TryConvertTo(value, out HttpResponseContext http))
            {
                typedData.Http = http.ToRpcHttp();
            }
            else if (LanguagePrimitives.TryConvertTo(value, out Hashtable hashtable))
            {
                    typedData.Json = JsonConvert.SerializeObject(hashtable);
            }
            else if (LanguagePrimitives.TryConvertTo(value, out string str))
            {
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
            }
            return typedData;
        }
    }
}