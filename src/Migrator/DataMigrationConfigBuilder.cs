using Microsoft.Azure.Cosmos;

namespace CosmosDb.Migrator;

public sealed class DataMigrationConfigBuilder
{
    private readonly List<Delegate> _conditions = new();
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

    public DataMigrationConfigBuilder WithoutDocumentType()
    {
        _documentType = DataMigrationConfig.EmptyDocumentType;

        return this;
    }
    
    public DataMigrationConfigBuilder AddCondition<TOld>(Func<Container, TOld, Task<bool>> func) 
        where TOld : IMigratable 
    {   
        _conditions.Add(func);

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

        return new DataMigrationConfig(
            _collectionName, 
            _partitionKey,
            _partitionKeyPath,
            _documentType, 
            typeof(TOld), 
            typeof(TNew), 
            _conditions,
            func);
    }
    
    public DataMigrationConfig Migrate<TOld, TNew>(Func<Container, TOld, Task<TNew>> func) 
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

        return new DataMigrationConfig(
            _collectionName, 
            _partitionKey,
            _partitionKeyPath,
            _documentType, 
            typeof(TOld), 
            typeof(TNew), 
            _conditions,
            func);
    }
}
