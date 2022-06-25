namespace CosmosDb.Migrator.IntegrationTests.Documents;

public record TestDataDocument(
    string Id,
    string DocumentType,
    long? Version,
    string SomeString,
    string SomeBoolStr,
    string SomeDateTimeStr) : MigratableRecord(Id, DocumentType, Version);
    
public record TestDataDocumentV2(
    string Id, 
    string DocumentType, 
    long? Version, 
    string SomeString, 
    bool SomeBool, 
    DateTime SomeDateTime) : MigratableRecord(Id, DocumentType, Version);
