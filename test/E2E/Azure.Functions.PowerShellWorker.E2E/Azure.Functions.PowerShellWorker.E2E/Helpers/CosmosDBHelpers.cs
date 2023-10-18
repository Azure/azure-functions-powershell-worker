// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Azure.Functions.PowerShell.Tests.E2E
{

    public static class CosmosDBHelpers
    {
        private static CosmosClient _cosmosDbClient;

        private class Document
        {
            public string id { get; set; }
        }

        static CosmosDBHelpers()
        {
            var builder = new System.Data.Common.DbConnectionStringBuilder();
            builder.ConnectionString = Constants.CosmosDB.CosmosDBConnectionStringSetting;
            var serviceUri = builder["AccountEndpoint"].ToString();
            _cosmosDbClient = new CosmosClient(serviceUri, builder["AccountKey"].ToString());
        }

        // keep
        public async static Task CreateDocument(string docId)
        {
            Document documentToTest = new Document()
            {
                id = docId
            };

            Container _inputContainer = _cosmosDbClient.GetContainer(Constants.CosmosDB.DbName, Constants.CosmosDB.InputCollectionName);

            Document insertedDoc = await _inputContainer.CreateItemAsync<Document>(documentToTest, new PartitionKey(documentToTest.id));
        }

        // keep
        public async static Task<string> ReadDocument(string docId)
        {
            Document retrievedDocument = null;
            await Utilities.RetryAsync(async () =>
            {
                try
                {
                    Container container = _cosmosDbClient.GetContainer(Constants.CosmosDB.DbName, Constants.CosmosDB.OutputCollectionName);

                    retrievedDocument = await container.ReadItemAsync<Document>(docId, new PartitionKey(docId));
                    return true;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return false;
                }
            }, 120000, 4000);
            return retrievedDocument.id;
        }

        // keep
        public async static Task DeleteTestDocuments(string docId)
        {
            await DeleteDocument(Constants.CosmosDB.DbName, Constants.CosmosDB.InputCollectionName, docId);
            await DeleteDocument(Constants.CosmosDB.DbName, Constants.CosmosDB.InputCollectionName, docId);
        }

        private async static Task DeleteDocument(string dbName, string collectionName, string docId)
        {
            try
            {
                Container container = _cosmosDbClient.GetContainer(dbName, collectionName);
                await container.DeleteItemAsync<Document>(dbName, new PartitionKey(dbName));
            }
            catch (Exception)
            {
                //ignore
            }
        }

        // keep
        public async static Task CreateDocumentCollections()
        {
            Database db = await _cosmosDbClient.CreateDatabaseIfNotExistsAsync(Constants.CosmosDB.DbName);

            await CreateCollection(Constants.CosmosDB.DbName, Constants.CosmosDB.InputCollectionName, "/id");
            await CreateCollection(Constants.CosmosDB.DbName, Constants.CosmosDB.OutputCollectionName, "/id");
            // While using extensions v2-3, the leases may not have a partition key, but the new SDK requires
            // one to manually create a collection. This comment may be removed and this line uncommented when
            // extension bundles for tests are updated. 
            //await CreateCollection(Constants.CosmosDB.DbName, Constants.CosmosDB.LeaseCollectionName, "/id");
        }
        public async static Task DeleteDocumentCollections()
        {
            await DeleteCollection(Constants.CosmosDB.DbName, Constants.CosmosDB.InputCollectionName);
            await DeleteCollection(Constants.CosmosDB.DbName, Constants.CosmosDB.OutputCollectionName);
            await DeleteCollection(Constants.CosmosDB.DbName, Constants.CosmosDB.LeaseCollectionName);
        }

        private async static Task DeleteCollection(string dbName, string collectionName)
        {
            try
            {
                Database database = _cosmosDbClient.GetDatabase(dbName);
                await database.GetContainer(collectionName).DeleteContainerAsync();
            }
            catch (Exception)
            {
                //Ignore
            }
        }

        private async static Task CreateCollection(string dbName, string collectionName, string partitionKey)
        {
            Database database = _cosmosDbClient.GetDatabase(dbName);
            IndexingPolicy indexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath
                    {
                        Path = "/*"
                    }
                }
            };
            var containerProperties = new ContainerProperties(collectionName, partitionKey)
            {
                IndexingPolicy = indexingPolicy
            };
            await database.CreateContainerIfNotExistsAsync(containerProperties, 400);
        }
    }
}
