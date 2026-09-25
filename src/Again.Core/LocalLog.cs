using System.Text.Json;
namespace Again.Core;
public sealed class LocalLog(string directory)
{
    readonly object gate = new();
    public void Write(string eventName, Guid workflow, Guid? step, string code)
    {
        // Deliberately excludes exception messages, paths, text input, URLs, clipboard and screenshots.
        if (eventName.Length > 80 || code.Length > 80) throw new ArgumentException("Use a short diagnostic code.");
        var line = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, eventName, workflow, step, code }); lock (gate) { Directory.CreateDirectory(directory); File.AppendAllText(Path.Combine(directory, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl"), line + Environment.NewLine); }
    }
}
