// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Azure.Functions.PowerShell.Tests.E2E
{
    using System;
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

            var orchestrationCompletionTimeout = TimeSpan.FromSeconds(60);
            var startTime = DateTime.UtcNow;

            using (var httpClient = new HttpClient())
            {
                while (true)
                {
                    var statusResponse = await httpClient.GetAsync(statusQueryGetUri);
                    switch (statusResponse.StatusCode)
                    {
                        case HttpStatusCode.Accepted:
                        {
                            var statusResponseBody = await GetResponseBodyAsync(statusResponse);
                            var runtimeStatus = (string)statusResponseBody.runtimeStatus;
                            Assert.True(
                                runtimeStatus == "Running" || runtimeStatus == "Pending",
                                $"Unexpected runtime status: {runtimeStatus}");

                            if (DateTime.UtcNow > startTime + orchestrationCompletionTimeout)
                            {
                                Assert.True(false, $"The orchestration has not completed after {orchestrationCompletionTimeout}");
                            }

                            await Task.Delay(TimeSpan.FromSeconds(2));
                            break;
                        }

                        case HttpStatusCode.OK:
                        {
                            var statusResponseBody = await GetResponseBodyAsync(statusResponse);
                            Assert.Equal("Completed", (string)statusResponseBody.runtimeStatus);
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
        }

        [Fact]
        public async Task DurableExecutionReplaysCurrentUtcDateTime()
        {
            var initialResponse = await Utilities.GetHttpTriggerResponse("CurrentUtcDateTimeStart", queryString: string.Empty);
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

            var orchestrationCompletionTimeout = TimeSpan.FromSeconds(60);
            var startTime = DateTime.UtcNow;

            using(var httpClient = new HttpClient())
            {
                while (true)
                {
                    var statusResponse = await httpClient.GetAsync(statusQueryGetUri);
                    switch (statusResponse.StatusCode)
                    {
                        case HttpStatusCode.Accepted:
                        {
                            var statusResponseBody = await GetResponseBodyAsync(statusResponse);
                            var runtimeStatus = (string)statusResponseBody.runtimeStatus;
                            Assert.True(
                                runtimeStatus == "Running" || runtimeStatus == "Pending",
                                $"Unexpected runtime status: {runtimeStatus}");

                            if (DateTime.UtcNow > startTime + orchestrationCompletionTimeout)
                            {
                                Assert.True(false, $"The orchestration has not completed after {orchestrationCompletionTimeout}");
                            }

                            await Task.Delay(TimeSpan.FromSeconds(2));
                            break;
                        }
                        case HttpStatusCode.OK:
                        {
                            var statusResponseBody = await GetResponseBodyAsync(statusResponse);
                            Assert.Equal("Completed", (string)statusResponseBody.runtimeStatus);
                            string path = statusResponseBody.output.ToString();
                            string[] lines = System.IO.File.ReadAllLines(path);
                            Assert.Equal("---", lines[0]);
                            int[] delineatorIndices = { 0, 3, 9, 15 };
                            int[] timestamp1Indices = { 1, 2, 4, 5, 10, 11, 16, 17 };
                            int[] timestamp2Indices = { 6, 7, 8, 12, 13, 14, 18, 19, 20 };

                            // See the CurrentUtcDateTimeOrchestrator in the TestFunctionApp to see why these results are expected
                            VerifyLinesEqual(lines: lines, equalIndices: delineatorIndices);
                            VerifyLinesEqual(lines: lines, equalIndices: timestamp1Indices);
                            VerifyLinesEqual(lines: lines, equalIndices: timestamp2Indices);
                            Assert.NotEqual(lines[21], lines[0]);
                            Assert.NotEqual(lines[21], lines[1]);
                            Assert.NotEqual(lines[21], lines[6]);
                            return;
                        }
                        default:
                        {
                            Assert.True(false, $"Unexpected orchestration status code: {statusResponse.StatusCode}");
                            break;
                        }
                    }
                }
            }
            
        }
        
        private void VerifyLinesEqual(string[] lines, int[] equalIndices)
        {
            if (equalIndices.Length > 0)
            {
                var expected = lines[equalIndices[0]];
                for (int i = 1; i < equalIndices.Length; i++)
                {
                    Assert.Equal(expected, lines[equalIndices[i]]);
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
