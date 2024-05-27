namespace Cryptique.DataTransferObjects.Requests;

public class CreateMessageRequest
{
    public string Message { get; set; }
    public int MaxAttempts { get; set; }
    public int MaxDecrypts { get; set; }
    public DateTimeOffset? Expiration { get; set; }
}
