using CosmosDb.Migrator.IntegrationTests.Documents;
using CosmosDb.Migrator.IntegrationTests.Migrations;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CosmosDb.Migrator.IntegrationTests;

public class ChangeContainerNameUpTests : IAsyncLifetime, IClassFixture<CosmosDbEmulatorFixture>
{
    private readonly CosmosDbEmulatorFixture _emulatorFixture;
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly JsonSerializer _serializer = new();

    // Is run for every test
    public ChangeContainerNameUpTests(CosmosDbEmulatorFixture emulatorFixture)
    {   
        _emulatorFixture = emulatorFixture;
    }

    // Async helper init method, is run for every test
    public async Task InitializeAsync()
    {
        await _emulatorFixture.CreateEmptyContainer("test", "/id");
    }
    
    [Fact]
    public async Task GivenAnEmptyCollection_WhenRenamingTheCollectionUp_TheNewCollectionCanBeRetrieved()
    {
        var migrations = new List<Type> {typeof(ChangeContainerNameTestToTest2)};
        var runner = new MigrationRunner(_emulatorFixture.TestDatabase, _loggerMock.Object, migrations, _serializer);

        await runner.MigrateUp();

        var containerReference = _emulatorFixture.TestDatabase.GetContainer("test2");
        var result = async () => await containerReference.ReadContainerAsync();
        
        await result.Should().NotThrowAsync<CosmosException>();
    }
    
    [Fact]
    public async Task GivenAnEmptyCollection_WhenRenamingTheCollectionUpAndDown_TheCollectionCanBeRetrievedByOldCollectionName()
    {
        var migrations = new List<Type> {typeof(ChangeContainerNameTestToTest2)};
        var runner = new MigrationRunner(_emulatorFixture.TestDatabase, _loggerMock.Object, migrations, _serializer);

        await runner.MigrateUp();
        await runner.MigrateDown(0);

        var containerReference = _emulatorFixture.TestDatabase.GetContainer("test");
        var result = async () => await containerReference.ReadContainerAsync();
        
        await result.Should().NotThrowAsync<CosmosException>();
    }
    
    [Fact]
    public async Task GivenCollection_WhenRenamingTheCollectionUp_TheNewCollectionContainsCopiedDocuments()
    {
        var generatedDocs = GenerateTestDataDocuments(10);
        var oldContainerReference = _emulatorFixture.TestDatabase.GetContainer("test");
        await _emulatorFixture.SeedContainer(oldContainerReference, generatedDocs);
        
        var migrations = new List<Type> {typeof(ChangeContainerNameTestToTest2)};
        var runner = new MigrationRunner(_emulatorFixture.TestDatabase, _loggerMock.Object, migrations, _serializer);

        await runner.MigrateUp();

        var newContainerReference = _emulatorFixture.TestDatabase.GetContainer("test2");
        
        var query = new QueryDefinition("select * from c where c.documentType = @documentType")
            .WithParameter("@documentType", "TestDataDocument");

        using var iterator =
            newContainerReference.GetItemQueryStreamIterator(query, null,
                new QueryRequestOptions {MaxConcurrency = -1});

        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync();
            using var sr = new StreamReader(response.Content);
            using var jtr = new JsonTextReader(sr);

            var result = await JObject.LoadAsync(jtr);

            result.GetValue("Documents").Should().HaveCount(generatedDocs.Count);
        }
    }

    private List<TestDataDocument> GenerateTestDataDocuments(int nrToGenerate)
    {
        var list = new List<TestDataDocument>();

        for (var i = 1; i <= nrToGenerate; i++)
        {
            var doc = new TestDataDocument(
                Id: i.ToString(), 
                DocumentType: nameof(TestDataDocument), 
                Version: null, 
                SomeString: $"This is document {i}", 
                SomeBoolStr: i % 2 == 0 ? false.ToString() : true.ToString(), 
                SomeDateTimeStr: new DateTime(2022, 4, 19).ToString("yyyy-MM-dd"));
            
            list.Add(doc);
        }

        return list;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
