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
    
    public async Task<dynamic> Invoke(Container container, IMigratable input)
    {
        if (_migrationDelegate is null)
        {
            throw new Exception($"No migration delegate is set for type from: {FromType} and type to: {ToType}");
        }

        return await InvokeDelegate<dynamic>(_migrationDelegate, new object[]{ container, input });
    }
    
    public async Task<TResult> InvokeDelegate<TResult>(Delegate action, object[] actionArgs = null)
    {
        var result = action.DynamicInvoke(actionArgs);
        
        if (result is Task<TResult> task)
        {
            return await task;
        }

        return (TResult)result;
    }
}
