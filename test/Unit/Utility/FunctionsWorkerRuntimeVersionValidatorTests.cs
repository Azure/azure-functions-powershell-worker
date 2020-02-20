//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Utility
{
    using Xunit;

    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    public class FunctionsWorkerRuntimeVersionValidatorTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("6")]
        [InlineData("~6")]
        [InlineData("  6  ")]
        [InlineData("  ~6  ")]
        public void NoErrorOnValidVersion(string versionToCheck)
        {
            Assert.Null(FunctionsWorkerRuntimeVersionValidator.GetErrorMessage(versionToCheck));
        }

        [Theory]
        [InlineData("7")]
        [InlineData("~7")]
        [InlineData("  7  ")]
        [InlineData("  ~7  ")]
        [InlineData("5")]
        [InlineData("8")]
        [InlineData("~")]
        [InlineData("anything else")]
        public void ErrorOnInvalidVersion(string versionToCheck)
        {
            var error = FunctionsWorkerRuntimeVersionValidator.GetErrorMessage(versionToCheck);
            Assert.NotNull(error);
            Assert.Contains(versionToCheck, error);
        }
    }
}
