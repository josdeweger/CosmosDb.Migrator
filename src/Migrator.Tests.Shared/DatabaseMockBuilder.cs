using System.Text;
using System.Text.Json;
using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Azure.Cosmos;
using Moq;

namespace CosmosDb.Migrator.Tests.Shared;

public sealed class DatabaseMockBuilder
{
    private readonly Mock<Database> _dbMock = new();
    private readonly List<Mock<Container>> _containerMocks = new();

    public DatabaseMockBuilder WithContainer<T>(string containerName,
        Func<ContainerMockBuilder<T>, Mock<Container>> func) where T : IMigratable
    {
        var containerMock = func(new ContainerMockBuilder<T>(containerName));

        var containerResponseMock = new Mock<ContainerResponse>();
        containerResponseMock.SetupGet(m => m.Container).Returns(containerMock.Object);

        _dbMock.Setup(m => m.CreateContainerIfNotExistsAsync(It.IsAny<ContainerProperties>(),
                It.IsAny<int?>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(containerResponseMock.Object);

        _dbMock.Setup(m => m.GetContainer(containerMock.Object.Id)).Returns(containerMock.Object);

        _containerMocks.Add(containerMock);
        
        return this;
    }

    public Mock<Container> GetContainerMock(string containerName)
    {
        return _containerMocks.First(c => c.Object.Id.Equals(containerName));
    }
    
    public Mock<Database> Build()
    {   
        return _dbMock;
    }
}

public class ContainerMockBuilder<T> where T : IMigratable
{
    private readonly Fixture _fixture = new();
    private readonly List<T> _docs = new();
    public readonly Mock<Container> ContainerMock = new();

    public ContainerMockBuilder(string containerName)
    {
        ContainerMock.SetupGet(x => x.Id).Returns(containerName);
        _fixture.Customize(new AutoMoqCustomization());
    }

    public ContainerMockBuilder<T> WithVersionDocument(long version, long? timestamp = null)
    {
        long ts = timestamp ?? new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        
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

    public ContainerMockBuilder<T> AddDocument(Func<Fixture, T> docBuilderFunc)
    {
        var doc = docBuilderFunc(_fixture);

        var itemResponse = new Mock<ItemResponse<T>>();
        itemResponse.SetupGet(x => x.Resource).Returns(doc);
        
        ContainerMock
            .Setup(x => x.ReadItemAsync<T>(doc.Id, new PartitionKey(doc.Id), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(itemResponse.Object);
        
        //also add doc to collection so in the Build() method the FeedIterator can be setup to return the collection
        _docs.Add(doc);

        return this;
    }

    public Mock<Container> Build()
    {
        SetupItemQueryStreamIterator();

        return ContainerMock;
    }

    private void SetupItemQueryStreamIterator()
    {
        var response = new DocumentsResponse<T>(_docs);
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
                if (i == _docs.Count)
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
    }

    private byte[] ObjectToByteArray(DocumentsResponse<T> obj)
    {
        var objToString = JsonSerializer.Serialize(obj);
        
        return Encoding.ASCII.GetBytes(objToString);
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
