namespace Again.Core;

public static class SafetyGuard
{
    public static void ValidateReplayTarget(string inputPath, string outputPath)
    {
        var input = Path.GetFullPath(inputPath);
        var output = Path.GetFullPath(outputPath);
        if (string.Equals(input, output, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AGAIN stopped because this workflow would overwrite the source file.");

        var outputDirectory = Path.GetDirectoryName(output)
            ?? throw new InvalidOperationException("Output directory is invalid.");

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);
    }

    public static string MakeCollisionSafe(string outputPath)
    {
        if (!File.Exists(outputPath))
            return outputPath;

        var dir = Path.GetDirectoryName(outputPath)!;
        var stem = Path.GetFileNameWithoutExtension(outputPath);
        var ext = Path.GetExtension(outputPath);
        for (var i = 2; i <= 9999; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException("AGAIN could not create a collision-safe output filename.");
    }
}
