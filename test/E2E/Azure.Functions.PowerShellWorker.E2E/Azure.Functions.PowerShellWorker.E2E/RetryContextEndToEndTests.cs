// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Functions.PowerShell.Tests.E2E
{
    [Collection(Constants.FunctionAppCollectionName)]
    public class RetryContextEndToEndTests 
    {
        private readonly FunctionAppFixture _fixture;

        public RetryContextEndToEndTests(FunctionAppFixture fixture)
        {
            this._fixture = fixture;
        }

        [Theory]
        [InlineData("HttpTriggerThrowsWithFixedRetry", "", HttpStatusCode.InternalServerError, "Current retry count: 3")]
        public async Task HttpTriggerRetryContextTests(string functionName, string queryString, HttpStatusCode expectedStatusCode, string expectedMessage)
        {
            Assert.True(await Utilities.InvokeHttpTrigger(functionName, queryString, expectedStatusCode, expectedMessage));
        }
    }
}
