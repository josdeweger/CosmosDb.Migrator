using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Azure.Cosmos;
using Moq;

namespace CosmosDb.Migrator.Tests.Shared;

public sealed class DatabaseMockBuilder
{
    private readonly Fixture _fixture = new();
    private string? _containerName;
    private readonly Mock<Database> _dbMock = new();
    
    public readonly Mock<Container> ContainerMock = new();

    public DatabaseMockBuilder()
    {
        _fixture.Customize(new AutoMoqCustomization());
    }
    
    public DatabaseMockBuilder WithContainer(string name)
    {
        _containerName = name;

        return this;
    }

    public DatabaseMockBuilder WithVersionDocument(long version)
    {
        var versionDoc = new VersionDocument("version-doc-id", nameof(VersionDocument), version);
        var itemResponseMock = new Mock<ItemResponse<VersionDocument>>();
        itemResponseMock.Setup(i => i.Resource).Returns(versionDoc);

        ContainerMock.Setup(x =>
                x.ReadItemAsync<VersionDocument>(
                    It.IsAny<string>(), 
                    It.IsAny<PartitionKey>(), 
                    null,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(itemResponseMock.Object);

        return this;
    }

    public DatabaseMockBuilder WithQueryResult<T>(Func<Fixture, List<T>> docBuilderFunc) 
        where T : IMigratable
    {
        var docs = docBuilderFunc(_fixture);
        var response = new DocumentsResponse<T>(docs);
        var docsAsByteArr = ObjectToByteArray(response);

        var responseMessage = _fixture
            .Build<ResponseMessage>()
            .With(x => x.Content, new MemoryStream(docsAsByteArr))
            .Create();

        var feedIteratorMock = new Mock<FeedIterator>();

        var i = 0;
        
        feedIteratorMock
            .SetupSequence(f => f.HasMoreResults)
            .Returns(() =>
            {
                if (i == docs.Count)
                {
                    return false;
                }

                i++;
                return true;
            });
        
        feedIteratorMock.Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(responseMessage);

        ContainerMock.Setup(x =>
                x.GetItemQueryStreamIterator(It.IsAny<QueryDefinition>(), It.IsAny<string>(),
                    It.IsAny<QueryRequestOptions>()))
            .Returns(feedIteratorMock.Object);

        return this;
    }

    public Mock<Database> Build()
    {
        if (string.IsNullOrEmpty(_containerName))
        {
            throw new Exception($"{_containerName} can not be empty");
        }
        
        var containerResponseMock = new Mock<ContainerResponse>();
        containerResponseMock.SetupGet(m => m.Container).Returns(ContainerMock.Object);

        _dbMock.Setup(m => m.CreateContainerIfNotExistsAsync(It.IsAny<ContainerProperties>(),
                It.IsAny<int?>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(containerResponseMock.Object);

        _dbMock.Setup(m => m.GetContainer(_containerName)).Returns(ContainerMock.Object);
        
        return _dbMock;
    }
    
    private byte[] ObjectToByteArray(object obj)
    {
        // proper way to serialize object
        var objToString = System.Text.Json.JsonSerializer.Serialize(obj);
        // convert that that to string with ascii you can chose what ever encoding want
        return System.Text.Encoding.ASCII.GetBytes(objToString);
    }
}

public class DocumentsResponse<T>
{
    public List<T> Documents { get; }

    public DocumentsResponse(List<T> documents)
    {
        Documents = documents;
    }
}
