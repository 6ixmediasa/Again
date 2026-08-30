using System.Text.Json.Serialization;

namespace Again.Core;

public enum ImageOutputFormat
{
    Jpeg,
    Png,
    Bmp,
    Tiff,
    Gif
}

public sealed record ImageResizeStep(int Width, int Height)
{
    public override string ToString() => $"Resize image to {Width} × {Height}";
}

public sealed record OutputRule(
    string DestinationDirectory,
    string FilenameTemplate,
    ImageOutputFormat Format,
    int JpegQuality = 92)
{
    [JsonIgnore]
    public string Extension => Format switch
    {
        ImageOutputFormat.Jpeg => ".jpg",
        ImageOutputFormat.Png => ".png",
        ImageOutputFormat.Bmp => ".bmp",
        ImageOutputFormat.Tiff => ".tiff",
        ImageOutputFormat.Gif => ".gif",
        _ => throw new ArgumentOutOfRangeException()
    };

    public string ResolveOutputPath(string inputPath)
    {
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var number = FilenameTemplateEngine.ExtractTrailingNumber(stem);
        var outputStem = FilenameTemplateEngine.Apply(FilenameTemplate, stem, number);
        var candidate = Path.Combine(DestinationDirectory, outputStem + Extension);
        return Path.GetFullPath(candidate);
    }
}

public sealed record WorkflowDefinition(
    Guid Id,
    string Name,
    string DemonstrationInput,
    string DemonstrationOutput,
    ImageResizeStep Resize,
    OutputRule Output,
    DateTimeOffset CreatedAt,
    string Adapter = "Paint demonstration → Windows Imaging")
{
    public string Summary =>
        $"{Resize}; export {Output.Format.ToString().ToUpperInvariant()} to {Output.DestinationDirectory}";
}

public sealed record ImageFileInfo(string Path, int Width, int Height, long Length, DateTime LastWriteUtc);

public sealed record WorkflowDetectionResult(bool Success, WorkflowDefinition? Workflow, string Message)
{
    public static WorkflowDetectionResult Fail(string message) => new(false, null, message);
    public static WorkflowDetectionResult Ok(WorkflowDefinition workflow, string message) => new(true, workflow, message);
}

public sealed record BatchItemResult(string InputPath, string? OutputPath, bool Success, bool Skipped, string Message);

public sealed record BatchRunSummary(
    Guid WorkflowId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<BatchItemResult> Items)
{
    public int Completed => Items.Count(x => x.Success);
    public int Skipped => Items.Count(x => x.Skipped);
    public int Errors => Items.Count(x => !x.Success && !x.Skipped);
}
