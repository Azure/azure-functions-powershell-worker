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
    public class ManagedDependenciesEndToEndTests 
    {
        private readonly FunctionAppFixture _fixture;

        public ManagedDependenciesEndToEndTests(FunctionAppFixture fixture)
        {
            this._fixture = fixture;
        }

        [Fact]
        public async Task ManagedDependenciesAreAvailable()
        {
            const string ManagedDependenciesRootFolder = "ManagedDependencies";
            const string ModuleFolder  = "Az.Accounts";

            var actualResponseMessage =
                await Utilities.InvokeHttpTrigger("UsingManagedDependencies", string.Empty, HttpStatusCode.OK);

            var pathParts = actualResponseMessage.Split(Path.DirectorySeparatorChar);

            var rootDependenciesFolderPosition =
                Array.FindIndex(
                    pathParts,
                    item => string.Compare(item, ManagedDependenciesRootFolder, StringComparison.OrdinalIgnoreCase) == 0);

            Assert.True(rootDependenciesFolderPosition != -1, $"'{ManagedDependenciesRootFolder}' is not found in response message '{actualResponseMessage}'");

            var moduleFolderPosition =
                Array.FindIndex(
                    pathParts,
                    item => string.Compare(item, ModuleFolder, StringComparison.OrdinalIgnoreCase) == 0);

            Assert.True(moduleFolderPosition != -1, $"'{ModuleFolder}' is not found at an expected position in response message '{actualResponseMessage}'");
        }
    }
}
