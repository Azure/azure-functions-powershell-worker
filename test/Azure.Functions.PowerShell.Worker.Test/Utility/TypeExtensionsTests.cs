using System;
using System.Collections;

using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Azure.Functions.PowerShellWorker;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Newtonsoft.Json;
using Xunit;

namespace Azure.Functions.PowerShell.Worker.Test
{
    public class TypeExtensionsTests
    {
        #region TypedDataToObject
        [Fact]
        public void TestTypedDataToObjectHttpRequestContextBasic()
        {
            var method = "Get";
            var url = "https://example.com";

            var input = new TypedData
            {
                Http = new RpcHttp
                {
                    Method = method,
                    Url = url
                }
            };

            var expected = new HttpRequestContext
            {
                Method = method,
                Url = url,
                Headers = new MapField<string, string>(),
                Params = new MapField<string, string>(),
                Query = new MapField<string, string>()
            };

            Assert.Equal(expected, (HttpRequestContext)input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectHttpRequestContextWithUrlData()
        {
            var method = "Get";
            var url = "https://example.com";
            var key = "foo";
            var value = "bar";

            var input = new TypedData
            {
                Http = new RpcHttp
                {
                    Method = method,
                    Url = url
                }
            };
            input.Http.Headers.Add(key, value);
            input.Http.Params.Add(key, value);
            input.Http.Query.Add(key, value);

            var expected = new HttpRequestContext
            {
                Method = method,
                Url = url,
                Headers = new MapField<string, string>
                {
                    {key, value}
                },
                Params = new MapField<string, string>
                {
                    {key, value}
                },
                Query = new MapField<string, string>
                {
                    {key, value}
                }
            };

            Assert.Equal(expected, (HttpRequestContext)input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectHttpRequestContextBodyData()
        {
            var method = "Get";
            var url = "https://example.com";
            var data = "Hello World";

            var input = new TypedData
            {
                Http = new RpcHttp
                {
                    Method = method,
                    Url = url,
                    Body = new TypedData
                    {
                        String = data
                    },
                    RawBody = new TypedData
                    {
                        String = data
                    }
                }
            };

            var expected = new HttpRequestContext
            {
                Method = method,
                Url = url,
                Headers = new MapField<string, string>(),
                Params = new MapField<string, string>(),
                Query = new MapField<string, string>(),
                Body = data,
                RawBody = data
            };

            Assert.Equal(expected, (HttpRequestContext)input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectString()
        {
            var data = "Hello World";

            var input = new TypedData { String = data };
            var expected = data;

            Assert.Equal(expected, (string)input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectInt()
        {
            long data = 2;

            var input = new TypedData { Int = data };
            var expected = data;

            Assert.Equal(expected, (long)input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectDouble()
        {
            var data = 2.2;

            var input = new TypedData { Double = data };
            var expected = data;

            Assert.Equal(expected, (double)input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectJson()
        {
            var data = "{\"Foo\":\"Bar\"}";

            var input = new TypedData { Json = data };
            var expected = JsonConvert.DeserializeObject<Hashtable>(data);
            var actual = (Hashtable)input.ToObject();
            Assert.Equal((string)expected["Foo"], (string)actual["Foo"]);
        }

        [Fact]
        public void TestTypedDataToObjectBytes()
        {
            var data = ByteString.CopyFromUtf8("Hello World");

            var input = new TypedData { Bytes = data };
            var expected = data.ToByteArray();

            Assert.Equal(expected, (byte[])input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectStream()
        {
            var data = ByteString.CopyFromUtf8("Hello World");

            var input = new TypedData { Stream = data };
            var expected = data.ToByteArray();

            Assert.Equal(expected, (byte[])input.ToObject());
        }
        #endregion
        #region ExceptionToRpcException
        [Fact]
        public void TestExceptionToRpcExceptionBasic()
        {
            var data = "bad";

            var input = new Exception(data);
            var expected = new RpcException
            {
                Message = "bad"
            };

            Assert.Equal(expected, input.ToRpcException());
        }

        [Fact]
        public void TestExceptionToRpcExceptionExtraData()
        {
            var data = "bad";

            var input = new Exception(data);
            input.Source = data;

            var expected = new RpcException
            {
                Message = data,
                Source = data
            };

            Assert.Equal(expected, input.ToRpcException());
        }
        #endregion
        #region ObjectToTypedData
        [Fact]
        public void TestObjectToTypedDataRpcHttpBasic()
        {
            var data = "Hello World";

            var input = new HttpResponseContext
            {
                Body = data
            };
            var expected = new TypedData
            {
                Http = new RpcHttp
                {
                    StatusCode = "200",
                    Body = new TypedData { String = data },
                    Headers = { { "content-type", "text/plain" } }
                }
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataRpcHttpContentTypeSet()
        {
            var data = "<html></html>";

            var input = new HttpResponseContext
            {
                Body = data,
                ContentType = "text/html"
            };
            var expected = new TypedData
            {
                Http = new RpcHttp
                {
                    StatusCode = "200",
                    Body = new TypedData { String = data },
                    Headers = { { "content-type", "text/html" } }
                }
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataRpcHttpContentTypeInHeader()
        {
            var data = "<html></html>";

            var input = new HttpResponseContext
            {
                Body = data,
                Headers =  { { "content-type", "text/html" } }
            };
            var expected = new TypedData
            {
                Http = new RpcHttp
                {
                    StatusCode = "200",
                    Body = new TypedData { String = data },
                    Headers = { { "content-type", "text/html" } }
                }
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataRpcHttpStatusCodeString()
        {
            var data = "Hello World";

            var input = new HttpResponseContext
            {
                Body = data,
                StatusCode = "201"
            };
            var expected = new TypedData
            {
                Http = new RpcHttp
                {
                    StatusCode = "201",
                    Body = new TypedData { String = data },
                    Headers = { { "content-type", "text/plain" } }
                }
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact(Skip = "Int gets interpreted as byte[]")]
        public void TestObjectToTypedDataInt()
        {
            var data = (long)1;

            var input = (object)data;
            var expected = new TypedData
            {
                Int = data
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact(Skip = "Double gets interpreted as byte[]")]
        public void TestObjectToTypedDataDouble()
        {
            var data = 1.1;

            var input = (object)data;
            var expected = new TypedData
            {
                Double = data
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataString()
        {
            var data = "Hello World!";

            var input = (object)data;
            var expected = new TypedData
            {
                String = data
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataBytes()
        {
            var data = ByteString.CopyFromUtf8("Hello World!").ToByteArray();

            var input = (object)data;
            var expected = new TypedData
            {
                Bytes = ByteString.CopyFrom(data)
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact(Skip = "Stream gets interpreted as Bytes")]
        public void TestObjectToTypedDataStream()
        {
            var data = ByteString.CopyFromUtf8("Hello World!").ToByteArray();

            var input = (object)data;
            var expected = new TypedData
            {
                Stream = ByteString.CopyFrom(data)
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataJsonString()
        {
            var data = "{\"foo\":\"bar\"}";

            var input = (object)data;
            var expected = new TypedData
            {
                Json = data
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataJsonHashtable()
        {
            var data = new Hashtable { { "foo", "bar" } };

            var input = (object)data;
            var expected = new TypedData
            {
                Json = "{\"foo\":\"bar\"}"
        };

            Assert.Equal(expected, input.ToTypedData());
        }
        #endregion
    }
}