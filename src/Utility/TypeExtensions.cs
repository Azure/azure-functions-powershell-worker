//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.IO;
using System.Management.Automation;
using System.Net.Http.Headers;
using System.Text;

using Google.Protobuf;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.PowerShell.Commands;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal static class TypeExtensions
    {
        private const string ContentTypeHeaderKey = "content-type";

        private const string ApplicationJsonMediaType = "application/json";
        private const string TextPlainMediaType = "text/plain";

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
                httpRequestContext.Body = rpcHttp.Body.ToObject(ShouldConvertBodyFromJson(rpcHttp));
                httpRequestContext.RawBody = GetRawBody(rpcHttp.Body);
            }

            return httpRequestContext;
        }

        internal static object ToObject(this TypedData data, bool convertFromJsonIfValidJson = true)
        {
            if (data == null)
            {
                return null;
            }

            switch (data.DataCase)
            {
                case TypedData.DataOneofCase.Json:
                    return ConvertFromJson(data.Json);
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
                    string str = data.String;
                    return convertFromJsonIfValidJson && IsValidJson(str)
                                ? ConvertFromJson(str)
                                : str;
                case TypedData.DataOneofCase.None:
                    return null;
                default:
                    return new InvalidOperationException("DataCase was not set.");
            }
        }

        private static object GetRawBody(TypedData rpcHttpBody)
        {
            switch (rpcHttpBody.DataCase)
            {
                case TypedData.DataOneofCase.String:
                    return rpcHttpBody.String;

                case TypedData.DataOneofCase.Bytes:
                    return Encoding.UTF8.GetString(rpcHttpBody.Bytes.ToByteArray());

                default:
                    return rpcHttpBody.ToObject();
            }
        }

        public static object ConvertFromJson(string json)
        {
            object retObj = JsonObject.ConvertFromJson(json, returnHashtable: true, error: out _);

            if (retObj is PSObject psObj)
            {
                retObj = psObj.BaseObject;
            }

            if (retObj is Hashtable hashtable)
            {
                try
                {
                    // ConvertFromJson returns case-sensitive Hashtable by design -- JSON may contain keys that only differ in case.
                    // We try casting the Hashtable to a case-insensitive one, but if that fails, we keep using the original one.
                    retObj = new Hashtable(hashtable, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    retObj = hashtable;
                }
            }

            return retObj;
        }

        private static string ConvertToJson(object fromObj)
        {
            /* we set the max-depth to 50 because the Durable Functions Extension
             * may produce deeply nested JSON-Objects when callig its
             * WhenAll/WhenAny APIs. The value 50 is arbitrarily chosen to be
             * "deep enough" for the vast majority of cases.
             */
            var context = new JsonObject.ConvertToJsonContext(
                maxDepth: 50,
                enumsAsStrings: false,
                compressOutput: true);

            return JsonObject.ConvertToJson(fromObj, in context);
        }

        internal static RpcException ToRpcException(this Exception exception)
        {
            return new RpcException
            {
                Source = exception.Source ?? "",
                StackTrace = exception.StackTrace ?? "",
                Message = exception.Message
            };
        }

        private static RpcHttp ToRpcHttp(this HttpResponseContext httpResponseContext)
        {
            var rpcHttp = new RpcHttp
            {
                StatusCode = httpResponseContext.StatusCode.ToString("d")
            };

            rpcHttp.Body = httpResponseContext.Body == null
                            ? string.Empty.ToTypedData()
                            : httpResponseContext.Body.ToTypedData();

            rpcHttp.EnableContentNegotiation = httpResponseContext.EnableContentNegotiation;

            // Add all the headers. ContentType is separated for convenience
            if (httpResponseContext.Headers != null)
            {
                foreach (DictionaryEntry item in httpResponseContext.Headers)
                {
                    rpcHttp.Headers.Add(item.Key.ToString(), item.Value.ToString());
                }
            }

            // Allow the user to set content-type in the Headers
            if (!rpcHttp.Headers.ContainsKey(ContentTypeHeaderKey))
            {
                rpcHttp.Headers.Add(ContentTypeHeaderKey, DeriveContentType(httpResponseContext, rpcHttp));
            }

            return rpcHttp;
        }

        private static string DeriveContentType(HttpResponseContext httpResponseContext, RpcHttp rpcHttp)
        {
            return httpResponseContext.ContentType ??
                                (rpcHttp.Body.DataCase == TypedData.DataOneofCase.Json
                                    ? ApplicationJsonMediaType
                                    : TextPlainMediaType);
        }

        internal static TypedData ToTypedData(this object value)
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

            // Save the original value.
            // We will use the original value when converting to JSON, so members added by ETS can be captured in the serialization. 
            var originalValue = value;
            if (value is PSObject psObj && psObj.BaseObject != null)
            {
                value = psObj.BaseObject;
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
                case string str:
                    if (IsValidJson(str)) { typedData.Json = str; } else { typedData.String = str; }
                    break;
                default:
                    typedData.Json = ConvertToJson(originalValue);
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

        /// <summary>
        /// The body should be deserialized from JSON automatically only if the HTTP request:
        ///   - does not have Content-Type header; or
        ///   - does have Content-Type header, and it contains 'application/json'.
        /// Any other Content-Type is interpreted as an instruction to *not* deserialize the body.
        /// In these cases, we should pass the body to the function code as is, without any attempt to deserialize.
        /// </summary>
        private static bool ShouldConvertBodyFromJson(RpcHttp rpcHttp)
        {
            var contentType = GetContentType(rpcHttp);
            if (contentType == null)
            {
                return true;
            }

            return MediaTypeHeaderValue.TryParse(contentType, out var mediaTypeHeaderValue)
                   && mediaTypeHeaderValue.MediaType.Equals(ApplicationJsonMediaType, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetContentType(RpcHttp rpcHttp)
        {
            if (rpcHttp.Headers == null)
            {
                return null;
            }

            foreach (var (key, value) in rpcHttp.Headers)
            {
                if (key.Equals(ContentTypeHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
