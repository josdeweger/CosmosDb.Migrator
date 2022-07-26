using AutoFixture;
using CosmosDb.Migrator;
using CosmosDb.Migrator.Tests.Shared;
using CosmosDb.Migrator.Tests.Shared.Documents;
using CosmosDb.Migrator.Tests.Shared.Migrations;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;

namespace Migrator.UnitTests;

public class MigrationRunnerTests
{
    private readonly Mock<ILogger> _logger = new();
    
    [Fact]
    public async Task GivenDocumentsWithOldVersion_WhenMigrationToNewVersion_ItMigratesFields()
    {
        var someString1 = "Foo";
        var someBool1 = true;
        var someDateTime1 = new DateTime(2022, 01, 15);
        
        var someString2 = "Bar";
        var someBool2 = false;
        var someDateTime2 = new DateTime(2022, 02, 28);
        
        var dbMockBuilder = new DatabaseMockBuilder()
            .WithContainer("test-data")
            .WithVersionDocument(1)
            .WithQueryResult(fixture =>
            {
                var docs = new List<TestDataDocument>();

                var doc1 = fixture
                    .Build<TestDataDocument>()
                    .With(x => x.Id, Guid.NewGuid().ToString)
                    .With(x => x.SomeString, someString1)
                    .With(x => x.SomeBoolStr, someBool1.ToString)
                    .With(x => x.SomeDateTimeStr, someDateTime1.ToString("yyyy-MM-dd"))
                    .Create();
                
                var doc2 = fixture
                    .Build<TestDataDocument>()
                    .With(x => x.Id, Guid.NewGuid().ToString)
                    .With(x => x.SomeString, someString2)
                    .With(x => x.SomeBoolStr, someBool2.ToString)
                    .With(x => x.SomeDateTimeStr, someDateTime2.ToString("yyyy-MM-dd"))
                    .Create();
                
                docs.Add(doc1);
                docs.Add(doc2);
                
                return docs;
            });

        var dbMock = dbMockBuilder.Build();
        var migrationTypes = new List<Type>{ typeof(TestDataMigration) };
        var migrator = new MigrationRunner(dbMock.Object, _logger.Object, migrationTypes);;
    
        await migrator.MigrateUp();

        dbMockBuilder.ContainerMock.Verify(x =>
            x.UpsertItemAsync<IMigratable>(
                It.Is<TestDataDocumentV2>(x => x.SomeString == someString1 &&
                                             x.SomeBool == someBool1 &&
                                             x.SomeDateTime == someDateTime1),
                It.IsAny<PartitionKey>(), 
                It.IsAny<ItemRequestOptions>(), 
                It.IsAny<CancellationToken>()));
        
        dbMockBuilder.ContainerMock.Verify(x =>
            x.UpsertItemAsync<IMigratable>(
                It.Is<TestDataDocumentV2>(x => x.SomeString == someString2 &&
                                               x.SomeBool == someBool2 &&
                                               x.SomeDateTime == someDateTime2),
                It.IsAny<PartitionKey>(), 
                It.IsAny<ItemRequestOptions>(), 
                It.IsAny<CancellationToken>()));
    }
}
