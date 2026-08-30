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

public enum ImageGeometryMode
{
    ResizeToFixedSize,
    CropRelative,
    PreserveOriginal
}

public sealed record NormalizedCrop(double X, double Y, double Width, double Height)
{
    [JsonIgnore]
    public bool IsValid => X >= 0 && Y >= 0 && Width > 0 && Height > 0 && X + Width <= 1.001 && Y + Height <= 1.001;

    public override string ToString() => $"Crop {Width:P0} × {Height:P0} from ({X:P0}, {Y:P0})";
}

public sealed record ImageResizeStep(
    int Width,
    int Height,
    NormalizedCrop? Crop = null,
    string? OverlayAssetPath = null,
    ImageGeometryMode GeometryMode = ImageGeometryMode.ResizeToFixedSize)
{
    [JsonIgnore]
    public bool HasCrop => Crop is { IsValid: true };

    [JsonIgnore]
    public bool HasOverlay => !string.IsNullOrWhiteSpace(OverlayAssetPath);

    public override string ToString()
    {
        return GeometryMode switch
        {
            ImageGeometryMode.CropRelative when HasOverlay => "Relative crop + visual overlay",
            ImageGeometryMode.CropRelative => "Relative crop",
            ImageGeometryMode.PreserveOriginal when HasOverlay => "Visual overlay · preserve original size",
            ImageGeometryMode.PreserveOriginal => "Preserve original size",
            _ when HasOverlay => $"Resize + visual overlay → {Width} × {Height}",
            _ => $"Resize image to {Width} × {Height}"
        };
    }
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

    public string ResolveOutputPath(string inputPath, int sequenceNumber = 1)
    {
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var number = FilenameTemplateEngine.ExtractTrailingNumber(stem);
        var outputStem = FilenameTemplateEngine.Apply(FilenameTemplate, stem, number, sequenceNumber);
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
