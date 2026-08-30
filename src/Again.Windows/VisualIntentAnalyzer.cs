using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Again.Core;

namespace Again.Windows;

public sealed record VisualIntentAnalysis(bool Success, ImageResizeStep? Step, string Message)
{
    public static VisualIntentAnalysis Fail(string message) => new(false, null, message);
    public static VisualIntentAnalysis Ok(ImageResizeStep step, string message) => new(true, step, message);
}

public static class VisualIntentAnalyzer
{
    private const int ClosePixelThreshold = 42;
    private const int OverlayPixelThreshold = 58;
    private const int InformativeGradientThreshold = 18;
    private const int OverlayTileSize = 32;
    private const double MaxOverlayTileCoverage = 0.12;

    private readonly record struct SamplePoint(int X, int Y);

    public static VisualIntentAnalysis AnalyzeAndPersist(string sourcePath, string demonstratedOutputPath, Guid workflowId)
    {
        var source = LoadBgra32(sourcePath);
        var output = LoadBgra32(demonstratedOutputPath);

        var sourceAspect = source.PixelWidth / (double)source.PixelHeight;
        var outputAspect = output.PixelWidth / (double)output.PixelHeight;
        var aspectDelta = Math.Abs(sourceAspect - outputAspect) / Math.Max(sourceAspect, outputAspect);

        NormalizedCrop? crop = null;
        BitmapSource baseline;
        ImageGeometryMode geometryMode;

        if (source.PixelWidth == output.PixelWidth && source.PixelHeight == output.PixelHeight)
        {
            baseline = source;
            geometryMode = ImageGeometryMode.PreserveOriginal;
        }
        else if (output.PixelWidth <= source.PixelWidth && output.PixelHeight <= source.PixelHeight)
        {
            var cropMatch = TryFindExactCrop(source, output);
            if (cropMatch is not null)
            {
                crop = new NormalizedCrop(
                    cropMatch.Value.X / (double)source.PixelWidth,
                    cropMatch.Value.Y / (double)source.PixelHeight,
                    cropMatch.Value.Width / (double)source.PixelWidth,
                    cropMatch.Value.Height / (double)source.PixelHeight);
                baseline = new CroppedBitmap(source, cropMatch.Value);
                baseline.Freeze();
                geometryMode = ImageGeometryMode.CropRelative;
            }
            else if (aspectDelta <= 0.02)
            {
                baseline = RenderToSize(source, output.PixelWidth, output.PixelHeight);
                geometryMode = ImageGeometryMode.ResizeToFixedSize;
            }
            else
            {
                return VisualIntentAnalysis.Fail(
                    "The demonstrated image changed aspect ratio, but AGAIN could not confidently locate the crop. It stopped instead of stretching the remaining images.");
            }
        }
        else if (aspectDelta <= 0.02)
        {
            baseline = RenderToSize(source, output.PixelWidth, output.PixelHeight);
            geometryMode = ImageGeometryMode.ResizeToFixedSize;
        }
        else
        {
            return VisualIntentAnalysis.Fail(
                "The demonstrated image changed aspect ratio in a way AGAIN cannot safely classify as a crop or proportional resize yet. Nothing was replayed.");
        }

        var overlayPath = TryExtractOverlay(baseline, output, workflowId);
        var step = new ImageResizeStep(output.PixelWidth, output.PixelHeight, crop, overlayPath, geometryMode);

        var description = geometryMode switch
        {
            ImageGeometryMode.CropRelative when overlayPath is not null =>
                "Detected a relative crop plus a localized visual edit. Each item keeps its own natural cropped dimensions; the demo output size is not forced onto later images.",
            ImageGeometryMode.CropRelative =>
                "Detected a relative crop. The same crop region will be applied proportionally to each image without stretching it.",
            ImageGeometryMode.PreserveOriginal when overlayPath is not null =>
                "Detected a localized visual edit with no resize or crop. Each image keeps its original dimensions.",
            ImageGeometryMode.PreserveOriginal =>
                "Detected an export/rename workflow that preserves each image's original dimensions.",
            ImageGeometryMode.ResizeToFixedSize when overlayPath is not null =>
                "Detected a proportional fixed-size resize plus a localized visual edit.",
            _ => "Detected a proportional fixed-size resize."
        };

        return VisualIntentAnalysis.Ok(step, description);
    }

