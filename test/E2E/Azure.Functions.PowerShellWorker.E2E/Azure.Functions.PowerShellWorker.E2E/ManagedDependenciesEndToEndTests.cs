﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Functions.PowerShell.Tests.E2E
{
    public class ManagedDependenciesEndToEndTests 
    {
        [Fact]
        public async Task ManagedDependenciesAreAvailable()
        {
            var actualResponseMessage =
                await Utilities.InvokeHttpTrigger("UsingManagedDependencies", string.Empty, HttpStatusCode.OK);

            var expectedAzModulePathPart = Path.DirectorySeparatorChar + Path.Combine("ManagedDependencies", "Az") + Path.DirectorySeparatorChar;
            Assert.Contains(expectedAzModulePathPart, actualResponseMessage);
        }
    }
}
