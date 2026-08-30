using System.Collections.Concurrent;
using System.IO;

namespace Again.Windows;

public sealed class FileActivityObserver : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentDictionary<string, FileActivity> _latest = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<FileActivity> Activities => _latest.Values.ToArray();

    public void Start(string demonstrationInput)
    {
        Stop();
        _latest.Clear();

        var roots = GetObservationRoots(demonstrationInput);
        foreach (var root in roots)
        {
            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                    EnableRaisingEvents = false
                };
                foreach (var filter in new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tif", "*.tiff", "*.gif" })
                    watcher.Filters.Add(filter);

                watcher.Created += OnChanged;
                watcher.Changed += OnChanged;
                watcher.Renamed += OnRenamed;
                watcher.Error += (_, _) => { };
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch
            {
                // Some user folders can be unavailable/redirection targets. Remaining roots still work.
            }
        }
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsImage(e.FullPath)) return;
        _latest[e.FullPath] = new FileActivity(DateTimeOffset.Now, e.FullPath, e.ChangeType);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsImage(e.FullPath)) return;
        _latest[e.FullPath] = new FileActivity(DateTimeOffset.Now, e.FullPath, WatcherChangeTypes.Renamed);
    }

    private static IReadOnlyList<string> GetObservationRoots(string demonstrationInput)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inputDir = Path.GetDirectoryName(Path.GetFullPath(demonstrationInput));
        if (!string.IsNullOrWhiteSpace(inputDir) && Directory.Exists(inputDir)) roots.Add(inputDir);

        foreach (var folder in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        })
        {
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)) roots.Add(folder);
        }

        return roots.OrderByDescending(x => x.Length).ToArray();
    }

    private static bool IsImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tif" or ".tiff" or ".gif";
    }

    public void Dispose() => Stop();
}
