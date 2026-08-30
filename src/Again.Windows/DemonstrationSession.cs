using Again.Core;

namespace Again.Windows;

public sealed class DemonstrationSession : IDisposable
{
    private readonly FileActivityObserver _fileObserver;
    private readonly ForegroundWindowObserver _windowObserver;

    public DemonstrationSession(string sourcePath, IEnumerable<string>? excludedProcesses = null)
    {
        SourcePath = Path.GetFullPath(sourcePath);
        _fileObserver = new FileActivityObserver();
        _windowObserver = new ForegroundWindowObserver(excludedProcesses);
    }

    public string SourcePath { get; }
    public ImageFileInfo OriginalSourceInfo { get; private set; } = null!;
    public DateTimeOffset StartedAt { get; private set; }
    public IReadOnlyList<WindowObservation> WindowSamples => _windowObserver.Samples;

    public void Start()
    {
        OriginalSourceInfo = ImageInspector.Read(SourcePath);
        StartedAt = DateTimeOffset.Now;
        _fileObserver.Start(SourcePath);
        _windowObserver.Start();
    }

    public void Stop()
    {
        _windowObserver.Stop();
        _fileObserver.Stop();
    }

    public string? FindBestOutputCandidate()
    {
        var sourceFull = Path.GetFullPath(SourcePath);
        var activities = _fileObserver.Activities
            .Where(x => x.Timestamp >= StartedAt)
            .OrderByDescending(x => x.Timestamp)
            .ToArray();

        var source = OriginalSourceInfo;
        var scored = new List<(string Path, int Score, DateTimeOffset Timestamp)>();
        foreach (var activity in activities)
        {
            if (!File.Exists(activity.Path))
                continue;

            if (!ImageInspector.TryRead(activity.Path, out var candidate) || candidate is null)
                continue;

            var score = 0;
            if (!string.Equals(candidate.Path, sourceFull, StringComparison.OrdinalIgnoreCase)) score += 20;
            if (candidate.Width != source.Width || candidate.Height != source.Height) score += 50;
            if (!string.Equals(Path.GetExtension(candidate.Path), Path.GetExtension(sourceFull), StringComparison.OrdinalIgnoreCase)) score += 10;
            if (activity.ChangeType is WatcherChangeTypes.Created or WatcherChangeTypes.Renamed) score += 8;
            scored.Add((candidate.Path, score, activity.Timestamp));
        }

        return scored.OrderByDescending(x => x.Score).ThenByDescending(x => x.Timestamp).FirstOrDefault().Path;
    }

    public bool SawPaint()
    {
        return WindowSamples.Any(x => x.ProcessName.Equals("mspaint", StringComparison.OrdinalIgnoreCase) ||
                                      x.ProcessName.Equals("paint", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        Stop();
        _fileObserver.Dispose();
        _windowObserver.Dispose();
    }
}
