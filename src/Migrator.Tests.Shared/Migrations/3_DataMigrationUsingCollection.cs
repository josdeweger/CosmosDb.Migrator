using System.Globalization;
using CosmosDb.Migrator.Tests.Shared.Documents;
using Microsoft.Azure.Cosmos;

namespace CosmosDb.Migrator.Tests.Shared.Migrations;

[Migration(version: 3)]
public class DataMigrationUsingCollection : DataMigration
{
    public override void Up()
    {
        MigrateDataInCollection(cfg => cfg
            .WithCollectionName("test-data")
            .WithPartitionKey(key: "id", path: "/id")
            .ForDocumentType("TestDataDocument")
            .AddCondition<TestDataDocument>(async (collection, testData) =>
            {
                //if id starts with upper, see if there is a doc with a key starting with lower and vice versa
                var key = string.Concat(
                    Char.IsUpper(testData.Id[0])
                        ? testData.Id[0].ToString().ToLower()
                        : testData.Id[0].ToString().ToUpper(), testData.Id.AsSpan(1));
                
                //get the 'duplicate' doc
                var response = await collection.ReadItemAsync<TestDataDocument>(key, new PartitionKey(key));
                    
                if (response.Resource is null)
                {
                    return false;
                }

                //if the current doc is older than the duplicate, remove this one
                if (testData.Timestamp < response.Resource.Timestamp)
                {
                    await collection.DeleteItemAsync<TestDataDocument>(testData.Id, new PartitionKey(testData.Id));
                        
                    return false;
                }

                //else migrate this one
                return true;
            })
            .Migrate<TestDataDocument, TestDataDocument>(testData =>
            {
                testData.Id = testData.Id.ToLower();
                
                return testData;
            }));
    }
}
