using Cryptique.DataTransferObjects;

namespace Cryptique.Data;

public interface IMessageRepository
{
    Task AddMessageAsync(MessageDto message);
    Task<MessageDto?> GetMessageAsync(string id);
    Task UpdateMessageOptionsAsync(string id, MessageOptionsDto messageOptions);
    Task DeleteMessageAsync(string id);
}
