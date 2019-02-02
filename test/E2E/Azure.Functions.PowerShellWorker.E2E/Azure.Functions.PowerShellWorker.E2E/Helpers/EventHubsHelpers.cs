// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Functions.PowerShell.Tests.E2E
{
    public class EventHubsHelpers
    {
        public static async Task SendJSONMessagesAsync(string eventId, string eventHubName)
        {
            // write 3 events
            List<EventData> events = new List<EventData>();
            string[] ids = new string[3];
            for (int i = 0; i < 3; i++)
            {
                ids[i] = eventId + $"TestEvent{i}";
                JObject jo = new JObject
                {
                    { "value", ids[i] }
                };
                var evt = new EventData(Encoding.UTF8.GetBytes(jo.ToString(Formatting.None)));
                evt.Properties.Add("TestIndex", i);
                events.Add(evt);
            }

            EventHubsConnectionStringBuilder builder = new EventHubsConnectionStringBuilder(Constants.EventHubs.EventHubsConnectionStringSetting);
            builder.EntityPath = eventHubName;
            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(builder.ToString());
            await eventHubClient.SendAsync(events);
        }

        public static async Task SendMessagesAsync(string eventId, string evenHubName)
        {
            // write 3 events
            List<EventData> events = new List<EventData>();
            string[] ids = new string[3];
            for (int i = 0; i < 3; i++)
            {
                ids[i] = eventId + $"TestEvent{i}";
                var evt = new EventData(Encoding.UTF8.GetBytes(ids[i]));
                evt.Properties.Add("TestIndex", i);
                events.Add(evt);
            }

            EventHubsConnectionStringBuilder builder = new EventHubsConnectionStringBuilder(Constants.EventHubs.EventHubsConnectionStringSetting);
            builder.EntityPath = evenHubName;
            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(builder.ToString());
            await eventHubClient.SendAsync(events);
        }
    }
}
