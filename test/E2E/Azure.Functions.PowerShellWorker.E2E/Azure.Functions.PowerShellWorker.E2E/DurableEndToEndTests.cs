// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Azure.Functions.PowerShell.Tests.E2E
{
    using System;
    using System.Collections.Generic;
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

<<<<<<< HEAD
        /*
            Verifies that the Durable execution model correctly replays the same collection of CurrentUtcDateTimes.
            The orchestrator writes CurrentUtcDateTime values to a temporary file. File contents are expected to
            take one of two forms:

            Case 1                      Case 2
            Line                        Line
            0     ---                   0     ---
            1     <Timestamp1>          1     <Timestamp1>    
            2     <Timestamp1>          2     <Timestamp1>
            3     ---                   3     ---
            4     <Timestamp1>          4     <Timestamp1>
            5     <Timestamp1>          5     <Timestamp1>
            6     <Timestamp2>          6     <Timestamp2>
            7     <Timestamp2>          7     <Timestamp2>
            8     <Timestamp2>          8     <Timestamp2>
            9     ---                   9     ---
            10    <Timestamp1>          10    <Timestamp1>
            11    <Timestamp1>          11    <Timestamp1>
            12    <Timestamp2>          12    <Timestamp2>
            13    <Timestamp2>          13    <Timestamp2>
            14    <Timestamp2>          14    <Timestamp2>
            15    <Timestamp3>          15    ---
                                        16    <Timestamp1>
                                        17    <Timestamp1>
                                        18    <Timestamp2>
                                        19    <Timestamp2>
                                        20    <Timestamp2>
                                        21    <Timestamp3>
        */
=======
>>>>>>> 751b9f5... Added CurrentUtcDateTime E2E tests
        [Fact]
        public async Task DurableExecutionReplaysCurrentUtcDateTime()
        {
            var initialResponse = await Utilities.GetHttpTriggerResponse("CurrentUtcDateTimeStart", queryString: string.Empty);
<<<<<<< HEAD

            var location = initialResponse.Headers.Location;

            var initialResponseBody = await initialResponse.Content.ReadAsStringAsync();
            dynamic initialResponseBodyObject = JsonConvert.DeserializeObject(initialResponseBody);
            var statusQueryGetUri = (string)initialResponseBodyObject.statusQueryGetUri;
=======
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
>>>>>>> 751b9f5... Added CurrentUtcDateTime E2E tests

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
<<<<<<< HEAD
=======
                            var statusResponseBody = await GetResponseBodyAsync(statusResponse);
                            var runtimeStatus = (string)statusResponseBody.runtimeStatus;
                            Assert.True(
                                runtimeStatus == "Running" || runtimeStatus == "Pending",
                                $"Unexpected runtime status: {runtimeStatus}");

                            if (DateTime.UtcNow > startTime + orchestrationCompletionTimeout)
                            {
                                Assert.True(false, $"The orchestration has not completed after {orchestrationCompletionTimeout}");
                            }

>>>>>>> 751b9f5... Added CurrentUtcDateTime E2E tests
                            await Task.Delay(TimeSpan.FromSeconds(2));
                            break;
                        }
                        case HttpStatusCode.OK:
                        {
                            var statusResponseBody = await GetResponseBodyAsync(statusResponse);
                            Assert.Equal("Completed", (string)statusResponseBody.runtimeStatus);
                            string path = statusResponseBody.output.ToString();
                            string[] lines = System.IO.File.ReadAllLines(path);
<<<<<<< HEAD
                            // Expect the format to be as in Case 1
                            var delineatorLines = new List<int>(new int[] { 0, 3, 9 });
                            var timestamp1Lines = new List<int>(new int[] { 1, 2, 4, 5, 10, 11 });
                            var timestamp2Lines = new List<int>(new int[] { 6, 7, 8, 12, 13, 14 });
                            int timestamp3Line = 15;
                            
                            // Updates the expected format to be Case 2 if it is not Case 1
                            if (lines[timestamp3Line] == "---") {
                                timestamp3Line = 21;
                                delineatorLines.Add(15);
                                timestamp1Lines.AddRange(new int[] { 16, 17 });
                                timestamp2Lines.AddRange(new int[] { 18, 19, 20 });
                            }

                            Assert.Equal("---", lines[delineatorLines[0]]);
                            VerifyArrayItemsAreEqual(array: lines, indices: delineatorLines);
                            VerifyArrayItemsAreEqual(array: lines, indices: timestamp1Lines);
                            VerifyArrayItemsAreEqual(array: lines, indices: timestamp2Lines);
                            // Verifies that the Timestamp3 line is not a delineator, Timestamp2, or Timestamp1 line
                            Assert.NotEqual(lines[timestamp3Line], lines[delineatorLines[0]]);
                            Assert.NotEqual(lines[timestamp3Line], lines[timestamp1Lines[0]]);
                            Assert.NotEqual(lines[timestamp3Line], lines[timestamp2Lines[0]]);
=======
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
>>>>>>> 751b9f5... Added CurrentUtcDateTime E2E tests
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
        
<<<<<<< HEAD
        private void VerifyArrayItemsAreEqual(string[] array, List<int> indices)
        {
            if (indices.Capacity > 0)
            {
                var expected = array[indices[0]];
                for (int i = 1; i < indices.Capacity; i++)
                {
                    Assert.Equal(expected, array[indices[i]]);
=======
        private void VerifyLinesEqual(string[] lines, int[] equalIndices)
        {
            if (equalIndices.Length > 0)
            {
                var expected = lines[equalIndices[0]];
                for (int i = 1; i < equalIndices.Length; i++)
                {
                    Assert.Equal(expected, lines[equalIndices[i]]);
>>>>>>> 751b9f5... Added CurrentUtcDateTime E2E tests
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
