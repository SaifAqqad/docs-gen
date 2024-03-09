namespace docs_gen.Models;

public record Config
{
    public string JsonFile { get; init; } = null!;

    public string OutputDir { get; init; } = null!;

    public bool OutputProcessedJson { get; init; }

    public bool IncludeHeaderIds { get; init; }

    public string? BaseUri { get; init; }
}