namespace CosmosDb.Migrator;

[AttributeUsage(AttributeTargets.Class)]
public class MigrationAttribute : Attribute
{
    public long Version { get; }
    
    public MigrationAttribute(long version)
    {
        Version = version;
    }
}
