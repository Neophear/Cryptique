using System.Text;
using Cryptique.Data;
using Cryptique.DataTransferObjects;
using Cryptique.Logic.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace Cryptique.Logic.Tests;

public class MessageServiceTests(ITestOutputHelper outputHelper)
{
    private readonly ILogger<MessageService> _logger = new LogHelper<MessageService>(outputHelper);

    [Fact]
    public async Task TestEncryption()
    {
        // Arrange
        const string message = "Hello, World!";
        var service = new MessageService(_logger, GetConfiguration(), new Mock<IMessageRepository>().Object);

        // Act
        var response = await service.AddMessageAsync(message, 0, 0);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Id);
        Assert.NotNull(response.Key);
    }

    [Fact]
    public async Task TestDecryption()
    {
        // Arrange
        const string message = "Hello, World!";
        var messageRepositoryMock = GetMessageRepositoryMock();

        var service = new MessageService(_logger, GetConfiguration(), messageRepositoryMock.Object);

        var response = await service.AddMessageAsync(message, 0, 0);

        // Act
        var decryptedResponse = await service.DecryptMessageAsync(response.Id, response.Key);

        // Assert
        Assert.NotNull(decryptedResponse);
        var actualMessage = Convert.FromBase64String(decryptedResponse.Message);
        Assert.Equal(message, Encoding.UTF8.GetString(actualMessage));
    }

    [Fact]
    public async Task TestAllowMaxDecryptionAttempt()
    {
        // Arrange
        const string message = "Hello, World!";
        var messageRepositoryMock = GetMessageRepositoryMock();

        var service = new MessageService(_logger, GetConfiguration(), messageRepositoryMock.Object);

        var response = await service.AddMessageAsync(message, 0, 2);

        // Act
        var decryptedResponse = await service.DecryptMessageAsync(response.Id, response.Key);
        var decryptedResponse2 = await service.DecryptMessageAsync(response.Id, response.Key);
        var decryptedResponse3 = await service.DecryptMessageAsync(response.Id, response.Key);

        // Assert
        
        // First two attempts should succeed
        Assert.NotNull(decryptedResponse);
        var actualMessage = Convert.FromBase64String(decryptedResponse.Message);
        Assert.Equal(message, Encoding.UTF8.GetString(actualMessage));
        Assert.NotNull(decryptedResponse2);
        var actualMessage2 = Convert.FromBase64String(decryptedResponse2.Message);
        Assert.Equal(message, Encoding.UTF8.GetString(actualMessage2));
        
        // Third attempt should return null, as the message has been deleted
        Assert.Null(decryptedResponse3);
    }

    [Fact]
    public async Task TestAllowMaxAttempts()
    {
        // Arrange
        const string message = "Hello, World!";
        var messageRepositoryMock = GetMessageRepositoryMock();
        
        var service = new MessageService(_logger, GetConfiguration(), messageRepositoryMock.Object);
        
        var response = await service.AddMessageAsync(message, 2, 0);
        
        // Act
        var decryptedResponseSuccess1 = await service.DecryptMessageAsync(response.Id, response.Key);
        var decryptedResponseFail1 = await service.DecryptMessageAsync(response.Id, "wrong0key1==");
        
        // This one should still succeed as we have only attempted once
        var decryptedResponseSuccess2 = await service.DecryptMessageAsync(response.Id, response.Key);

        // This one should fail, but the message should still exist
        var decryptedResponseFail2 = await service.DecryptMessageAsync(response.Id, "wrong0key1==");
        
        // This one should fail, and the message should be deleted
        var decryptedResponseFail3 = await service.DecryptMessageAsync(response.Id, "wrong0key1==");
        
        // Attempt to decrypt a deleted message
        var decryptedResponseFail4 = await service.DecryptMessageAsync(response.Id, response.Key);
        
        // Assert
        
        // First attempt should succeed
        Assert.NotNull(decryptedResponseSuccess1);
        var actualMessage1 = Convert.FromBase64String(decryptedResponseSuccess1.Message);
        Assert.Equal(message, Encoding.UTF8.GetString(actualMessage1));
        
        // Second attempt should fail
        Assert.Null(decryptedResponseFail1);
        
        // Third attempt should succeed
        Assert.NotNull(decryptedResponseSuccess2);
        var actualMessage2 = Convert.FromBase64String(decryptedResponseSuccess2.Message);
        Assert.Equal(message, Encoding.UTF8.GetString(actualMessage2));
        
        // Fourth attempt should fail
        Assert.Null(decryptedResponseFail2);
        
        // Fifth attempt should fail, and the message should be deleted
        Assert.Null(decryptedResponseFail3);
    }

    private static Mock<IMessageRepository> GetMessageRepositoryMock()
    {
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
        
        messageRepositoryMock
            .Setup(mr => mr.UpdateMessageOptionsAsync(It.IsAny<string>(), It.IsAny<MessageOptionsDto>()))
            .Returns<string, MessageOptionsDto>((id, options) =>
            {
                var message = repoMessages.FirstOrDefault(m => m.Id == id);

                if (message != null)
                {
                    message.Options = options;
                }

                return Task.CompletedTask;
            });
        
        messageRepositoryMock
            .Setup(mr => mr.DeleteMessageAsync(It.IsAny<string>()))
            .Returns<string>(id =>
            {
                var message = repoMessages.FirstOrDefault(m => m.Id == id);

                if (message != null)
                {
                    repoMessages.Remove(message);
                }

                return Task.CompletedTask;
            });
        
        return messageRepositoryMock;
    }

    private static IConfiguration GetConfiguration()
    {
        var configuration = new Mock<IConfiguration>();

        configuration
            .Setup(c => c["StorageConnectionString"])
            .Returns("UseDevelopmentStorage=true");

        configuration
            .Setup(c => c["TableName"])
            .Returns("Message");

        configuration
            .Setup(c => c.GetSection("MessageConfig"))
            .Returns(new Mock<IConfigurationSection>().Object);

        return configuration.Object;
    }
}
