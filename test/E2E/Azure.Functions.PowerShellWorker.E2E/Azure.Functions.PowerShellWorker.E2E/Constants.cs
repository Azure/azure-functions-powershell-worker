// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Azure.Functions.PowerShell.Tests.E2E
{
    public static class Constants
    {
        public static string FunctionsHostUrl = Environment.GetEnvironmentVariable("FunctionAppUrl") ?? "http://localhost:7071";

        // Xunit Fixtures and Collections
        public const string FunctionAppCollectionName = "FunctionAppCollection";

        //Queue tests
        public static class Queue {
            public static string StorageConnectionStringSetting = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            public static string OutputBindingName = "test-output-ps";
            public static string InputBindingName = "test-input-ps";
        }

        // CosmosDB tests
        public static class CosmosDB {
            public static string CosmosDBConnectionStringSetting = Environment.GetEnvironmentVariable("AzureWebJobsCosmosDBConnectionString");
            public static string DbName = "ItemDb";
            public static string InputCollectionName = "ItemCollectionIn";
            public static string OutputCollectionName = "ItemCollectionOut";
            public static string LeaseCollectionName = "leases";
        }

        // EventHubs
        public static class EventHubs {
            public static string EventHubsConnectionStringSetting = Environment.GetEnvironmentVariable("AzureWebJobsEventHubSender");

            public static class Json_Test {
                public static string OutputName = "test-output-object-ps";
                public static string InputName = "test-input-object-ps";
            }
            
            public static class String_Test {
                public static string OutputName = "test-output-string-ps";
                public static string InputName = "test-input-string-ps";
            }

            public static class Cardinality_One_Test {
                public static string InputName = "test-input-one-ps";
                public static string OutputName = "test-output-one-ps";
            }
        }
    }
}