    private static Int32Rect? TryFindExactCrop(BitmapSource source, BitmapSource output)
    {
        var outW = output.PixelWidth;
        var outH = output.PixelHeight;
        var maxX = source.PixelWidth - outW;
        var maxY = source.PixelHeight - outH;
        if (maxX < 0 || maxY < 0) return null;

        var sourcePixels = CopyPixels(source);
        var outputPixels = CopyPixels(output);
        var sourceStride = source.PixelWidth * 4;
        var outputStride = outW * 4;
        var samples = BuildInformativeSamples(outputPixels, outW, outH, outputStride);
        var coarseStep = Math.Max(1, Math.Max(maxX, maxY) / 100);

        var best = FindBestOffset(
            sourcePixels, outputPixels, sourceStride, outputStride, samples,
            0, maxX, 0, maxY, coarseStep);

        if (coarseStep > 1)
        {
            var radius = coarseStep * 2;
            best = FindBestOffset(
                sourcePixels, outputPixels, sourceStride, outputStride, samples,
                Math.Max(0, best.X - radius), Math.Min(maxX, best.X + radius),
                Math.Max(0, best.Y - radius), Math.Min(maxY, best.Y + radius), 1);
        }

        if (best.CloseRatio < 0.65 || best.AverageDifference > 60)
            return null;

        return new Int32Rect(best.X, best.Y, outW, outH);
    }

    private static SamplePoint[] BuildInformativeSamples(byte[] outputPixels, int width, int height, int stride)
    {
        const int tileSize = 24;
        var samples = new List<SamplePoint>();

        for (var tileY = 0; tileY < height; tileY += tileSize)
        {
            var endY = Math.Min(height, tileY + tileSize);
            for (var tileX = 0; tileX < width; tileX += tileSize)
            {
                var endX = Math.Min(width, tileX + tileSize);
                var bestGradient = 0;
                var bestX = -1;
                var bestY = -1;

                for (var y = Math.Max(1, tileY); y < endY; y += 2)
                {
                    for (var x = Math.Max(1, tileX); x < endX; x += 2)
                    {
                        var index = y * stride + x * 4;
                        var left = PixelDifference(outputPixels, index, outputPixels, index - 4);
                        var up = PixelDifference(outputPixels, index, outputPixels, index - stride);
                        var gradient = Math.Max(left, up);
                        if (gradient <= bestGradient) continue;
                        bestGradient = gradient;
                        bestX = x;
                        bestY = y;
                    }
                }

                if (bestGradient >= InformativeGradientThreshold && bestX >= 0)
                    samples.Add(new SamplePoint(bestX, bestY));
            }
        }

        if (samples.Count < 80)
        {
            foreach (var y in EvenSamples(height, 14))
            foreach (var x in EvenSamples(width, 18))
                samples.Add(new SamplePoint(x, y));
        }

        return samples.Distinct().ToArray();
    }

    private static (int X, int Y, double CloseRatio, double AverageDifference) FindBestOffset(
        byte[] sourcePixels,
        byte[] outputPixels,
        int sourceStride,
        int outputStride,
        IReadOnlyList<SamplePoint> samples,
        int minX,
        int maxX,
        int minY,
        int maxY,
        int step)
    {
        var bestX = minX;
        var bestY = minY;
        var bestClose = -1.0;
        var bestAverage = double.MaxValue;

        for (var y = minY; y <= maxY; y += step)
        {
            for (var x = minX; x <= maxX; x += step)
            {
                long diffTotal = 0;
                var close = 0;

                foreach (var sample in samples)
                {
                    var sourceIndex = ((y + sample.Y) * sourceStride) + ((x + sample.X) * 4);
                    var outputIndex = (sample.Y * outputStride) + (sample.X * 4);
                    var diff = PixelDifference(sourcePixels, sourceIndex, outputPixels, outputIndex);
                    diffTotal += diff;
                    if (diff <= ClosePixelThreshold) close++;
                }

                var closeRatio = close / (double)samples.Count;
                var average = diffTotal / (double)samples.Count;
                if (closeRatio > bestClose || (Math.Abs(closeRatio - bestClose) < 0.0001 && average < bestAverage))
                {
                    bestX = x;
                    bestY = y;
                    bestClose = closeRatio;
                    bestAverage = average;
                }
            }
        }

        return (bestX, bestY, bestClose, bestAverage);
    }

