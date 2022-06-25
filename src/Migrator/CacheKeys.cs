namespace CosmosDb.Migrator;

public static class CacheKeys
{
    private const string VersionDocumentCacheKeyBase = "versiondocument";

    public static string GetVersionDocumentCacheKey(string collection) =>
        $"{VersionDocumentCacheKeyBase.ToLower()}-{collection.ToLower()}";
}