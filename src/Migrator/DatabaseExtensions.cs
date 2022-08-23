using System.Net;
using Microsoft.Azure.Cosmos;

namespace CosmosDb.Migrator;

internal static class DatabaseExtensions
{
    public static async Task<bool> ContainerExists(this Database db, string containerName)
    {
        Container container = db.GetContainer(containerName);
        
        try
        {
            await container.ReadContainerAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        return true;
    }
}
