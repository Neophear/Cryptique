using System.Security.Cryptography;
using System.Text;
using Cryptique.Data;
using Cryptique.DataTransferObjects;
using Cryptique.DataTransferObjects.Exceptions;
using Cryptique.DataTransferObjects.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cryptique.Logic;

public class MessageService : IMessageService
{
    private readonly ILogger<MessageService> _logger;
    private readonly IMessageRepository _repository;
    private readonly int _maxSize = 0;

    public MessageService(ILogger<MessageService> logger, IConfiguration config, IMessageRepository repository)
    {
        _logger = logger;
        _repository = repository;
        
        // Get MaxSize from appsettings.json
        _ = int.TryParse(config.GetSection("MessageConfig")["MaxSize"], out _maxSize);
    }

    private const int KeySize = 256;
    private const int IvSize = KeySize / 2;
    private const int IdLength = 15;

    public async Task<CreatedResponse> AddMessageAsync(string message, int maxAttempts, int maxDecrypts)
    {
        var messageData = Encoding.UTF8.GetBytes(message);
        
        // If there is a limit, check if the message is too long
        if (_maxSize > 0 && messageData.Length > _maxSize)
            throw new DataTooLongException(_maxSize, messageData.Length);
        
        // Generate random Salt
        var salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        
        // Hash message+salt, so that we can verify decryption when needed
        var messageHash = SHA256.HashData(messageData.Concat(salt).ToArray());
        
        // Generate a random key for AES encryption
        byte[] key;
        using (var aes = Aes.Create())
        {
            aes.KeySize = KeySize;
            aes.GenerateKey();
            key = aes.Key;
        }

        // Encrypt the message using AES encryption
        byte[] encryptedMessage;
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.GenerateIV();
            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            {
                using (var ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    await using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(messageData, 0, message.Length);
                    }
                    encryptedMessage = ms.ToArray();
                }
            }
        }

        // Generate a random ID
        var id = GenerateRandomId();

        var messageDto = new MessageDto
        {
            Id = id,
            CipherText = Convert.ToBase64String(encryptedMessage),
            Hash = Convert.ToBase64String(messageHash),
            Salt = Convert.ToBase64String(salt),
            Options = new MessageOptionsDto
            {
                Attempts = 0,
                Decrypts = 0,
                MaxAttempts = maxAttempts,
                MaxDecrypts = maxDecrypts
            }
        };
        
        await _repository.AddMessageAsync(messageDto);
        
        return new CreatedResponse
        {
            Id = id,
            Key = Convert.ToBase64String(key)
        };
    }

    public async Task<DecryptedMessageResponse?> DecryptMessageAsync(string id, string key) =>
        await DecryptMessageAsync(id, Convert.FromBase64String(key));

    public async Task<DecryptedMessageResponse?> DecryptMessageAsync(string id, byte[] key)
    {
        var message = await _repository.GetMessageAsync(id);

        if (message == null)
            return null;
        
        try
        {
            var encryptedMessage = Convert.FromBase64String(message.CipherText);

            using var aes = Aes.Create();
        
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            var iv = new byte[IvSize / 8];
            Array.Copy(encryptedMessage, 0, iv, 0, iv.Length);
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            await using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
            {
                cs.Write(encryptedMessage, iv.Length, encryptedMessage.Length - iv.Length);
            }
            var decryptedMessage = ms.ToArray();
            
            // Verify the hash of the decrypted message
            var messageSalt = Convert.FromBase64String(message.Salt);
            var decryptedMessageHash =
                SHA256.HashData(decryptedMessage.Concat(messageSalt).ToArray());

            var messageHash = Convert.FromBase64String(message.Hash);

            if (!messageHash.SequenceEqual(decryptedMessageHash))
            {
                await IncrementAttemptsAsync(message);
                return null;
            }

            // We have now successfully decrypted the message
            message.Options.Attempts = 0;
            
            var result = new DecryptedMessageResponse
            {
                Message = Encoding.UTF8.GetString(decryptedMessage)
            };
            
            await IncrementDecryptsAsync(message);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to decrypt message, Id: {Id}", id);
            await IncrementAttemptsAsync(message);
            
            return null;
        }
    }

    private async Task IncrementDecryptsAsync(MessageDto message)
    {
        var decrypts = ++message.Options.Decrypts;

        // If maxDecrypts has been exceeded, delete the message
        if (message.Options.MaxDecrypts > 0 && decrypts >= message.Options.MaxDecrypts)
        {
            _logger.LogInformation("MaxDecrypts {MaxDecrypts} exceeded, deleting message, Id: {Id}",
                message.Options.MaxDecrypts, message.Id);
                
            await _repository.DeleteMessageAsync(message.Id);
        }
        else
        {
            await _repository.UpdateMessageOptionsAsync(message.Id, message.Options);
        }
    }

    private async Task IncrementAttemptsAsync(MessageDto message)
    {
        var attempts = ++message.Options.Attempts;
        
        // If message attempts has been exceeded, delete the message
        if (message.Options.MaxAttempts > 0 && attempts > message.Options.MaxAttempts)
        {
            _logger.LogWarning("MaxAttempts {MaxAttempts} exceeded, deleting message, Id: {Id}",
                message.Options.MaxAttempts, message.Id);

            await _repository.DeleteMessageAsync(message.Id);
        }
        else
        {
            await _repository.UpdateMessageOptionsAsync(message.Id, message.Options);
        }
    }

    private static string GenerateRandomId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(
            Enumerable
            .Repeat(chars, IdLength)
            .Select(s => s[random.Next(s.Length)])
            .ToArray());
    }
}
