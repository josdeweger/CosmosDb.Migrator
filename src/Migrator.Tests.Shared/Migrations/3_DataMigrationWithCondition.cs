using System.Globalization;
using CosmosDb.Migrator.Tests.Shared.Documents;
using Microsoft.Azure.Cosmos;

namespace CosmosDb.Migrator.Tests.Shared.Migrations;

[Migration(version: 3)]
public class RemoveDuplicateRecordsWithDifferentIds : CosmosDbMigration
{
    public override void Up()
    {
        MigrateDataInCollection(cfg => cfg
            .WithCollectionName("test-data")
            .WithPartitionKey(key: "id", path: "/id")
            .ForDocumentType("TestDataDocument")
            .AddCondition<TestDataDocument>(async (collection, testData) =>
            {
                //if id starts with upper, see if there is a document with a key starting with lower
                //or if id starts with lower, see if there is a document with a key starting with upper
                var key = string.Concat(
                    Char.IsUpper(testData.Id[0])
                        ? testData.Id[0].ToString().ToLower()
                        : testData.Id[0].ToString().ToUpper(), testData.Id.AsSpan(1));
                
                //get the 'duplicate' doc
                ItemResponse<TestDataDocument> response;
                
                try
                {
                    response = await collection.ReadItemAsync<TestDataDocument>(key, new PartitionKey(key));
                    
                    if (response?.Resource is null)
                    {
                        //no duplicate, condition is met
                        return true;
                    }
                }
                catch (CosmosException)
                {
                    //no duplicate, condition is met
                    return true;
                }

                //if the current doc is older than the duplicate
                if (testData._ts <= response.Resource._ts)
                {
                    return false;
                }

                //this is the document we want to keep, condition is met, so modify the id to have only lower case
                return true;
            })
            .Migrate<TestDataDocument, TestDataDocument>(async (collection, testData) =>
            {
                //if the document id starts with a capital, remove it
                if (Char.IsUpper(testData.Id[0]))
                {
                    await collection.DeleteItemAsync<TestDataDocument>(testData.Id, new PartitionKey(testData.Id));
                }
                
                //return the new document with lowercase id 
                testData.Id = testData.Id.ToLower();
                
                return testData;
            }));
    }

    public override void Down()
    {
        
    }
}
