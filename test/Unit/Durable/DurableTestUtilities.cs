//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Azure.Functions.PowerShellWorker.Durable;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    internal static class DurableTestUtilities
    {
        public static HistoryEvent[] MergeHistories(params HistoryEvent[][] histories)
        {
            return histories.Aggregate((a, b) => a.Concat(b).ToArray());
        }

        public static long MeasureExecutionTimeInMilliseconds(Action action)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            action();

            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
    }
}
