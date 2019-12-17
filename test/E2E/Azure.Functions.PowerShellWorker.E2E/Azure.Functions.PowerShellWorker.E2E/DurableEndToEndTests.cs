// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Functions.PowerShell.Tests.E2E
{
    [Collection(Constants.FunctionAppCollectionName)]
    public class DurableEndToEndTests
    {
        private readonly FunctionAppFixture _fixture;

        public DurableEndToEndTests(FunctionAppFixture fixture)
        {
            this._fixture = fixture;
        }

        [Fact]
        public async Task ManagedDependenciesAreAvailable()
        {
            var actualResponseMessage =
                await Utilities.InvokeHttpTrigger("DurableClient", string.Empty, HttpStatusCode.Accepted);
        }
    }
}
