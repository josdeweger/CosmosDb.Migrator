using System.Globalization;
using CosmosDb.Migrator.Tests.Shared.Documents;

namespace CosmosDb.Migrator.Tests.Shared.Migrations;

[Migration(version: 2)]
public class DataMigration : CosmosDbMigration
{   
    public override void Up()
    {
        MigrateDataInCollection(cfg => cfg
            .WithCollectionName("test-data")
            .WithPartitionKey(key: "id", path: "/id")
            .ForDocumentType("TestDataDocument")
            .Migrate<TestDataDocument, TestDataDocumentV2>(testData => new TestDataDocumentV2(
                Id: testData.Id,
                DocumentType: nameof(TestDataDocumentV2),
                Version: 2,
                SomeString: testData.SomeString,
                SomeBool: bool.Parse(testData.SomeBoolStr),
                SomeDateTime: DateTime.ParseExact(testData.SomeDateTimeStr, "yyyy-MM-dd", CultureInfo.InvariantCulture))));
    }

    public override void Down()
    {
        MigrateDataInCollection(cfg => cfg
            .WithCollectionName("test-data")
            .WithPartitionKey(key: "id", path: "/id")
            .ForDocumentType("TestDataDocumentV2")
            .Migrate<TestDataDocumentV2, TestDataDocument>(testDataV2 => new TestDataDocument(
                Id: testDataV2.Id,
                DocumentType: nameof(TestDataDocument),
                Version: 1,
                SomeString: testDataV2.SomeString,
                SomeBoolStr: testDataV2.SomeBool.ToString(),
                SomeDateTimeStr: testDataV2.SomeDateTime.ToString("yyyy-MM-dd"))));
    }
}
