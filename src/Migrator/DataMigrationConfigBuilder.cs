namespace CosmosDb.Migrator;

public sealed class DataMigrationConfigBuilder
{
    private readonly Dictionary<(Type from, Type to), Delegate> _factories = new();
    private string _collectionName = null!;
    private string _partitionKey = null!;
    private string _partitionKeyPath = null!;
    private string _documentType = null!;

    public DataMigrationConfigBuilder WithCollectionName(string collectionName)
    {
        _collectionName = collectionName;

        return this;
    }
    
    public DataMigrationConfigBuilder WithPartitionKey(string key, string path)
    {
        _partitionKey = key;
        _partitionKeyPath = path;

        return this;
    }


    public DataMigrationConfigBuilder ForDocumentType(string documentType)
    {
        _documentType = documentType;

        return this;
    }


    public DataMigrationConfig Migrate<TOld, TNew>(Func<TOld, TNew> func) 
        where TOld : IMigratable 
        where TNew : IMigratable
    {   
        if (string.IsNullOrEmpty(_partitionKeyPath))
        {
            throw new ArgumentException(nameof(_partitionKeyPath));
        }
        
        if (string.IsNullOrEmpty(_partitionKey))
        {
            throw new ArgumentException(nameof(_partitionKey));
        }
        
        _factories[(typeof(TOld), typeof(TNew))] = func;

        return new DataMigrationConfig(
            _collectionName, 
            _partitionKey,
            _partitionKeyPath,
            _documentType, 
            typeof(TOld), 
            typeof(TNew), 
            _factories);
    }
}
