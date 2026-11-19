using System.Text.Json;
using AeroGuard.Api.Data;
using AeroGuard.Api.Import;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Tests;

public sealed class ArtifactImporterTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"aeroguard-import-tests-{Guid.NewGuid():N}");

    public ArtifactImporterTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ImportIsIdempotentByArtifactHash()
    {
        var repository = await CreateRepositoryAsync("idempotent.db");
        var importer = new ArtifactImporter(RepositoryPaths.DataDirectory, repository);

        var first = await importer.ImportManifestAsync(CancellationToken);
        var second = await importer.ImportManifestAsync(CancellationToken);

        Assert.Equal(7, first.Imported);
        Assert.Equal(0, first.Skipped);
        Assert.Equal(0, second.Imported);
        Assert.Equal(7, second.Skipped);
        Assert.Equal(7, await repository.CountRunsAsync(CancellationToken));
    }

    [Fact]
    public async Task ImportRejectsManifestPathTraversal()
    {
        var dataDirectory = Path.Combine(_temporaryDirectory, "unsafe-data");
        Directory.CreateDirectory(dataDirectory);
        var manifest = new
        {
            schema_version = 1,
            generated_by = "test",
            scenarios = new[]
            {
                new
                {
                    id = "unsafe",
                    label = "Unsafe",
                    description = "Path validation fixture",
                    file = "../outside.jsonl",
                    duration_ms = 1000,
                    tick_ms = 100,
                    final_state = "NORMAL",
                },
            },
        };
        await File.WriteAllTextAsync(
            Path.Combine(dataDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest),
            CancellationToken);
        var repository = await CreateRepositoryAsync("unsafe.db");
        var importer = new ArtifactImporter(dataDirectory, repository);

        var exception = await Assert.ThrowsAsync<ArtifactImportException>(
            () => importer.ImportManifestAsync(CancellationToken));

        Assert.Contains("unsafe", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await repository.CountRunsAsync(CancellationToken));
    }

    [Fact]
    public async Task ImportRejectsArtifactWithoutSummaryAndRollsBack()
    {
        var dataDirectory = Path.Combine(_temporaryDirectory, "malformed-data");
        Directory.CreateDirectory(dataDirectory);
        var sourceManifest = await File.ReadAllTextAsync(
            Path.Combine(RepositoryPaths.DataDirectory, "manifest.json"),
            CancellationToken);
        using var sourceDocument = JsonDocument.Parse(sourceManifest);
        var firstScenario = sourceDocument.RootElement.GetProperty("scenarios")[0];
        var file = firstScenario.GetProperty("file").GetString()!;
        var manifest = new
        {
            schema_version = 1,
            generated_by = "test",
            scenarios = new[] { JsonSerializer.Deserialize<JsonElement>(firstScenario.GetRawText()) },
        };
        await File.WriteAllTextAsync(
            Path.Combine(dataDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest),
            CancellationToken);
        var sourceLines = await File.ReadAllLinesAsync(
            Path.Combine(RepositoryPaths.DataDirectory, file),
            CancellationToken);
        await File.WriteAllLinesAsync(
            Path.Combine(dataDirectory, file),
            sourceLines[..^1],
            CancellationToken);
        var repository = await CreateRepositoryAsync("malformed.db");
        var importer = new ArtifactImporter(dataDirectory, repository);

        await Assert.ThrowsAsync<ArtifactImportException>(
            () => importer.ImportManifestAsync(CancellationToken));
        Assert.Equal(0, await repository.CountRunsAsync(CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private async Task<ArchiveRepository> CreateRepositoryAsync(string databaseFile)
    {
        var schemaPath = Path.Combine(
            RepositoryPaths.Root,
            "backend",
            "schema.sql");
        var repository = new ArchiveRepository(
            Path.Combine(_temporaryDirectory, databaseFile),
            schemaPath);
        await repository.InitializeAsync(CancellationToken);
        return repository;
    }
}
