namespace Cryptique.DataTransferObjects;

public class MessageDto
{
    public string Id { get; set; }
    public string CipherText { get; set; }
    public string Hash { get; set; }
    public string Salt { get; set; }
}
