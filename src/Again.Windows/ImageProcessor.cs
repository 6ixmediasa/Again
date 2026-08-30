using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Again.Core;

namespace Again.Windows;

public static class ImageProcessor
{
    private sealed record RenderGeometry(BitmapSource Image, int Width, int Height);

    public static Task ProcessAsync(string inputPath, string outputPath, ImageResizeStep resize, OutputRule outputRule, CancellationToken cancellationToken)
    {
        return Task.Run(() => Process(inputPath, outputPath, resize, outputRule, cancellationToken), cancellationToken);
    }

    private static void Process(string inputPath, string outputPath, ImageResizeStep resize, OutputRule outputRule, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SafetyGuard.ValidateReplayTarget(inputPath, outputPath);

        BitmapFrame frame;
        using (var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            frame = decoder.Frames[0];
        }

        cancellationToken.ThrowIfCancellationRequested();
        var geometry = ResolveGeometry(frame, resize);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (outputRule.Format == ImageOutputFormat.Jpeg)
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, geometry.Width, geometry.Height));

            dc.DrawImage(geometry.Image, new Rect(0, 0, geometry.Width, geometry.Height));

            if (!string.IsNullOrWhiteSpace(resize.OverlayAssetPath))
            {
                if (!File.Exists(resize.OverlayAssetPath))
                    throw new IOException("The demonstrated visual overlay asset is missing. AGAIN stopped rather than silently dropping the text/edit.");

                var overlay = LoadBitmap(resize.OverlayAssetPath);
                DrawOverlayRelative(dc, overlay, geometry.Width, geometry.Height);
            }
        }

        var bitmap = new RenderTargetBitmap(geometry.Width, geometry.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        BitmapEncoder encoder = outputRule.Format switch
        {
            ImageOutputFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = Math.Clamp(outputRule.JpegQuality, 1, 100) },
            ImageOutputFormat.Png => new PngBitmapEncoder(),
            ImageOutputFormat.Bmp => new BmpBitmapEncoder(),
            ImageOutputFormat.Tiff => new TiffBitmapEncoder(),
            ImageOutputFormat.Gif => new GifBitmapEncoder(),
            _ => throw new NotSupportedException($"Unsupported output format: {outputRule.Format}")
        };

        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var tempPath = outputPath + ".again-tmp";
        try
        {
            using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                encoder.Save(output);

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, outputPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private static RenderGeometry ResolveGeometry(BitmapSource frame, ImageResizeStep resize)
    {
        BitmapSource imageToDraw = frame;

        if (resize.Crop is { IsValid: true } crop)
        {
            var rect = CalculateCropRect(frame.PixelWidth, frame.PixelHeight, crop);
            imageToDraw = new CroppedBitmap(frame, rect);
            imageToDraw.Freeze();
        }

        if (resize.GeometryMode == ImageGeometryMode.CropRelative)
        {
            if (!resize.HasCrop)
                throw new InvalidOperationException("This workflow is marked as a relative crop but has no valid crop region.");

            return new RenderGeometry(imageToDraw, imageToDraw.PixelWidth, imageToDraw.PixelHeight);
        }

        if (resize.GeometryMode == ImageGeometryMode.PreserveOriginal)
        {
            if (resize.HasCrop)
                throw new InvalidOperationException("A preserve-size workflow cannot also contain a crop region.");

            return new RenderGeometry(frame, frame.PixelWidth, frame.PixelHeight);
        }

        var sourceAspect = imageToDraw.PixelWidth / (double)imageToDraw.PixelHeight;
        var targetAspect = resize.Width / (double)resize.Height;
        var aspectDelta = Math.Abs(sourceAspect - targetAspect) / Math.Max(sourceAspect, targetAspect);
        if (aspectDelta > 0.025)
        {
            throw new InvalidOperationException(
                $"This item would be stretched ({imageToDraw.PixelWidth}×{imageToDraw.PixelHeight} → {resize.Width}×{resize.Height}). AGAIN stopped instead of distorting it.");
        }

        return new RenderGeometry(imageToDraw, resize.Width, resize.Height);
    }

    private static Int32Rect CalculateCropRect(int width, int height, NormalizedCrop crop)
    {
        var x = Math.Clamp((int)Math.Round(crop.X * width), 0, Math.Max(0, width - 1));
        var y = Math.Clamp((int)Math.Round(crop.Y * height), 0, Math.Max(0, height - 1));
        var cropWidth = Math.Clamp((int)Math.Round(crop.Width * width), 1, width - x);
        var cropHeight = Math.Clamp((int)Math.Round(crop.Height * height), 1, height - y);
        return new Int32Rect(x, y, cropWidth, cropHeight);
    }

    private static void DrawOverlayRelative(DrawingContext dc, BitmapSource overlay, int targetWidth, int targetHeight)
    {
        var bounds = FindOpaqueBounds(overlay);
        if (bounds is null)
            throw new IOException("The demonstrated visual overlay contains no visible pixels.");

        if (overlay.PixelWidth == targetWidth && overlay.PixelHeight == targetHeight)
        {
            dc.DrawImage(overlay, new Rect(0, 0, targetWidth, targetHeight));
            return;
        }

        var crop = new CroppedBitmap(overlay, bounds.Value);
        crop.Freeze();

        var scale = Math.Min(
            targetWidth / (double)overlay.PixelWidth,
            targetHeight / (double)overlay.PixelHeight);

        var x = bounds.Value.X / (double)overlay.PixelWidth * targetWidth;
        var y = bounds.Value.Y / (double)overlay.PixelHeight * targetHeight;
        var width = Math.Max(1, bounds.Value.Width * scale);
        var height = Math.Max(1, bounds.Value.Height * scale);

        dc.DrawImage(crop, new Rect(x, y, width, height));
    }

    private static Int32Rect? FindOpaqueBounds(BitmapSource overlay)
    {
        var width = overlay.PixelWidth;
        var height = overlay.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        overlay.CopyPixels(pixels, stride, 0);

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var alpha = pixels[y * stride + x * 4 + 3];
                if (alpha <= 8) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return null;

        return new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static BitmapSource LoadBitmap(string path)
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

    public static (int Width, int Height) GetExpectedOutputSize(string inputPath, ImageResizeStep resize)
    {
        var input = ImageInspector.Read(inputPath);

        return resize.GeometryMode switch
        {
            ImageGeometryMode.CropRelative when resize.Crop is { IsValid: true } crop =>
                GetCropSize(input.Width, input.Height, crop),
            ImageGeometryMode.PreserveOriginal => (input.Width, input.Height),
            _ => (resize.Width, resize.Height)
        };
    }

    private static (int Width, int Height) GetCropSize(int width, int height, NormalizedCrop crop)
    {
        var rect = CalculateCropRect(width, height, crop);
        return (rect.Width, rect.Height);
    }

    public static void Validate(string outputPath, string inputPath, ImageResizeStep resize)
    {
        if (!File.Exists(outputPath))
            throw new IOException("Expected output file was not created.");

        var info = new FileInfo(outputPath);
        if (info.Length <= 0)
            throw new IOException("Output file is empty.");

        var expected = GetExpectedOutputSize(inputPath, resize);
        var image = ImageInspector.Read(outputPath);
        if (image.Width != expected.Width || image.Height != expected.Height)
            throw new IOException($"Output validation failed. Expected {expected.Width}×{expected.Height}, got {image.Width}×{image.Height}.");
    }
}
