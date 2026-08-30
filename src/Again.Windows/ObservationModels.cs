using System.IO;

namespace Again.Windows;

public sealed record WindowObservation(
    DateTimeOffset Timestamp,
    string ProcessName,
    string WindowTitle,
    string? ControlType,
    string? AutomationId,
    string? SafeControlName);

public sealed record FileActivity(
    DateTimeOffset Timestamp,
    string Path,
    WatcherChangeTypes ChangeType);
