using System.Globalization;
using CosmosDb.Migrator.IntegrationTests.Documents;

namespace CosmosDb.Migrator.IntegrationTests.Migrations;

[Migration(version: 20220419081800)]
public class TestDataMigration : CosmosDbMigration
{
    public TestDataMigration() : base("test-data")
    {
    }
    
    public override void Up()
    {
        OnCollection()
            .WithPartitionKey(path: "/id", key: "id")
            .ForDocumentType("TestDataDocument")
            .Migrate<TestDataDocument, TestDataDocumentV2>(testData => new TestDataDocumentV2(
                Id: testData.Id,
                DocumentType: nameof(TestDataDocumentV2),
                Version: 20220419081800,
                SomeString: testData.SomeString,
                SomeBool: bool.Parse(testData.SomeBoolStr),
                SomeDateTime: DateTime.ParseExact(testData.SomeDateTimeStr, "yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    public override void Down()
    {
        OnCollection()
            .WithPartitionKey(path: "/id", key: "id")
            .ForDocumentType("TestDataDocumentV2")
            .Migrate<TestDataDocumentV2, TestDataDocument>(testDataV2 => new TestDataDocument(
                Id: testDataV2.Id,
                DocumentType: nameof(TestDataDocument),
                Version: 0,
                SomeString: testDataV2.SomeString,
                SomeBoolStr: testDataV2.SomeBool.ToString(),
                SomeDateTimeStr: testDataV2.SomeDateTime.ToString("yyyy-MM-dd")));
    }
}
