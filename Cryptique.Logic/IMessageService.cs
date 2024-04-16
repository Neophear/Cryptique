using Cryptique.DataTransferObjects.Responses;

namespace Cryptique.Logic;

public interface IMessageService
{
    Task<CreatedResponse> AddMessageAsync(string message);
    Task<MessageResponse?> GetMessageAsync(string id);
    Task<DecryptedMessageResponse?> DecryptMessageAsync(string id, byte[] key);
    Task<DecryptedMessageResponse?> DecryptMessageAsync(string id, string key);
}
