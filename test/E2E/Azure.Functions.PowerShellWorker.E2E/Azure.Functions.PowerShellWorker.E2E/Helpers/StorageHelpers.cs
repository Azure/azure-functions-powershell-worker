// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azure.Functions.PowerShell.Tests.E2E
{
    class StorageHelpers
    {
        public static CloudStorageAccount _storageAccount = CloudStorageAccount.Parse(Constants.Queue.StorageConnectionStringSetting);
        public static CloudQueueClient _queueClient = _storageAccount.CreateCloudQueueClient();

        public async static Task DeleteQueue(string queueName)
        {
            CloudQueue queue = _queueClient.GetQueueReference(queueName);
            await queue.DeleteAsync();
        }

        public async static Task ClearQueue(string queueName)
        {
            CloudQueue queue = _queueClient.GetQueueReference(queueName);
            if (await queue.ExistsAsync())
            {
                await queue.ClearAsync();
            }
        }

        public async static Task CreateQueue(string queueName)
        {
            CloudQueue queue = _queueClient.GetQueueReference(queueName);
            await queue.CreateIfNotExistsAsync();
        }

        public async static Task<string> InsertIntoQueue(string queueName, string queueMessage)
        {
            CloudQueue queue = _queueClient.GetQueueReference(queueName);
            await queue.CreateIfNotExistsAsync();
            CloudQueueMessage message = new CloudQueueMessage(queueMessage);            
            await queue.AddMessageAsync(message);
            return message.Id;
        }

        public async static Task<string> ReadFromQueue(string queueName)
        {
            CloudQueue queue = _queueClient.GetQueueReference(queueName);
            CloudQueueMessage retrievedMessage = null;
            await Utilities.RetryAsync(async () =>
            {
                retrievedMessage = await queue.GetMessageAsync();
                return retrievedMessage != null;
            });
            await queue.DeleteMessageAsync(retrievedMessage);
            return retrievedMessage.AsString;
        }

        public async static Task<IEnumerable<string>> ReadMessagesFromQueue(string queueName)
        {
            CloudQueue queue = _queueClient.GetQueueReference(queueName);
            IEnumerable<CloudQueueMessage> retrievedMessages = null;
            List<string> messages = new List<string>();
            await Utilities.RetryAsync(async () =>
            {
                retrievedMessages = await queue.GetMessagesAsync(3);
                return retrievedMessages != null;
            });
            foreach(CloudQueueMessage msg in retrievedMessages)
            {
                messages.Add(msg.AsString);
                await queue.DeleteMessageAsync(msg);
            }
            return messages;
        }
    }
}
