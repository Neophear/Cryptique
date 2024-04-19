namespace Cryptique.DataTransferObjects;

public class MessageOptionsDto
{
    /// <summary>
    /// Number of times the message has been attempted to decrypt
    /// </summary>
    public int Attempts { get; set; }
    
    /// <summary>
    /// Number of times the message has been decrypted
    /// </summary>
    public int Decrypts { get; set; }
    
    /// <summary>
    /// Max attempts to decrypt the message, after which the message is deleted
    /// 0 means no limit
    /// </summary>
    public int MaxAttempts { get; set; }
    
    /// <summary>
    /// Max decrypts allowed for the message
    /// 0 means no limit
    /// </summary>
    public int MaxDecrypts { get; set; }
}
