namespace CosmosDb.Migrator;

public sealed class DataMigrationConfig : MigrationConfig
{
    private readonly Dictionary<(Type from, Type to), Delegate> _factories = new();
    public Type FromType { get; } = default!;
    public Type ToType { get; } = default!;
    public string DocumentType { get; } = default!;
    public string EmptyDocumentType => "None";

    public DataMigrationConfig(string collectionName,
        string partitionKey, string partitionKeyPath,
        string documentType, Type fromType, Type toType,
        Dictionary<(Type from, Type to), Delegate> factories) 
        : base(collectionName, partitionKey, partitionKeyPath)
    {
        DocumentType = documentType;
        FromType = fromType;
        ToType = toType;

        _factories = factories;
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
