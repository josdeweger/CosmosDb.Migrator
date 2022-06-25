using System.Globalization;
using CosmosDb.Migrator.IntegrationTests.Documents;
using CosmosDb.Migrator.IntegrationTests.Migrations;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDb.Migrator.IntegrationTests;

public class TestDataMigrationUpTests : IAsyncLifetime, IClassFixture<CosmosDbEmulatorFixture>
{
    private readonly CosmosDbEmulatorFixture _emulatorFixture;
    private readonly Mock<ILogger> _loggerMock = new(); 
    private Container _testDataContainer = default!;
    private readonly JsonSerializer _serializer = new();
    private const long NewVersion = 20220419081800;
    private List<TestDataDocument> TestDataList => CreateTestData();

    // Is run for every test
    public TestDataMigrationUpTests(CosmosDbEmulatorFixture emulatorFixture)
    {   
        _emulatorFixture = emulatorFixture;
    }

    // Async helper init method, is run for every test
    public async Task InitializeAsync()
    {
        _testDataContainer = await _emulatorFixture.CreateEmptyContainer("test-data", "/id");
        await _emulatorFixture.SeedContainer(_testDataContainer, TestDataList);
    }
    
    [Fact]
    public async Task GivenExistingTestData_WhenMigratingToNewVersion_DocumentsAreMigrated()
    {
        var migrations = new List<Type> {typeof(TestDataMigration)};
        var runner = new MigrationRunner(_emulatorFixture.TestDatabase, _loggerMock.Object, migrations, _serializer);

        await runner.MigrateUp();
        
        foreach (var testData in TestDataList)
        {
            var result =
                await _testDataContainer.ReadItemAsync<TestDataDocumentV2>(testData.Id,
                    new PartitionKey(testData.Id));

            using var scope = new AssertionScope();
            result.Resource.Should().NotBeNull();
            result.Resource.Id.Should().Be(testData.Id);
            result.Resource.Version.Should().Be(NewVersion);
            result.Resource.DocumentType.Should().Be(nameof(TestDataDocumentV2));
            result.Resource.SomeString.Should().Be(testData.SomeString);
            result.Resource.SomeBool.Should().Be(bool.Parse(testData.SomeBoolStr));
            result.Resource.SomeDateTime.Should().Be(DateTime.ParseExact(testData.SomeDateTimeStr, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }
    
    [Fact]
    public async Task GivenExistingTestData_WhenMigratingUpAndDown_DocumentsAreTheSameAsBefore()
    {
        var migrations = new List<Type> {typeof(TestDataMigration)};
        var runner = new MigrationRunner(_emulatorFixture.TestDatabase, _loggerMock.Object, migrations, _serializer);

        await runner.MigrateUp();
        
        await runner.MigrateDown(0);
        
        foreach (var testData in TestDataList)
        {
            var result =
                await _testDataContainer.ReadItemAsync<TestDataDocument>(testData.Id,
                    new PartitionKey(testData.Id));

            using var scope = new AssertionScope();
            result.Resource.Should().BeEquivalentTo(testData);
        }
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
    
    private List<TestDataDocument> CreateTestData()
    {
        return new List<TestDataDocument>
        {
            new("1", nameof(TestDataDocument), 0, "This is some string", "False", "2022-01-15"),
            new("2", nameof(TestDataDocument), 0, "This is another string", "True", "2022-02-16"),
            new("3", nameof(TestDataDocument), 0, "Yet another string", "False", "2022-04-17")
        };
    }
}
