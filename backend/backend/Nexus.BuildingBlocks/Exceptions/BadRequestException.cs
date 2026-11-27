namespace Nexus.BuildingBlocks.Exceptions;

public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message)
    {
    }

    /// <summary>Extra machine-readable data for the client, e.g. the override reasons a batch needs.</summary>
    public BadRequestException(string message, string code, object? data) : base(message)
    {
        Code = code;
        Data_ = data;
    }

    public string? Code { get; }
    public object? Data_ { get; }
}
