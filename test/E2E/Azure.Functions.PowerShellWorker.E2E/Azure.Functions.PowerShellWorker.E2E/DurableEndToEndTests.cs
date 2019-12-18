﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Azure.Functions.PowerShell.Tests.E2E
{
    using System.Net;
    using System.Threading.Tasks;
    using Xunit;

    using System.Net.Http;
    using Newtonsoft.Json;

    [Collection(Constants.FunctionAppCollectionName)]
    public class DurableEndToEndTests
    {
        private readonly FunctionAppFixture _fixture;

        public DurableEndToEndTests(FunctionAppFixture fixture)
        {
            this._fixture = fixture;
        }

        [Fact]
        public async Task DurableClientFollowsAsyncPattern()
        {
            var initialResponse = await Utilities.GetHttpTriggerResponse("DurableClient", queryString: string.Empty);
            Assert.Equal(HttpStatusCode.Accepted, initialResponse.StatusCode);

            var location = initialResponse.Headers.Location;
            Assert.NotNull(location);

            var initialResponseBody = await initialResponse.Content.ReadAsStringAsync();
            dynamic initialResponseBodyObject = JsonConvert.DeserializeObject(initialResponseBody);
            Assert.NotNull(initialResponseBodyObject.id);
            var statusQueryGetUri = (string)initialResponseBodyObject.statusQueryGetUri;
            Assert.Equal(location.ToString(), statusQueryGetUri);
            Assert.NotNull(initialResponseBodyObject.sendEventPostUri);
            Assert.NotNull(initialResponseBodyObject.purgeHistoryDeleteUri);
            Assert.NotNull(initialResponseBodyObject.terminatePostUri);
            Assert.NotNull(initialResponseBodyObject.rewindPostUri);

            using (var httpClient = new HttpClient())
            {
                var statusResponse = await httpClient.GetAsync(statusQueryGetUri);
                switch (statusResponse.StatusCode)
                {
                    case HttpStatusCode.Accepted:
                    {
                        var statusResponseBody = await GetResponseBodyAsync(statusResponse);
                        Assert.Equal("Running", statusResponseBody.runtimeStatus);
                        break;
                    }

                    case HttpStatusCode.OK:
                    {
                        var statusResponseBody = await GetResponseBodyAsync(statusResponse);
                        Assert.Equal("Completed", statusResponseBody.runtimeStatus);
                        Assert.Equal("Hello Tokyo", statusResponseBody.output[0].ToString());
                        Assert.Equal("Hello Seattle", statusResponseBody.output[1].ToString());
                        Assert.Equal("Hello London", statusResponseBody.output[2].ToString());
                        return;
                    }

                    default:
                        Assert.True(false, $"Unexpected orchestration status code: {statusResponse.StatusCode}");
                        break;
                }
            }
        }

        private static async Task<dynamic> GetResponseBodyAsync(HttpResponseMessage response)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject(responseBody);
        }
    }
}
