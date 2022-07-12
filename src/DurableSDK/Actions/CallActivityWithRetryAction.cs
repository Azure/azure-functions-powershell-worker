//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions
{
    /// <summary>
    /// An orchestration action that represents calling an activity function with retry.
    /// </summary>
    internal class CallActivityWithRetryAction : OrchestrationAction
    {
        /// <summary>
        /// The activity function name.
        /// </summary>
        public readonly string FunctionName;

        /// <summary>
        /// The input to the activity function.
        /// </summary>
        public readonly object Input;

        /// <summary>
        /// Retry options.
        /// </summary>
        public readonly Dictionary<string, object> RetryOptions;

        public CallActivityWithRetryAction(string functionName, object input, RetryOptions retryOptions)
            : base(ActionType.CallActivityWithRetry)
        {
            FunctionName = functionName;
            Input = input;
            RetryOptions = ToDictionary(retryOptions);
        }

        private static Dictionary<string, object> ToDictionary(RetryOptions retryOptions)
        {
            var result = new Dictionary<string, object>()
                            {
                                { "firstRetryIntervalInMilliseconds", ToIntMilliseconds(retryOptions.FirstRetryInterval) },
                                { "maxNumberOfAttempts", retryOptions.MaxNumberOfAttempts }
                            };

            AddOptionalValue(result, "backoffCoefficient", retryOptions.BackoffCoefficient, x => x);
            AddOptionalValue(result, "maxRetryIntervalInMilliseconds", retryOptions.MaxRetryInterval, ToIntMilliseconds);
            AddOptionalValue(result, "retryTimeoutInMilliseconds", retryOptions.RetryTimeout, ToIntMilliseconds);

            return result;
        }

        private static void AddOptionalValue<T>(
            Dictionary<string, object> dictionary,
            string name,
            T? nullable,
            Func<T, object> transformValue) where T : struct
        {
            if (nullable.HasValue)
            {
                dictionary.Add(name, transformValue(nullable.Value));
            }
        }

        private static object ToIntMilliseconds(TimeSpan timespan)
        {
            return (int)timespan.TotalMilliseconds;
        }
    }
}
