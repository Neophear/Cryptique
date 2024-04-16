using Cryptique.Data;
using Cryptique.DataTransferObjects;
using Moq;

namespace Cryptique.Logic.Tests;

public class MessageServiceTests
{
    [Fact]
    public async Task TestEncryption()
    {
        // Arrange
        var message = "Hello, World!";
        var service = new MessageService(new Mock<IMessageRepository>().Object);

        // Act
        var response = await service.AddMessageAsync(message);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Id);
        Assert.NotNull(response.Key);
    }
    
    [Fact]
    public async Task TestDecryption()
    {
        // Arrange
        var message = "Hello, World!";
        var messageRepositoryMock = new Mock<IMessageRepository>();

        // We need to catch the encrypted message to decrypt it later
        var repoMessages = new List<MessageDto>();
        
        messageRepositoryMock
            .Setup(mr => mr.AddMessageAsync(It.IsAny<MessageDto>()))
            .Returns<MessageDto>(messageDto =>
            {
                repoMessages.Add(messageDto);

                return Task.CompletedTask;
            });

        messageRepositoryMock
            .Setup(mr => mr.GetMessageAsync(It.IsAny<string>()))
            .Returns<string>(id => Task.FromResult(repoMessages.FirstOrDefault(m => m.Id == id)));
        
        var service = new MessageService(messageRepositoryMock.Object);
        
        var response = await service.AddMessageAsync(message);

        // Act
        var decryptedResponse = await service.DecryptMessageAsync(response.Id, response.Key);

        // Assert
        Assert.NotNull(decryptedResponse);
        Assert.Equal(message, decryptedResponse.Message);
    }
}
