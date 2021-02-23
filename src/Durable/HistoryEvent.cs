//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    internal class HistoryEvent
    {
        #region Common_Fields

        [DataMember]
        public int EventId { get; set; }

        [DataMember]
        public bool IsPlayed { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; }

        [DataMember]
        public HistoryEventType EventType { get; set; }

        [DataMember]
        public string Reason { get; set; }

        #endregion

        #region Timer_Event_Fields

        [DataMember]
        public DateTime FireAt { get; set; }

        [DataMember]
        public int TimerId { get; set; }

        #endregion

        #region Overloaded_Fields

        [DataMember]
        public int TaskScheduledId { get; set; }

        [DataMember]
        public string Input { get; set; }

        [DataMember]
        public string Name { get; set; }
        
        [DataMember]
        public string Result { get; set; }

        #endregion

        // Internal used only
        public bool IsProcessed { get; set; }

        public override string ToString()
        {
            var relatedEventId = EventType == HistoryEventType.TimerFired ? TimerId : TaskScheduledId;
            var processedMarker = IsProcessed ? "X" : " ";
            return $"[{EventId}] {EventType} '{Name}' ({relatedEventId}) [{processedMarker}]";
        }
    }
}
