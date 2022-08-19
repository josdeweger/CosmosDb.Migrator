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
    public static string EmptyDocumentType => "None";

    public DataMigrationConfig(
        string collectionName, 
        string partitionKey, 
        string partitionKeyPath, 
        string documentType,
        Type fromType, 
        Type toType, 
        List<Delegate> conditions, 
        Delegate migrationDelegate)
        : base(collectionName, partitionKey, partitionKeyPath)
    {
        DocumentType = documentType;
        FromType = fromType;
        ToType = toType;

        _conditions = conditions;
        _migrationDelegate = migrationDelegate;
    }

    public async Task<bool> AreConditionsMet(Container container, object oldDoc)
    {
        foreach (var condition in _conditions)
        {
            var result = condition.DynamicInvoke(container, oldDoc);
            await (Task) result;

            return (bool?)result.GetType().GetProperty("Result")?.GetValue(result) ?? false;
        }

        return true;
    }
    
    public async Task<dynamic?> Invoke(Container container, IMigratable input)
    {
        if (_migrationDelegate is null)
        {
            throw new Exception($"No migration delegate is set for type from: {FromType} and type to: {ToType}");
        }

        if (_migrationDelegate.Method.IsDefined(typeof(AsyncStateMachineAttribute), false))
        {
            var result = _migrationDelegate.DynamicInvoke(container, input);
            await (Task) result;

            return result.GetType().GetProperty("Result")?.GetValue(result);
        }

        return _migrationDelegate.DynamicInvoke(input);
    }
}
