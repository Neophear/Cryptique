namespace Cryptique.DataTransferObjects.Requests;

public class CreateMessageDataRequest
{
    public byte[] Data { get; set; }
    public int MaxAttempts { get; set; }
    public int MaxDecrypts { get; set; }
    public DateTimeOffset? Expiration { get; set; }
}
