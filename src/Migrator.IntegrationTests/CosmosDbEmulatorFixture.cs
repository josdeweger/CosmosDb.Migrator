using Microsoft.Azure.Cosmos;

namespace CosmosDb.Migrator.IntegrationTests;

/// <summary>
/// Cosmos db emulator fixture, is run for every test class and disposed when all facts in a class have ran
/// 
/// https://docs.microsoft.com/en-us/azure/cosmos-db/linux-emulator?tabs=ssl-netstd21
/// 
/// Run the emulator in docker using:
/// docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254  -m 3g --cpus=2.0 --name=test-linux-emulator -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10 -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true -e AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE=$ipaddr -it mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
/// where $ipaddr is the ipaddress from your local machine, e.g.: ipaddr="`ifconfig | grep "inet " | grep -Fv 127.0.0.1 | awk '{print $2}' | head -n 1`"
/// </summary>
public class CosmosDbEmulatorFixture : IDisposable
{
    private readonly CosmosClient _client;
    private const string CosmosEndpoint = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseId = "testdb";
    public Database TestDatabase { get; }

    public CosmosDbEmulatorFixture()
    {
        _client = new CosmosClient(
            CosmosEndpoint,
            new CosmosClientOptions
            {
                AllowBulkExecution = true,
                ConnectionMode = ConnectionMode.Direct
            });
        
        TestDatabase = CreateEmptyDatabase().GetAwaiter().GetResult();
    }

    private async Task<Database> CreateEmptyDatabase()
    {
        try
        {
            var database = _client.GetDatabase(DatabaseId);
            await database.DeleteAsync();
        }
        catch (CosmosException)
        {
            //ignore, db does not exist, which is what we want
        }

        var response = await _client.CreateDatabaseAsync(DatabaseId);

        return response.Database;
    }

    public async Task<Container> CreateEmptyContainer(string container, string partitionKeyPath)
    {
        try
        {
            var existingContainer = TestDatabase.GetContainer(container);

            if (existingContainer is not null)
            {
                await existingContainer.DeleteContainerAsync();
            }
        }
        catch (CosmosException)
        {
            //ignore, container does not exist, which is what we want
        }
        
        ContainerProperties props = new(container, partitionKeyPath);
        var containerResponse = await TestDatabase.CreateContainerIfNotExistsAsync(props);

        return containerResponse.Container;
    }
    
    public async Task SeedContainer<T>(Container container, List<T> documents)
    {
        foreach (var document in documents)
        {
            await container.UpsertItemAsync(document);
        }
    }
    
    public async Task SafelyDeleteContainer(string containerName)
    {
        try
        {
            var existingContainer = TestDatabase.GetContainer(containerName);
            await existingContainer.DeleteContainerAsync();
        }
        catch (CosmosException)
        {
            //ignore, container does not exist, which is what we want
        }
    }
    
    private void ReleaseUnmanagedResources()
    {
        _client.Dispose();
    }

    public void Dispose()
    {
        try
        {
            TestDatabase.DeleteAsync().GetAwaiter().GetResult();
        }
        catch (CosmosException)
        {
        }
        
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~CosmosDbEmulatorFixture()
    {
        ReleaseUnmanagedResources();
    }
}
