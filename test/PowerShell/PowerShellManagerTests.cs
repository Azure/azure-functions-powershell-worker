//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    public class PowerShellManagerTests
    {
        [Fact]
        public void InitializeRunspaceSuccess()
        {
            var logger = new ConsoleLogger();
            var manager = new PowerShellManager(logger);
            manager.InitializeRunspace();

            Assert.Single(logger.FullLog);
            Assert.Equal("Warning: Required environment variables to authenticate to Azure were not present", logger.FullLog[0]);
        }
    }
}
