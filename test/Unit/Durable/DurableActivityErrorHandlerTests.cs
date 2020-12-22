//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System.Management.Automation;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Xunit;

    public class DurableActivityErrorHandlerTests
    {
        [Fact]
        public void WritesCorrectError()
        {
            const string ErrorMessage = "My error message";

            var errorWritten = false;
            DurableActivityErrorHandler.CreateAndWriteError(
                ErrorMessage,
                errorRecord => {
                    errorWritten = true;
                    Assert.Equal("Functions.Durable.ActivityFailure", errorRecord.FullyQualifiedErrorId);
                    Assert.IsType<ActivityFailureException>(errorRecord.Exception);
                    Assert.Equal(ErrorMessage, errorRecord.Exception.Message);
                    Assert.Equal(ErrorCategory.NotSpecified, errorRecord.CategoryInfo.Category);
                    Assert.Null(errorRecord.TargetObject);
                });

            Assert.True(errorWritten);
        }
    }
}
