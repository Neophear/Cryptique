using Azure;
using Azure.Data.Tables;

namespace Cryptique.Data.TableStorage;

public class MessageEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Message";
    public string RowKey { get; set; } // Message ID
    public string CipherText { get; set; } // Encrypted message
    public string VerificationCipher { get; set; }
    public string VerificationBytes { get; set; }
    public int Attempts { get; set; }
    public int Decrypts { get; set; }
    public int MaxAttempts { get; set; }
    public int MaxDecrypts { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
