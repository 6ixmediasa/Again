using System.Text.Json;
using Again.Core;

namespace Again.Windows;

public sealed class LocalStateStore
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public string StatePath { get; }

    public LocalStateStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "6ixMedia SA", "AGAIN");
        Directory.CreateDirectory(root);
        StatePath = Path.Combine(root, "state.json");
    }

    public LocalState Load()
    {
        try
        {
            if (!File.Exists(StatePath)) return new LocalState();
            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<LocalState>(json, _jsonOptions) ?? new LocalState();
        }
        catch
        {
            return new LocalState();
        }
    }

    public void Save(LocalState state)
    {
        var temp = StatePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, _jsonOptions));
        File.Move(temp, StatePath, overwrite: true);
    }
}
