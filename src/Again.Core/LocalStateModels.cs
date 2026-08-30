namespace Again.Core;

public sealed record WorkflowHistoryEntry(
    Guid WorkflowId,
    string Name,
    DateTimeOffset LastRunAt,
    int Completed,
    int Skipped,
    int Errors,
    string Summary);

public sealed class LocalState
{
    public List<WorkflowDefinition> Workflows { get; set; } = [];
    public List<WorkflowHistoryEntry> History { get; set; } = [];
    public HashSet<string> ExcludedProcesses { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "CredentialUIBroker", "KeePass", "KeePassXC", "1Password", "Bitwarden", "LastPass"
    };
}
