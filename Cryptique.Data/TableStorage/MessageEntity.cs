using Azure;
using Azure.Data.Tables;

namespace Cryptique.Data.TableStorage;

public class MessageEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Message";
    public string RowKey { get; set; } // Message ID
    public string CipherText { get; set; } // Encrypted message
    public string Hash { get; set; } // Hash of the message
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
