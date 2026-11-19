namespace AeroGuard.Api.Import;

public sealed class ArtifactImportException : Exception
{
    public ArtifactImportException(string message)
        : base(message)
    {
    }

    public ArtifactImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
