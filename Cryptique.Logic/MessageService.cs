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
    
    public Task<CreatedResponse> AddMessageAsync(Stream stream, int maxAttempts, int maxDecrypts)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();

        return AddMessageAsync(data, maxAttempts, maxDecrypts);
    }

    public async Task<CreatedResponse> AddMessageAsync(string message, int maxAttempts, int maxDecrypts) =>
        await AddMessageAsync(Encoding.UTF8.GetBytes(message), maxAttempts, maxDecrypts);

    public async Task<CreatedResponse> AddMessageAsync(byte[] data, int maxAttempts, int maxDecrypts)
    {
        // If there is a limit, check if the message is too long
        if (_maxSize > 0 && data.Length > _maxSize)
            throw new DataTooLongException(_maxSize, data.Length);
        
        // Generate a random key for AES encryption
        var key = GenerateRandomKey();

        // Generate random Salt for key verification
        var verificationBytes = GenerateRandomBytes();
        
        // Encrypt the salt with key, to create a verification hash
        var encryptedVerification = await EncryptData(key, verificationBytes);

        var encryptedMessage = await EncryptData(key, data);

        // Generate a random ID
        var id = GenerateRandomId();

        var messageDto = new MessageDto
        {
            Id = id,
            CipherText = Convert.ToBase64String(encryptedMessage),
            VerificationCipher = Convert.ToBase64String(encryptedVerification),
            VerificationBytes = Convert.ToBase64String(verificationBytes),
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

    private static byte[] GenerateRandomKey()
    {
        using var aes = Aes.Create();

        aes.KeySize = KeySize;
        aes.GenerateKey();
        var key = aes.Key;

        return key;
    }

    private static byte[] GenerateRandomBytes(int bytes = 16)
    {
        var salt = new byte[bytes];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);

        return salt;
    }

    private static async Task<byte[]> EncryptData(byte[] key, byte[] data)
    {
        // Encrypt the message using AES encryption
        using var aes = Aes.Create();
        
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        
        ms.Write(aes.IV, 0, aes.IV.Length);

        await using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }

        var encryptedMessage = ms.ToArray();

        return encryptedMessage;
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
            // Check verificationBytes
            var verificationBytes = Convert.FromBase64String(message.VerificationBytes);
            var verificationCipher = Convert.FromBase64String(message.VerificationCipher);
            
            var decryptedVerification = await DecryptData(key, verificationCipher);
            
            if (!verificationBytes.SequenceEqual(decryptedVerification))
            {
                await IncrementAttemptsAsync(message);
                return null;
            }
            
            // Verification seems good, so decrypt the message
            var encryptedMessage = Convert.FromBase64String(message.CipherText);

            var decryptedMessage = await DecryptData(key, encryptedMessage);
            
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

    private static async Task<byte[]> DecryptData(byte[] key, byte[] cipher)
    {
        using var aes = Aes.Create();
        
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        var iv = new byte[IvSize / 8];
        Array.Copy(cipher, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();

        await using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
        {
            cs.Write(cipher, iv.Length, cipher.Length - iv.Length);
        }
        var decryptedMessage = ms.ToArray();

        return decryptedMessage;
    }

    private async Task IncrementDecryptsAsync(MessageDto message)
    {
        var decrypts = ++message.Options.Decrypts;

        // If maxDecrypts has been reached, delete the message
        if (message.Options.MaxDecrypts > 0 && decrypts >= message.Options.MaxDecrypts)
        {
            _logger.LogInformation("MaxDecrypts {MaxDecrypts} reached, deleting message, Id: {Id}",
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
        
        // If message attempts has been reached, delete the message
        if (message.Options.MaxAttempts > 0 && attempts >= message.Options.MaxAttempts)
        {
            _logger.LogWarning("MaxAttempts {MaxAttempts} reached, deleting message, Id: {Id}",
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
