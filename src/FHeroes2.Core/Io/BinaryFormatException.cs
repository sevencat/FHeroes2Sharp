namespace FHeroes2.Core.Io;

/// <summary>Thrown when binary data does not match the expected on-disk layout.</summary>
public sealed class BinaryFormatException : Exception
{
    public BinaryFormatException(string message)
        : base(message)
    {
    }

    public BinaryFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
