namespace CosmosDb.Migrator;

public abstract class MigrationConfig
{
    public string CollectionName { get; } = default!;
    public string PartitionKeyPath { get; } = default!;
    public string PartitionKey { get; } = default!;

    public MigrationConfig(string collectionName, string partitionKey, string partitionKeyPath)
    {
        CollectionName = collectionName;
        PartitionKey = partitionKey;
        PartitionKeyPath = partitionKeyPath;
    }
}
