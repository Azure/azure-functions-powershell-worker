// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Azure.Functions.PowerShell.Tests.E2E
{
    using System.Net;
    using System.Threading.Tasks;
    using Xunit;

    using System.Net.Http;
    using Newtonsoft.Json;
    using Xunit.Abstractions;

    [Collection(Constants.FunctionAppCollectionName)]
    public class DurableEndToEndTests
    {
        private readonly FunctionAppFixture _fixture;
        private readonly ITestOutputHelper _testOutputHelper;

        public DurableEndToEndTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this._fixture = fixture;
            this._testOutputHelper = testOutputHelper;
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
                _testOutputHelper.WriteLine($"statusQueryGetUri: '{statusQueryGetUri}'");
                var statusResponse = await httpClient.GetAsync(statusQueryGetUri);
                Assert.Equal(HttpStatusCode.Accepted, statusResponse.StatusCode);
            }
        }
    }
}
