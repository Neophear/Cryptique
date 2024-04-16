using Azure;
using Azure.Data.Tables;
using Cryptique.DataTransferObjects;
using Microsoft.Extensions.Configuration;

namespace Cryptique.Data.TableStorage;

public class MessageRepository : IMessageRepository
{
    private readonly TableClient _tableClient;

    public MessageRepository(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnectionString"];
        var tableName = configuration["TableName"];
        _tableClient = new TableClient(connectionString, tableName);
        _tableClient.CreateIfNotExists();
    }

    public Task AddMessageAsync(MessageDto message)
    {
        var entity = new MessageEntity(message.Id, message.CipherText);
        return _tableClient.AddEntityAsync(entity);
    }

    public async Task<MessageDto?> GetMessageAsync(string id)
    {
        try
        {
            var entity = await _tableClient.GetEntityAsync<MessageEntity>("Message", id);
            
            if (entity == null)
            {
                return null;
            }
            
            var msgEntity = entity.Value;

            var dto = new MessageDto
            {
                Id = id,
                CipherText = msgEntity.CipherText
            };
            
            return dto;
        }
        catch (RequestFailedException)
        {
            return null; // or handle differently based on your error handling policy
        }
    }
}
