using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Again.Core;

namespace Again.Windows;

public static class ImageProcessor
{
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

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (outputRule.Format == ImageOutputFormat.Jpeg)
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, resize.Width, resize.Height));
            dc.DrawImage(frame, new Rect(0, 0, resize.Width, resize.Height));
        }

        var bitmap = new RenderTargetBitmap(resize.Width, resize.Height, 96, 96, PixelFormats.Pbgra32);
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

    public static void Validate(string outputPath, ImageResizeStep resize)
    {
        if (!File.Exists(outputPath))
            throw new IOException("Expected output file was not created.");

        var info = new FileInfo(outputPath);
        if (info.Length <= 0)
            throw new IOException("Output file is empty.");

        var image = ImageInspector.Read(outputPath);
        if (image.Width != resize.Width || image.Height != resize.Height)
            throw new IOException($"Output validation failed. Expected {resize.Width}×{resize.Height}, got {image.Width}×{image.Height}.");
    }
}
