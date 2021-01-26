//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions;
    using Xunit;

    public class CallActivityWithRetryActionTests
    {
        [Theory]
        [InlineData(1, 1, null, null, null)]
        [InlineData(5, 3, null, null, null)]
        [InlineData(2, 3, 1.0, null, null)]
        [InlineData(4, 3, null, 1, null)]
        [InlineData(8, 3, null, null, 1)]
        [InlineData(1, 3, 0.5, 6, 7)]
        public void RetryOptionsContainsNonNullProperties(
            int firstRetryIntervalInMilliseconds,
            int maxNumberOfAttempts,
            double? backoffCoefficient,
            int? maxRetryIntervalInMilliseconds,
            int? retryTimeoutInMilliseconds)
        {
            var retryOptions = new RetryOptions(
                TimeSpan.FromMilliseconds(firstRetryIntervalInMilliseconds),
                maxNumberOfAttempts,
                backoffCoefficient,
                CreateTimeSpanOrNull(maxRetryIntervalInMilliseconds),
                CreateTimeSpanOrNull(retryTimeoutInMilliseconds));

            var action = new CallActivityWithRetryAction("FunctionName", "input", retryOptions);

            Assert.Equal(firstRetryIntervalInMilliseconds, action.RetryOptions["firstRetryIntervalInMilliseconds"]);
            Assert.Equal(maxNumberOfAttempts, action.RetryOptions["maxNumberOfAttempts"]);
            AssertRetryOptionsEntry("backoffCoefficient", backoffCoefficient, action);
            AssertRetryOptionsEntry("maxRetryIntervalInMilliseconds", maxRetryIntervalInMilliseconds, action);
            AssertRetryOptionsEntry("retryTimeoutInMilliseconds", retryTimeoutInMilliseconds, action);
        }

        private static void AssertRetryOptionsEntry<T>(
            string key,
            T? expectedValue,
            CallActivityWithRetryAction actualAction) where T : struct
        {
            if (expectedValue.HasValue)
            {
                Assert.Equal(expectedValue.Value, actualAction.RetryOptions[key]);
            }
            else
            {
                Assert.False(actualAction.RetryOptions.ContainsKey(key));
            }
        }

        private static TimeSpan? CreateTimeSpanOrNull(double? milliseconds)
        {
            return milliseconds.HasValue ? TimeSpan.FromMilliseconds(milliseconds.Value) : (TimeSpan?)null;
        }
    }
}
