using Azure;
using Azure.Data.Tables;
using Cryptique.DataTransferObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cryptique.Data.TableStorage;

public class MessageRepository : IMessageRepository
{
    private readonly ILogger<MessageRepository> _logger;
    private readonly TableClient _tableClient;

    public MessageRepository(ILogger<MessageRepository> logger, IConfiguration configuration)
    {
        _logger = logger;

        var connectionString = configuration["StorageConnectionString"];
        var tableName = configuration["TableName"];
        _tableClient = new TableClient(connectionString, tableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task AddMessageAsync(MessageDto message)
    {
        var entity = new MessageEntity
        {
            RowKey = message.Id,
            CipherText = message.CipherText,
            VerificationBytes = message.VerificationBytes,
            VerificationCipher = message.VerificationCipher,
            Attempts = message.Options.Attempts,
            Decrypts = message.Options.Decrypts,
            MaxAttempts = message.Options.MaxDecrypts,
            MaxDecrypts = message.Options.MaxDecrypts,
            Expiration = message.Options.Expiration
        };

        try
        {
            await _tableClient.AddEntityAsync(entity);

            _logger.Log(LogLevel.Information, "Message added to table storage, Id: {Id}", message.Id);
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, e, "Failed to add message to table storage, Id: {Id}", message.Id);

            throw;
        }
    }

    public async Task<MessageDto?> GetMessageAsync(string id)
    {
        try
        {
            var entity = await _tableClient.GetEntityAsync<MessageEntity>("Message", id);
            
            if (entity is null)
            {
                _logger.Log(LogLevel.Information, "Message not found in table storage, Id: {Id}", id);
                return null;
            }
            
            var msgEntity = entity.Value;

            var dto = new MessageDto
            {
                Id = id,
                CipherText = msgEntity.CipherText,
                VerificationBytes = msgEntity.VerificationBytes,
                VerificationCipher = msgEntity.VerificationCipher,
                Options = new MessageOptionsDto
                {
                    Attempts = msgEntity.Attempts,
                    Decrypts = msgEntity.Decrypts,
                    MaxAttempts = msgEntity.MaxAttempts,
                    MaxDecrypts = msgEntity.MaxDecrypts,
                    Expiration = msgEntity.Expiration
                }
            };
            
            return dto;
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, e, "Failed to get message from table storage, Id: {Id}", id);
            return null;
        }
    }
    
    public async Task UpdateMessageOptionsAsync(string id, MessageOptionsDto options)
    {
        var entity = new MessageEntity
        {
            RowKey = id,
            Attempts = options.Attempts,
            Decrypts = options.Decrypts,
            MaxAttempts = options.MaxAttempts,
            MaxDecrypts = options.MaxDecrypts,
            Expiration = options.Expiration
        };
        try
        {

            await _tableClient.UpdateEntityAsync(entity, ETag.All);
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, e, "Failed to update message options in table storage, Id: {Id}", id);

            throw;
        }
    }

    public async Task DeleteMessageAsync(string id) => await _tableClient.DeleteEntityAsync("Message", id);
    
    public async Task<int> CleanupExpiredMessagesAsync()
    {
        var expiredMessages =
            _tableClient.QueryAsync<MessageEntity>("PartitionKey eq 'Message' and Expiration lt datetime'" +
                                                   DateTimeOffset.UtcNow + "'");

        var tasks = new List<Task>();

        await foreach (var message in expiredMessages)
        {
            _logger.Log(LogLevel.Information, "Deleting expired message, Id: {Id}", message.RowKey);
            tasks.Add(DeleteMessageAsync(message.RowKey));
        }

        return await Task.WhenAll(tasks).ContinueWith(_ => tasks.Count);
    }
}
