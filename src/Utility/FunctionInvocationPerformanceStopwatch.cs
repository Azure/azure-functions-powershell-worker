//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    internal class FunctionInvocationPerformanceStopwatch
    {
        public enum Checkpoint
        {
            DependenciesAvailable,
            RunspaceAvailable,
            MetadataAndTraceContextReady,
            FunctionCodeReady,
            InputBindingValuesReady,
            InvokingFunctionCode
        }

        private readonly Stopwatch _stopwatch = new Stopwatch();

        private readonly List<KeyValuePair<Checkpoint, long>> _checkpointTimes =
            new List<KeyValuePair<Checkpoint, long>>(
                capacity: Enum.GetValues(typeof(Checkpoint)).Length);

        public long TotalMilliseconds => _stopwatch.ElapsedMilliseconds;

        public IEnumerable<KeyValuePair<Checkpoint, long>> CheckpointMilliseconds => _checkpointTimes;

        public void OnStart()
        {
            _stopwatch.Start();
        }

        public void OnCheckpoint(Checkpoint checkpoint)
        {
            _checkpointTimes.Add(KeyValuePair.Create(checkpoint, _stopwatch.ElapsedMilliseconds));
        }
    }
}
