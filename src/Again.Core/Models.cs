using System.Text.Json;
using System.Text.Json.Serialization;
namespace Again.Core;
public enum Method { Native, UIAutomation, Visual, Coordinates, Internal, Browser }
public enum Reliability { High, Medium, Low }
public enum Operation { Crop, Resize, Text, Logo, Rotate, Flip, Brightness, Contrast, Saturation, Export, Copy, Move, Rename, CreateFolder, WaitFile, Click, Type, Shortcut, Launch, WaitWindow, BrowserOpen, BrowserClick, BrowserType, BrowserExtract, BrowserUpload, BrowserDownload, Confirm }
public enum Conflict { NeverOverwrite, Ask, Replace, NumberedCopy }
public enum FailureRule { Continue, Stop, Ask }
public enum ItemStatus { Queued, Preparing, WaitingForApplication, Running, WaitingForUser, Completed, CompletedWithWarning, Failed, Skipped, Cancelled }
public sealed record Target
{
    public string ProcessPath { get; init; } = ""; public string WindowTitle { get; init; } = ""; public string WindowClass { get; init; } = "";
    public string AutomationId { get; init; } = ""; public string Name { get; init; } = ""; public string ControlType { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Confidence { get; init; } = 1;
    public string? Template { get; init; }
    public string? Domain { get; init; }
}
public sealed record Variable(string Name, string Kind = "Text", string? Default = null, bool Required = false, bool Secret = false);
public sealed record Predicate(string Kind, string Value = "", bool Negate = false);
public sealed record Step
{
    public Guid Id { get; init; } = Guid.NewGuid(); public string Name { get; init; } = "New step"; public Operation Operation { get; init; }
    public Method Method { get; init; } = Method.Internal; public Reliability Reliability { get; init; } = Reliability.High;
    public bool Enabled { get; init; } = true; public Target? Target { get; init; }
    public Dictionary<string, string> Args { get; init; } = [];
    public Predicate? When { get; init; }
    public int Retries { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public FailureRule OnFailure { get; init; } = FailureRule.Continue;
    public string Get(string key, string fallback = "") => Args.GetValueOrDefault(key, fallback);
    public double Number(string key, double fallback = 0) => double.TryParse(Get(key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : fallback;
}
public sealed record Workflow
{
    public const int CurrentSchema = 1; public int SchemaVersion { get; init; } = CurrentSchema;
    public Guid Id { get; init; } = Guid.NewGuid(); public int Version { get; init; } = 1; public string Name { get; init; } = "Untitled workflow";
    public string Description { get; init; } = ""; public string Category { get; init; } = "General"; public bool Favorite { get; init; }
    public bool Archived { get; init; }
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow; public DateTimeOffset Modified { get; init; } = DateTimeOffset.UtcNow;
    public List<Step> Steps { get; init; } = []; public List<Variable> Variables { get; init; } = [];
    public string OutputFolder { get; init; } = ""; public string Naming { get; init; } = "{original-name}-again"; public string Format { get; init; } = "png";
    public int Quality { get; init; } = 90; public bool PreserveMetadata { get; init; }
    public Conflict Conflict { get; init; } = Conflict.NumberedCopy; public FailureRule OnFailure { get; init; } = FailureRule.Continue;
}
public sealed record Draft(Guid Id, Workflow Workflow, List<string> Inputs, int NextItem = 0, string State = "Editing", DateTimeOffset? Updated = null);
public sealed record ItemResult(string Input, string? Output, ItemStatus Status, string? Error = null);
public sealed record RunRecord(Guid Id, Guid WorkflowId, int WorkflowVersion, string Name, DateTimeOffset Started, DateTimeOffset Finished, List<ItemResult> Items);
public sealed record Settings
{
    public string OutputFolder { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AGAIN");
    public string Theme { get; init; } = "System"; public bool ReducedMotion { get; init; }
    public bool CaptureKeyboard { get; init; }
    public int TimeoutSeconds { get; init; } = 30; public int RetryCount { get; init; } = 1; public double Confidence { get; init; } = .9;
    public int JpegQuality { get; init; } = 90; public bool PreserveMetadata { get; init; }
    public int HistoryRetentionDays { get; init; } = 90;
    public int ScreenshotRetentionDays { get; init; } = 7; public bool Diagnostics { get; init; }
}
public static class WorkflowJson
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException("The file is empty.");
    public static Workflow Import(string json) { if (json.Length > 4_000_000) throw new InvalidDataException("Workflow exceeds the size limit."); var w = Read<Workflow>(json); Validate(w); return w; }
    public static void Validate(Workflow w)
    {
        if(w.Steps is null||w.Variables is null||w.Steps.Any(s=>s is null||s.Args is null))throw new InvalidDataException("Workflow data is incomplete.");
        if (w.SchemaVersion != Workflow.CurrentSchema) throw new InvalidDataException("This workflow version is not supported. Keep the original and update AGAIN.");
        if (string.IsNullOrWhiteSpace(w.Name) || w.Steps.Count > 2000 || w.Steps.Select(s => s.Id).Distinct().Count() != w.Steps.Count) throw new InvalidDataException("Invalid workflow name or steps.");
        if (w.Variables.Any(v => v.Secret && v.Default is not null)) throw new InvalidDataException("Secret values must be stored in Windows Credential Manager.");
        foreach (var s in w.Steps)
        {
            if (!Enum.IsDefined(s.Operation) || !Enum.IsDefined(s.Method) || s.Retries is < 0 or > 10 || s.TimeoutSeconds is < 1 or > 3600) throw new InvalidDataException("Invalid step settings.");
            if (s.Args.Keys.Any(k => k.Equals("password", StringComparison.OrdinalIgnoreCase) || k.Equals("script", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Embedded passwords and scripts are not allowed.");
            if(s.Method==Method.Coordinates&&s.Operation!=Operation.Click)throw new InvalidDataException("Coordinate fallback currently supports clicks only.");
            if (s.Method == Method.Coordinates && (s.Target is null || s.Target.X is < 0 or > 1 || s.Target.Y is < 0 or > 1)) throw new InvalidDataException("A coordinate step needs a valid relative target.");
        }
    }
}
