using System.ComponentModel;
using System.Runtime.CompilerServices;
using Again.Core;
namespace Again.App;
public sealed class WorkspaceViewModel : INotifyPropertyChanged
{
    public Store Store { get; }
    public Draft Draft { get; private set; }
    public bool Dirty { get; private set; }
    string status = "Ready · Your files stay on this computer.";
    public string Status { get => status; set { status = value; Changed(); } }
    public WorkspaceViewModel() { var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "6ixMediaSA", "AGAIN", "again.db"); Store = new(path); Draft = new(Guid.NewGuid(), new() { OutputFolder = Store.Settings().OutputFolder }, []); }
    public void New() { Draft = new(Guid.NewGuid(), new() { OutputFolder = Store.Settings().OutputFolder }, []); Dirty = true; Store.SaveDraft(Draft); }
    public void Restore(Draft d) { Draft = d; Dirty = true; Status = "Unfinished work restored. Review the next item before resuming."; }
    public void Edit(Workflow w) { Draft = Draft with { Workflow = w }; Dirty = true; Store.SaveDraft(Draft); Status = "Draft saved automatically."; }
    public void Inputs(IEnumerable<string> files) { Draft = Draft with { Inputs = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), NextItem = 0 }; Dirty = true; Store.SaveDraft(Draft); }
    public void Save() { Draft = Draft with { Workflow = Store.SaveAndRun(Draft) }; Dirty = false; Status = "Workflow saved. Your demonstration is preserved."; }
    public void SetDraft(Draft d) { Draft = d; Dirty = true; Store.SaveDraft(d); }
    public event PropertyChangedEventHandler? PropertyChanged; void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
