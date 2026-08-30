namespace Again.Core;

public static class WorkflowDetector
{
    public static WorkflowDetectionResult Detect(ImageFileInfo source, ImageFileInfo output)
    {
        if (source.Width <= 0 || source.Height <= 0 || output.Width <= 0 || output.Height <= 0)
            return WorkflowDetectionResult.Fail("AGAIN could not read the demonstrated image dimensions.");

        if (source.Width == output.Width && source.Height == output.Height)
            return WorkflowDetectionResult.Fail("No image resize was detected. V0.1 currently needs the demonstration to change the image dimensions.");

        var format = TryGetFormat(output.Path);
        if (format is null)
            return WorkflowDetectionResult.Fail("The demonstrated output format is not supported in V0.1.");

        var destination = Path.GetDirectoryName(output.Path);
        if (string.IsNullOrWhiteSpace(destination))
            return WorkflowDetectionResult.Fail("AGAIN could not determine the output folder.");

        var sourceStem = Path.GetFileNameWithoutExtension(source.Path);
        var outputStem = Path.GetFileNameWithoutExtension(output.Path);
        var template = FilenameTemplateEngine.Infer(sourceStem, outputStem);

        if (string.Equals(Path.GetFullPath(source.Path), Path.GetFullPath(output.Path), StringComparison.OrdinalIgnoreCase))
        {
            destination = Path.Combine(Path.GetDirectoryName(source.Path)!, "AGAIN Results");
            template = "{stem}";
        }

        var workflow = new WorkflowDefinition(
            Guid.NewGuid(),
            "Resize + export image",
            source.Path,
            output.Path,
            new ImageResizeStep(output.Width, output.Height),
            new OutputRule(destination, template, format.Value),
            DateTimeOffset.Now);

        var note = template == "{stem}" && !string.Equals(sourceStem, outputStem, StringComparison.OrdinalIgnoreCase)
            ? "Workflow detected. The demonstrated fixed filename was normalized to each source filename to prevent collisions."
            : "Workflow detected from the demonstrated output.";

        return WorkflowDetectionResult.Ok(workflow, note);
    }

    public static ImageOutputFormat? TryGetFormat(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".jfif" => ImageOutputFormat.Jpeg,
            ".png" => ImageOutputFormat.Png,
            ".bmp" => ImageOutputFormat.Bmp,
            ".tif" or ".tiff" => ImageOutputFormat.Tiff,
            ".gif" => ImageOutputFormat.Gif,
            _ => null
        };
    }
}
