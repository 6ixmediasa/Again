namespace Again.Core;
public sealed class ExecutionContext
{
    public required string Input { get; init; }
    public required string Output { get; set; }
    public required Dictionary<string, string> Values { get; init; }
    public Dictionary<string, object> State { get; } = [];
    public Action<string>? Progress { get; init; }
}
public sealed record RepairDecision(string Action, Step? Replacement = null);
public interface IConnector { string Id { get; } string Name { get; } bool Supports(Step step); Task ExecuteAsync(Step step, ExecutionContext context, CancellationToken token); }
public interface IItemProcessor { Task<string> ProcessAsync(Workflow workflow, string input, int index, IReadOnlyDictionary<string, string> variables, PauseGate pause, CancellationToken token); }
public sealed class BatchRunner(Store store, IItemProcessor processor)
{
    public PauseGate Pause { get; } = new(); public event Action<int, ItemResult>? Changed;
    public async Task<RunRecord> RunAsync(Draft draft, IReadOnlyDictionary<string, string>? variables = null, CancellationToken token = default)
    {
        var w = store.SaveAndRun(draft); var started = DateTimeOffset.UtcNow; var runId = Guid.NewGuid(); var results = new List<ItemResult>();
        for (var i = draft.NextItem; i < draft.Inputs.Count; i++)
        {
            var input = draft.Inputs[i]; ItemResult item;
            try { await Pause.WaitAsync(token); token.ThrowIfCancellationRequested(); store.SaveDraft(draft with { Workflow = w, NextItem = i, State = "Running" }); Changed?.Invoke(i, new(input, null, ItemStatus.Running)); var output = await processor.ProcessAsync(w, input, i + 1, variables ?? new Dictionary<string, string>(), Pause, token); item = new(input, string.IsNullOrEmpty(output) ? null : output, ItemStatus.Completed); }
            catch (OperationCanceledException) { item = new(input, null, ItemStatus.Cancelled); results.Add(item); Changed?.Invoke(i, item); break; }
            catch (Exception e) { item = new(input, null, ItemStatus.Failed, UserErrors.Describe(e)); }
            results.Add(item); Changed?.Invoke(i, item); store.Save("run", runId.ToString(), 1, new RunRecord(runId, w.Id, w.Version, w.Name, started, DateTimeOffset.UtcNow, results.ToList())); store.SaveDraft(draft with { Workflow = w, NextItem = i + 1, State = "Running" });
            if (item.Status == ItemStatus.Failed && w.OnFailure != FailureRule.Continue) break;
        }
        var run = new RunRecord(runId, w.Id, w.Version, w.Name, started, DateTimeOffset.UtcNow, results); store.Save("run", run.Id.ToString(), 1, run);
        if (results.Count == draft.Inputs.Count - draft.NextItem && results.All(x => x.Status != ItemStatus.Cancelled)) store.Delete("draft", draft.Id.ToString());
        return run;
    }
}
public static class UserErrors
{
    public static string Describe(Exception e) => e switch { UnauthorizedAccessException => "AGAIN cannot access this file or folder. Choose a writable output folder.", FileNotFoundException => "A source file is missing. Choose the file again and retry.", DirectoryNotFoundException => "A folder was moved or removed. Choose the folder again.", TimeoutException => "The application did not respond in time. Check its window and retry.", OperationCanceledException => "The task was cancelled. Your unfinished work is saved.", InvalidDataException => e.Message, IOException => "The file could not be read or saved. Check that it is not locked and retry. " + e.Message, _ => "This step could not finish. Review the step and retry. " + e.GetType().Name };
}