    private static string? TryExtractOverlay(BitmapSource baseline, BitmapSource demonstratedOutput, Guid workflowId)
    {
        if (baseline.PixelWidth != demonstratedOutput.PixelWidth || baseline.PixelHeight != demonstratedOutput.PixelHeight)
            return null;

        var width = demonstratedOutput.PixelWidth;
        var height = demonstratedOutput.PixelHeight;
        var stride = width * 4;
        var basePixels = CopyPixels(baseline);
        var outputPixels = CopyPixels(demonstratedOutput);
        var changed = new bool[width * height];

        var tilesX = (width + OverlayTileSize - 1) / OverlayTileSize;
        var tilesY = (height + OverlayTileSize - 1) / OverlayTileSize;
        var changedPerTile = new int[tilesX * tilesY];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * stride + x * 4;
                var isChanged = PixelDifference(basePixels, index, outputPixels, index) >= OverlayPixelThreshold;
                changed[y * width + x] = isChanged;
                if (!isChanged) continue;
                var tileIndex = (y / OverlayTileSize) * tilesX + (x / OverlayTileSize);
                changedPerTile[tileIndex]++;
            }
        }

        var activeTiles = 0;
        for (var tileY = 0; tileY < tilesY; tileY++)
        {
            for (var tileX = 0; tileX < tilesX; tileX++)
            {
                var tileWidth = Math.Min(OverlayTileSize, width - tileX * OverlayTileSize);
                var tileHeight = Math.Min(OverlayTileSize, height - tileY * OverlayTileSize);
                var tileArea = tileWidth * tileHeight;
                var minimumChanged = Math.Max(4, (int)Math.Ceiling(tileArea * 0.01));
                if (changedPerTile[tileY * tilesX + tileX] >= minimumChanged)
                    activeTiles++;
            }
        }

        var activeTileRatio = activeTiles / (double)(tilesX * tilesY);
        if (activeTileRatio > MaxOverlayTileCoverage)
            return null;

        var overlayPixels = new byte[outputPixels.Length];
        var kept = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var neighbors = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    var ny = y + dy;
                    if (ny < 0 || ny >= height) continue;
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx;
                        if (nx < 0 || nx >= width) continue;
                        if (changed[ny * width + nx]) neighbors++;
                    }
                }

                if (neighbors < 2) continue;

                var index = y * stride + x * 4;
                overlayPixels[index] = outputPixels[index];
                overlayPixels[index + 1] = outputPixels[index + 1];
                overlayPixels[index + 2] = outputPixels[index + 2];
                overlayPixels[index + 3] = 255;
                kept++;
            }
        }

        var ratio = kept / (double)(width * height);
        if (ratio < 0.00008 || ratio > 0.15)
            return null;

        var overlay = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, overlayPixels, stride);
        overlay.Freeze();

        var assetDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "6ixMedia SA", "AGAIN", "assets");
        Directory.CreateDirectory(assetDirectory);
        var overlayPath = Path.Combine(assetDirectory, $"{workflowId:N}-overlay.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(overlay));
        using var stream = new FileStream(overlayPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
        return overlayPath;
    }

    private static BitmapSource RenderToSize(BitmapSource source, int width, int height)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            dc.DrawImage(source, new Rect(0, 0, width, height));

        var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        rendered.Freeze();

        var converted = new FormatConvertedBitmap(rendered, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static BitmapSource LoadBgra32(string path)
    {
        BitmapFrame frame;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            frame = decoder.Frames[0];
        }

        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static byte[] CopyPixels(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static int PixelDifference(byte[] a, int aIndex, byte[] b, int bIndex)
    {
        var db = Math.Abs(a[aIndex] - b[bIndex]);
        var dg = Math.Abs(a[aIndex + 1] - b[bIndex + 1]);
        var dr = Math.Abs(a[aIndex + 2] - b[bIndex + 2]);
        return Math.Max(db, Math.Max(dg, dr));
    }

    private static int[] EvenSamples(int length, int desired)
    {
        if (length <= 1) return [0];
        var count = Math.Min(desired, length);
        var samples = new int[count];
        for (var i = 0; i < count; i++)
        {
            var fraction = (i + 0.5) / count;
            samples[i] = Math.Clamp((int)(fraction * length), 0, length - 1);
        }
        return samples;
    }
}
