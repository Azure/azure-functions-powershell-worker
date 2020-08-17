//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Moq;
using Xunit;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    public class FunctionsEnvironmentReloaderTests
    {
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        [Fact]
        public void SetsEnvironmentVariables()
        {
            var actualEnvironmentVariables = new List<KeyValuePair<string, string>>();

            var reloader = new FunctionsEnvironmentReloader(
                logger: _mockLogger.Object,
                setEnvironmentVariable: (name, value) => { actualEnvironmentVariables.Add(new KeyValuePair<string, string>(name, value)); },
                setCurrentDirectory: directory => { });

            var requestedEnvironmentVariables = new[] {
                new KeyValuePair<string, string>( "name1", "valueA" ),
                new KeyValuePair<string, string>( "name2", "valueB" ),
                new KeyValuePair<string, string>( "name3", "valueC" ),
            };

            reloader.ReloadEnvironment(requestedEnvironmentVariables, functionAppDirectory: null);

            Assert.Equal(requestedEnvironmentVariables, actualEnvironmentVariables);
        }

        [Fact]
        public void SetsFunctionAppDirectoryIfRequested()
        {
            const string RequestedNewDirectory = "new app directory";
            string actualNewDirectory = null;

            var reloader = new FunctionsEnvironmentReloader(
                logger: _mockLogger.Object,
                setEnvironmentVariable: (name, value) => { },
                setCurrentDirectory: directory => { actualNewDirectory = directory; });

            reloader.ReloadEnvironment(new List<KeyValuePair<string, string>>(), RequestedNewDirectory);

            Assert.Equal(RequestedNewDirectory, actualNewDirectory);
        }

        [Fact]
        public void DoesNotSetFunctionAppDirectoryIfNotRequested()
        {
            var reloader = new FunctionsEnvironmentReloader(
                logger: _mockLogger.Object,
                setEnvironmentVariable: (name, value) => { },
                setCurrentDirectory: directory => { Assert.True(false, "Unexpected invocation"); });

            reloader.ReloadEnvironment(new List<KeyValuePair<string, string>>(), functionAppDirectory: null);
        }
    }
}
