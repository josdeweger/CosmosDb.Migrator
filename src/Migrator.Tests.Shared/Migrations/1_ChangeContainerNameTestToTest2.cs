namespace CosmosDb.Migrator.Tests.Shared.Migrations;

[Migration(version: 1)]
public class ChangeContainerNameTestToTest2 : CosmosDbMigration
{
    public override void Up()
    {
        RenameCollection(cfg => cfg
            .WithCollectionName("test")
            .WithPartitionKey("id", "/id")
            .RenameTo("test2"));
    }

    public override void Down()
    {
        RenameCollection(cfg => cfg
            .WithCollectionName("test2")
            .WithPartitionKey("id", "/id")
            .RenameTo("test"));
    }
}
