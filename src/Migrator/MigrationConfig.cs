namespace CosmosDb.Migrator;

public sealed class MigrationConfig
{
    private readonly Dictionary<(Type from, Type to), Delegate> _factories = new();
    
    public MigrationType MigrationType { get; private set; }
    public Type FromType { get; private set; } = default!;
    public Type ToType { get; private set; } = default!;
    public string CollectionName { get; }
    public string FromCollectionName { get; private set; } = default!;
    public string ToCollectionName { get; private set; } = default!;
    public string PartitionKeyPath { get; private set; } = default!;
    public string PartitionKey { get; private set; } = default!;
    public string DocumentType { get; private set; } = default!;
    public string EmptyDocumentType => "None";

    public MigrationConfig(string collectionName)
    {
        CollectionName = collectionName;
    }
    
    public MigrationConfig WithPartitionKey(string path, string key)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException(nameof(path));
        }
        
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException(nameof(key));
        }

        PartitionKeyPath = path;
        PartitionKey = key;
        
        return this;
    }

    /// <summary>
    /// Filter by documents that have no documentType field
    /// </summary>
    /// <param name="documentType"></param>
    /// <returns>MigrationConfig</returns>
    public MigrationConfig WithoutDocumentType()
    {
        if (!string.IsNullOrEmpty(DocumentType))
        {
            throw new ArgumentException($"Document Type already set to {DocumentType}. 'WithoutDocumentType' and " +
                                        "ForDocumentType can not be used at the same time");
        }
        
        MigrationType = MigrationType.DocumentMigration;
        DocumentType = EmptyDocumentType;

        return this;
    }
    
    /// <summary>
    /// Set the document type to filter by
    /// </summary>
    /// <param name="documentType"></param>
    /// <returns>MigrationConfig</returns>
    public MigrationConfig ForDocumentType(string documentType)
    {
        if (string.IsNullOrEmpty(documentType))
        {
            throw new ArgumentException(nameof(documentType));
        }

        if (documentType.Equals(EmptyDocumentType))
        {
            throw new ArgumentException("Document Type already set to none. 'WithoutDocumentType' and " +
                                        "ForDocumentType can not be used at the same time");
        }

        MigrationType = MigrationType.DocumentMigration;
        DocumentType = documentType;
        
        return this;
    }

    /// <summary>
    /// Use this method to rename a collection
    /// CAUTION: CosmosDb does not know the concept of a container rename. Instead, this method will create a new
    /// container, copy over all the items from the old container, and delete it
    /// </summary>
    /// <param name="fromCollection"></param>
    public MigrationConfig RenameFrom(string fromCollection)
    {
        MigrationType = MigrationType.CollectionRename;
        FromCollectionName = fromCollection;
        return this;
    }

    /// <summary>
    /// Use this method to set the new collection name, use in combination with RenameFrom
    /// </summary>
    /// <param name="toCollection"></param>
    public void RenameTo(string toCollection)
    {
        ToCollectionName = toCollection;
    }

    public MigrationConfig Migrate<TOld, TNew>(Func<TOld, TNew> func) where TOld : IMigratable where TNew : IMigratable
    {   
        if (string.IsNullOrEmpty(PartitionKeyPath))
        {
            throw new ArgumentException(nameof(PartitionKeyPath));
        }
        
        if (string.IsNullOrEmpty(PartitionKey))
        {
            throw new ArgumentException(nameof(PartitionKey));
        }
        
        FromType = typeof(TOld);
        ToType = typeof(TNew);
        _factories[(typeof(TOld), typeof(TNew))] = func;

        return this;
    }

    public object Invoke(object input)
    {
        if (_factories.TryGetValue((FromType, ToType), out var fn))
        {
            return fn.DynamicInvoke(input);
        }

        throw new Exception($"Could not find registered delegate for type from: {FromType} and type to: {ToType}");
    }
}

public enum MigrationType
{
    CollectionRename,
    DocumentMigration
}
