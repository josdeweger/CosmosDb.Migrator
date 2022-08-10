namespace CosmosDb.Migrator;

public sealed class CollectionRenameConfig : MigrationConfig
{
    public string ToCollectionName { get; }

    public CollectionRenameConfig(string collectionName, string partitionKey, string partitionKeyPath,
        string toCollectionName)
        : base(collectionName, partitionKey, partitionKeyPath)
    {
        ToCollectionName = toCollectionName;
    }
}
