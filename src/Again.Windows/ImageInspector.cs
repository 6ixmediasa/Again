using System.Windows.Media.Imaging;
using Again.Core;

namespace Again.Windows;

public static class ImageInspector
{
    public static ImageFileInfo Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var info = new FileInfo(path);
        return new ImageFileInfo(Path.GetFullPath(path), frame.PixelWidth, frame.PixelHeight, info.Exists ? info.Length : 0, info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue);
    }

    public static bool TryRead(string path, out ImageFileInfo? info)
    {
        try
        {
            info = Read(path);
            return true;
        }
        catch
        {
            info = null;
            return false;
        }
    }
}
