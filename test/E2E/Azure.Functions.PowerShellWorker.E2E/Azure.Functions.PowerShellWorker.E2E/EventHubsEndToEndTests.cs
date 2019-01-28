// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Functions.PowerShell.Tests.E2E
{
    public class EventHubsEndToEndTests 
    {
        [Fact]
        public async Task EventHubTriggerAndOutputJSON_Succeeds()
        {
            string expectedEventId = Guid.NewGuid().ToString();
            try
            {
                await SetupQueue(Constants.EventHubs.Json_Test.OutputName);

                // Need to setup EventHubs: test-inputjson-powershell and test-outputjson-powershell
                await EventHubsHelpers.SendJSONMessagesAsync(expectedEventId, Constants.EventHubs.Json_Test.InputName);

                //Verify
                var queueMessage = await StorageHelpers.ReadFromQueue(Constants.EventHubs.Json_Test.OutputName);
                JObject json = JObject.Parse(queueMessage);
                Assert.Contains(expectedEventId, json["value"].ToString());
            }
            finally
            {
                //Clear queue
                await StorageHelpers.ClearQueue(Constants.EventHubs.Json_Test.OutputName);
            }
        }

        [Fact]
        public async Task EventHubTriggerAndOutputString_Succeeds()
        {
            string expectedEventId = Guid.NewGuid().ToString();
            try
            {
                await SetupQueue(Constants.EventHubs.String_Test.OutputName);

                // Need to setup EventHubs: test-input-one-ps
                await EventHubsHelpers.SendMessagesAsync(expectedEventId, Constants.EventHubs.String_Test.InputName);

                //Verify
                var queueMessage = await StorageHelpers.ReadFromQueue(Constants.EventHubs.String_Test.OutputName);
                Assert.Contains(expectedEventId, queueMessage);
            }
            finally
            {
                //Clear queue
                await StorageHelpers.ClearQueue(Constants.EventHubs.String_Test.OutputName);
            }
        }

        [Fact]
        public async Task EventHubTriggerCardinalityOne_Succeeds()
        {
            string expectedEventId = Guid.NewGuid().ToString();
            try
            {
                await SetupQueue(Constants.EventHubs.Cardinality_One_Test.OutputName);

                // Need to setup EventHubs: test-inputOne-powershell and test-outputone-powershell
                await EventHubsHelpers.SendMessagesAsync(expectedEventId, Constants.EventHubs.Cardinality_One_Test.InputName);

                //Verify
                IEnumerable<string> queueMessages = await StorageHelpers.ReadMessagesFromQueue(Constants.EventHubs.Cardinality_One_Test.OutputName);
                Assert.True(queueMessages.All(msg => msg.Contains(expectedEventId)));
            }
            finally
            {
                //Clear queue
                await StorageHelpers.ClearQueue(Constants.EventHubs.Cardinality_One_Test.OutputName);
            }
        }

        private static async Task SetupQueue(string queueName)
        {
            //Clear queue
            await StorageHelpers.ClearQueue(queueName);

            //Set up and trigger            
            await StorageHelpers.CreateQueue(queueName);
        }
    }
}
