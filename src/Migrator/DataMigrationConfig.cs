using Microsoft.Azure.Cosmos;

namespace CosmosDb.Migrator;

public sealed class DataMigrationConfig : MigrationConfig
{
    private readonly List<Func<Container, Task<bool>>> _conditions;
    private readonly Delegate _migrationDelegate;
    public Type FromType { get; }
    public Type ToType { get; }
    public string DocumentType { get; }
    public string EmptyDocumentType => "None";

    public DataMigrationConfig(string collectionName, string partitionKey, string partitionKeyPath, string documentType,
        Type fromType, Type toType, List<Func<Container, Task<bool>>> conditions, Delegate migrationDelegate)
        : base(collectionName, partitionKey, partitionKeyPath)
    {
        DocumentType = documentType;
        FromType = fromType;
        ToType = toType;

        _conditions = conditions;
        _migrationDelegate = migrationDelegate;
    }

    public async Task<bool> AreConditionsMet(Container container)
    {
        foreach (var condition in _conditions)
        {
            var result = await condition(container);
            
            if (!result)
            {
                return false;
            }
        }

        return true;
    }
    
    public object Invoke(object input)
    {
        if (_migrationDelegate is null)
        {
            throw new Exception($"Delegate is not set for type from: {FromType} and type to: {ToType}");
        }
        
        return _migrationDelegate.DynamicInvoke(input);
    }
}
