using Azure;
using Azure.Data.Tables;
using Cryptique.Data.Interfaces;
using Cryptique.DataTransferObjects;

namespace Cryptique.Data;

public class MessageRepository : IMessageRepository
{
    private readonly TableClient _tableClient;

    public MessageRepository(string connectionString, string tableName)
    {
        _tableClient = new TableClient(connectionString, tableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task AddMessageAsync(MessageEntity message)
    {
        await _tableClient.AddEntityAsync(message);
    }

    public async Task<MessageDto?> GetMessageAsync(string id)
    {
        try
        {
            var entity = await _tableClient.GetEntityAsync<MessageEntity>("Message", id);
            
            Guid.TryParse(entity., out var guid);
            
            var id = entity.RowKey;
            
            var dto = new MessageDto
            {
                Id = entity.RowKey,
                CipherText = entity.CipherText
            };
            
            return entity.Value;
        }
        catch (RequestFailedException)
        {
            return null; // or handle differently based on your error handling policy
        }
    }
}
