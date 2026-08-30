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

    public static VisualIntentAnalysis AnalyzeAndPersist(string sourcePath, string demonstratedOutputPath, Guid workflowId)
    {
        var source = LoadBgra32(sourcePath);
        var output = LoadBgra32(demonstratedOutputPath);

        var sourceAspect = source.PixelWidth / (double)source.PixelHeight;
        var outputAspect = output.PixelWidth / (double)output.PixelHeight;
        var aspectDelta = Math.Abs(sourceAspect - outputAspect) / Math.Max(sourceAspect, outputAspect);

        NormalizedCrop? crop = null;
        BitmapSource baseline;

        if (output.PixelWidth <= source.PixelWidth && output.PixelHeight <= source.PixelHeight &&
            (output.PixelWidth < source.PixelWidth || output.PixelHeight < source.PixelHeight))
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
            }
            else if (aspectDelta <= 0.02)
            {
                baseline = RenderToSize(source, output.PixelWidth, output.PixelHeight);
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
        }
        else
        {
            return VisualIntentAnalysis.Fail(
                "The demonstrated image changed aspect ratio in a way AGAIN cannot safely classify as a crop or proportional resize yet. Nothing was replayed.");
        }

        var overlayPath = TryExtractOverlay(baseline, output, workflowId);
        var step = new ImageResizeStep(output.PixelWidth, output.PixelHeight, crop, overlayPath);

        var description = crop is not null
            ? overlayPath is not null
                ? "Detected crop plus a localized visual edit (such as text/paint marks). The edit is replayed as a local transparent overlay; typed text itself is not recorded."
                : "Detected a crop from the demonstrated result."
            : overlayPath is not null
                ? "Detected proportional resize plus a localized visual edit (such as text/paint marks)."
                : "Detected a proportional resize.";

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

        var sampleXs = EvenSamples(outW, 18);
        var sampleYs = EvenSamples(outH, 14);
        var coarseStep = Math.Max(1, Math.Max(maxX, maxY) / 100);

        var best = FindBestOffset(
            sourcePixels, outputPixels,
            sourceStride, outputStride,
            sampleXs, sampleYs,
            0, maxX, 0, maxY, coarseStep);

        if (coarseStep > 1)
        {
            var radius = coarseStep * 2;
            best = FindBestOffset(
                sourcePixels, outputPixels,
                sourceStride, outputStride,
                sampleXs, sampleYs,
                Math.Max(0, best.X - radius), Math.Min(maxX, best.X + radius),
                Math.Max(0, best.Y - radius), Math.Min(maxY, best.Y + radius),
                1);
        }

        // A localized text/brush edit is allowed to disagree with the source,
        // while most sampled pixels should still identify the underlying crop.
        if (best.CloseRatio < 0.52 || best.AverageDifference > 72)
            return null;

        return new Int32Rect(best.X, best.Y, outW, outH);
    }

    private static (int X, int Y, double CloseRatio, double AverageDifference) FindBestOffset(
        byte[] sourcePixels,
        byte[] outputPixels,
        int sourceStride,
        int outputStride,
        int[] sampleXs,
        int[] sampleYs,
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
                var count = 0;

                foreach (var sy in sampleYs)
                {
                    foreach (var sx in sampleXs)
                    {
                        var sourceIndex = ((y + sy) * sourceStride) + ((x + sx) * 4);
                        var outputIndex = (sy * outputStride) + (sx * 4);
                        var diff = PixelDifference(sourcePixels, sourceIndex, outputPixels, outputIndex);
                        diffTotal += diff;
                        if (diff <= ClosePixelThreshold) close++;
                        count++;
                    }
                }

                var closeRatio = close / (double)count;
                var average = diffTotal / (double)count;
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

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * stride + x * 4;
                changed[y * width + x] = PixelDifference(basePixels, index, outputPixels, index) >= OverlayPixelThreshold;
            }
        }

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

                if (!changed[y * width + x] && neighbors < 2) continue;
                if (changed[y * width + x] && neighbors < 2) continue;

                var index = y * stride + x * 4;
                overlayPixels[index] = outputPixels[index];
                overlayPixels[index + 1] = outputPixels[index + 1];
                overlayPixels[index + 2] = outputPixels[index + 2];
                overlayPixels[index + 3] = 255;
                kept++;
            }
        }

        var ratio = kept / (double)(width * height);
        if (ratio < 0.00008)
            return null;

        // If too much of the frame differs, this was not a localized text/paint
        // edit. Refuse to turn broad image differences into a misleading overlay.
        if (ratio > 0.15)
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
