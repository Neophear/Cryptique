namespace Cryptique.DataTransferObjects;

public class MessageDto
{
    public string Id { get; set; }
    public string CipherText { get; set; }
    public string VerificationCipher { get; set; }
    public string VerificationBytes { get; set; }
    public MessageOptionsDto Options { get; set; }
}
