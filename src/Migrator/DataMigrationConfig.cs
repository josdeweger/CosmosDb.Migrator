using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;

namespace CosmosDb.Migrator;

public sealed class DataMigrationConfig : MigrationConfig
{
    private readonly List<Delegate> _conditions;
    private readonly Delegate _migrationDelegate;
    public Type FromType { get; }
    public Type ToType { get; }
    public string DocumentType { get; }
    public string EmptyDocumentType => "None";

    public DataMigrationConfig(string collectionName, string partitionKey, string partitionKeyPath, string documentType,
        Type fromType, Type toType, List<Delegate> conditions, Delegate migrationDelegate)
        : base(collectionName, partitionKey, partitionKeyPath)
    {
        DocumentType = documentType;
        FromType = fromType;
        ToType = toType;

        _conditions = conditions;
        _migrationDelegate = migrationDelegate;
    }

    public bool AreConditionsMet(Container container, object oldDoc)
    {
        foreach (var condition in _conditions)
        {
            dynamic tmp = condition.DynamicInvoke(container, oldDoc);
            var result = (bool)tmp.GetAwaiter().GetResult();
            
            if (!result)
            {
                return false;
            }
        }

        return true;
    }
    
    public object Invoke(Container container, object input)
    {
        if (_migrationDelegate is null)
        {
            throw new Exception($"Delegate is not set for type from: {FromType} and type to: {ToType}");
        }

        if (_migrationDelegate.Method.IsDefined(typeof(AsyncStateMachineAttribute), false))
        {
            dynamic result = _migrationDelegate.DynamicInvoke(container, input);
            return result.GetAwaiter().GetResult();
        }
        
        return _migrationDelegate.DynamicInvoke(input);
    }
}
