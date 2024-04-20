namespace Cryptique.DataTransferObjects.Exceptions;

public class DataTooLongException(int allowedSize, int actualSize)
    : Exception($"Data is too long. Allowed size: {allowedSize}, actual size: {actualSize}")
{
    public int AllowedSize { get; set; } = allowedSize;
    public int ActualSize { get; set; } = actualSize;
}
