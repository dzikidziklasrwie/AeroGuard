namespace AeroGuard.Api.Errors;

public sealed class ApiProblemException : Exception
{
    public ApiProblemException(
        int statusCode,
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Errors = errors;
    }

    public int StatusCode { get; }

    public string Title { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public static ApiProblemException Validation(
        string field,
        string message) =>
        new(
            StatusCodes.Status400BadRequest,
            "Request validation failed",
            "Correct the listed field and submit the request again.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [field] = [message],
            });

    public static ApiProblemException Conflict(string detail) =>
        new(StatusCodes.Status409Conflict, "Request conflicts with stored data", detail);

    public static ApiProblemException NotFound(string detail) =>
        new(StatusCodes.Status404NotFound, "Resource not found", detail);

    public static ApiProblemException Forbidden(string detail) =>
        new(StatusCodes.Status403Forbidden, "Operation is not allowed", detail);
}
