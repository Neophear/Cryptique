using System.Security.Cryptography;
using System.Text;
using Cryptique.Data;
using Cryptique.DataTransferObjects;
using Cryptique.DataTransferObjects.Responses;

namespace Cryptique.Logic;

public class MessageService(IMessageRepository repository) : IMessageService
{
    private const int KeySize = 256;
    private const int IvSize = KeySize / 2;
    private const int IdLength = 15;

    public async Task<CreatedResponse> AddMessageAsync(string message)
    {
        var messageData = Encoding.UTF8.GetBytes(message);
        
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
            Salt = Convert.ToBase64String(salt)
        };
        
        await repository.AddMessageAsync(messageDto);
        
        return new CreatedResponse
        {
            Id = id,
            Key = Convert.ToBase64String(key)
        };
    }

    public async Task<MessageResponse?> GetMessageAsync(string id)
    {
        var message = await repository.GetMessageAsync(id);
        
        if (message == null)
            return null;

        return new MessageResponse
        {
            Id = message.Id,
            Cipher = message.CipherText
        };
    }

    public async Task<DecryptedMessageResponse?> DecryptMessageAsync(string id, string key) =>
        await DecryptMessageAsync(id, Convert.FromBase64String(key));

    public async Task<DecryptedMessageResponse?> DecryptMessageAsync(string id, byte[] key)
    {
        var message = await repository.GetMessageAsync(id);

        if (message == null)
            return null;

        try
        {
            var encryptedMessage = Convert.FromBase64String(message.CipherText);

            using var aes = Aes.Create();
        
            aes.Key = key;
            aes.Mode = CipherMode.CBC; // You can choose another mode if needed
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
                return null;

            return new DecryptedMessageResponse
            {
                Message = Encoding.UTF8.GetString(decryptedMessage)
            };
        }
        catch (Exception e)
        {
            return null;
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
