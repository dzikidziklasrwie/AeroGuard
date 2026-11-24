using AeroGuard.Api.Data;
using Microsoft.Net.Http.Headers;

namespace AeroGuard.Api.Endpoints;

public static class ArchivedReplayEndpoint
{
    public static async Task<IResult> GetAsync(
        long id,
        HttpContext context,
        ArchiveRepository archive,
        CancellationToken cancellationToken)
    {
        var run = await archive.GetRunAsync(id, cancellationToken);
        if (run is null)
        {
            return Results.NotFound();
        }

        var entityTag = new EntityTagHeaderValue($"\"sha256-{run.SourceSha256}\"");
        SetValidationHeaders(context.Response, entityTag);
        if (MatchesIfNoneMatch(context.Request, entityTag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        var bytes = await archive.GetReplayAsync(
            id,
            run.SourceSha256,
            cancellationToken);
        if (bytes is null)
        {
            context.Response.Headers.Remove(HeaderNames.ETag);
            context.Response.Headers.Remove(HeaderNames.CacheControl);
            return Results.NotFound();
        }

        return Results.File(
            bytes,
            contentType: "application/x-ndjson",
            fileDownloadName: $"{run.Scenario}.jsonl",
            lastModified: run.ImportedAtUtc,
            entityTag: entityTag,
            enableRangeProcessing: false);
    }

    private static bool MatchesIfNoneMatch(
        HttpRequest request,
        EntityTagHeaderValue entityTag)
    {
        var candidates = request.GetTypedHeaders().IfNoneMatch;
        return candidates is not null
            && candidates.Any(candidate =>
                candidate.Equals(EntityTagHeaderValue.Any)
                || candidate.Compare(entityTag, useStrongComparison: false));
    }

    private static void SetValidationHeaders(
        HttpResponse response,
        EntityTagHeaderValue entityTag)
    {
        var headers = response.GetTypedHeaders();
        headers.ETag = entityTag;
        headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            Public = true,
        };
    }
}
