using Cryptique.DataTransferObjects.Responses;

namespace Cryptique.Logic;

public interface IMessageService
{
    Task<CreatedResponse> AddMessageAsync(string message, int maxAttempts, int maxDecrypts, DateTimeOffset? expiration = null);
    Task<CreatedResponse> AddMessageAsync(byte[] data, int maxAttempts, int maxDecrypts, DateTimeOffset? expiration = null);
    Task<CreatedResponse> AddMessageAsync(Stream stream, int maxAttempts, int maxDecrypts, DateTimeOffset? expiration = null);
    Task<DecryptedMessageResponse?> DecryptMessageAsync(string id, byte[] key);
    Task<DecryptedMessageResponse?> DecryptMessageAsync(string id, string key);
    Task<int> CleanupExpiredMessages();
}
