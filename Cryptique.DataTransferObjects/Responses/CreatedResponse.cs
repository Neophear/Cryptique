namespace Cryptique.DataTransferObjects.Responses;

public class CreatedResponse
{
    public string Id { get; set; }
    public string Key { get; set; }
    public DateTimeOffset? Expiration { get; set; }
}
