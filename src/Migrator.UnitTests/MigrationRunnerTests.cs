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
    public async Task GivenDocumentsWithOldVersion_WhenMigratingToNewVersion_ItMigratesFields()
    {
        var containerName = "test-data";
        var someString1 = "Foo";
        var someBool1 = true;
        var someDateTime1 = new DateTime(2022, 01, 15);
        
        var someString2 = "Bar";
        var someBool2 = false;
        var someDateTime2 = new DateTime(2022, 02, 28);
        
        var dbMockBuilder = new DatabaseMockBuilder()
            .WithContainer<TestDataDocument>(containerName, c => c
                .WithVersionDocument(1)
                .AddDocument(fixture => fixture
                    .Build<TestDataDocument>()
                    .With(x => x.Id, Guid.NewGuid().ToString)
                    .With(x => x.SomeString, someString1)
                    .With(x => x.SomeBoolStr, someBool1.ToString)
                    .With(x => x.SomeDateTimeStr, someDateTime1.ToString("yyyy-MM-dd"))
                    .Create())
                .AddDocument(fixture => fixture
                    .Build<TestDataDocument>()
                    .With(x => x.Id, Guid.NewGuid().ToString)
                    .With(x => x.SomeString, someString2)
                    .With(x => x.SomeBoolStr, someBool2.ToString)
                    .With(x => x.SomeDateTimeStr, someDateTime2.ToString("yyyy-MM-dd"))
                    .Create())
                .Build());

        var dbMock = dbMockBuilder.Build();
        var migrationTypes = new List<Type>{ typeof(TestMigration) };
        var migrator = new MigrationRunner(dbMock.Object, _logger.Object, migrationTypes);;
    
        await migrator.MigrateUp();

        var containerMock = dbMockBuilder.GetContainerMock(containerName);

        containerMock.Verify(x => x.UpsertItemAsync<IMigratable>(
            It.Is<TestDataDocumentV2>(x => x.SomeString == someString1 &&
                                           x.SomeBool == someBool1 &&
                                           x.SomeDateTime == someDateTime1),
            It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);

        containerMock.Verify(x => x.UpsertItemAsync<IMigratable>(
            It.Is<TestDataDocumentV2>(x => x.SomeString == someString2 &&
                                           x.SomeBool == someBool2 &&
                                           x.SomeDateTime == someDateTime2),
            It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
