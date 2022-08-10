namespace CosmosDb.Migrator;

public sealed class CollectionRenameConfigBuilder
{
    private string _collectionName = null!;
    private string _partitionKey = null!;
    private string _partitionKeyPath = null!;
    
    public CollectionRenameConfigBuilder WithCollectionName(string collectionName)
    {
        _collectionName = collectionName;

        return this;
    }

    public CollectionRenameConfigBuilder WithPartitionKey(string key, string path)
    {
        _partitionKey = key;
        _partitionKeyPath = path;

        return this;
    }

    public CollectionRenameConfig RenameTo(string toCollectionName)
    {
        return new CollectionRenameConfig(_collectionName, _partitionKey, _partitionKeyPath, toCollectionName);
    }
}
