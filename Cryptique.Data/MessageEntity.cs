using Azure;
using Azure.Data.Tables;

namespace Cryptique.Data;

public class MessageEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Message";
    public string RowKey { get; set; } // Use GUID as RowKey for uniqueness
    public string CipherText { get; set; } // Encrypted message
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public MessageEntity() { }

    public MessageEntity(string id, string cipherText)
    {
        RowKey = id;
        CipherText = cipherText;
    }
}
