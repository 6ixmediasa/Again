using System.Diagnostics;
using System.Windows.Automation;
using Again.Core;
using Context = Again.Core.ExecutionContext;
namespace Again.Windows;
public sealed class TargetMissingException(string message) : Exception(message);
public sealed class WindowsConnector : IConnector
{
    public string Id => "windows"; public string Name => "Windows applications";
    public bool Supports(Step step) => step.Method is Method.UIAutomation or Method.Coordinates;
    public static Target TargetUnderPointer() { Native.GetCursorPos(out var p); return Describe(AutomationElement.FromPoint(new System.Windows.Point(p.X, p.Y))); }
    public static Target Describe(AutomationElement element) { var p = Process.GetProcessById(element.Current.ProcessId); var root = AutomationElement.FromHandle(p.MainWindowHandle); return new() { ProcessPath = p.MainModule?.FileName ?? "", WindowTitle = root.Current.Name, WindowClass = root.Current.ClassName, AutomationId = element.Current.AutomationId, Name = element.Current.IsPassword ? "Sensitive field" : element.Current.Name, ControlType = element.Current.ControlType.ProgrammaticName }; }
    public static AutomationElement Find(Target target)
    {
        if (string.IsNullOrWhiteSpace(target.ProcessPath)) throw new TargetMissingException("This step has no verified application. Repair its target.");
        var active = Native.GetForegroundWindow(); if (!string.Equals(Native.ProcessPath(active), target.ProcessPath, StringComparison.OrdinalIgnoreCase)) throw new TargetMissingException("The expected application is not active. Switch to it and retry.");
        var window = AutomationElement.FromHandle(active); if (!string.IsNullOrEmpty(target.WindowClass) && window.Current.ClassName != target.WindowClass) throw new TargetMissingException("The active window is different. Check for an unexpected dialog.");
        if (!string.IsNullOrEmpty(target.WindowTitle) && window.Current.Name != target.WindowTitle) throw new TargetMissingException("The window title changed. Review and repair this step before continuing.");
        var conditions = new List<Condition>(); if (target.AutomationId != "") conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, target.AutomationId)); if (target.Name != "") conditions.Add(new PropertyCondition(AutomationElement.NameProperty, target.Name));
        if (conditions.Count == 0) throw new TargetMissingException("No semantic target was captured. Select the target again.");
        var matches = window.FindAll(TreeScope.Descendants, conditions.Count == 1 ? conditions[0] : new AndCondition(conditions.ToArray())); var valid = new List<AutomationElement>(); foreach (AutomationElement e in matches) if (e.Current.IsEnabled && !e.Current.IsOffscreen && (target.ControlType == "" || e.Current.ControlType.ProgrammaticName == target.ControlType)) valid.Add(e);
        if (valid.Count != 1) throw new TargetMissingException(valid.Count == 0 ? "The button or field could not be found. Repair its target." : "Several controls match. Select a more specific target."); return valid[0];
    }
    public static void ActivateExpected(Target target)
    {
        var current = Native.GetForegroundWindow(); if (string.Equals(Native.ProcessPath(current), target.ProcessPath, StringComparison.OrdinalIgnoreCase) && AutomationElement.FromHandle(current).Current.Name == target.WindowTitle) return;
        var matches = new List<nint>(); foreach (var process in Process.GetProcesses()) { using (process) try { if (process.MainWindowHandle != 0 && string.Equals(process.MainModule?.FileName, target.ProcessPath, StringComparison.OrdinalIgnoreCase) && process.MainWindowTitle == target.WindowTitle) matches.Add(process.MainWindowHandle); } catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { } }
        if (matches.Count != 1 || !Native.SetForegroundWindow(matches[0])) throw new TargetMissingException("Switch to the expected application window, then retry.");
    }
    public Task ExecuteAsync(Step step, Context context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var target = step.Target ?? throw new TargetMissingException("Choose an application target.");
        if (step.Method == Method.Coordinates) { var h = Native.GetForegroundWindow(); if (!string.Equals(Native.ProcessPath(h), target.ProcessPath, StringComparison.OrdinalIgnoreCase) || AutomationElement.FromHandle(h).Current.Name != target.WindowTitle) throw new TargetMissingException("The expected application window is not active."); if (target.Confidence < .9) throw new TargetMissingException("The target confidence is too low. Repair it first."); Native.GetWindowRect(h, out var r); Native.SetCursorPos(r.Left + (int)((r.Right - r.Left) * target.X), r.Top + (int)((r.Bottom - r.Top) * target.Y)); token.ThrowIfCancellationRequested(); if (Native.GetForegroundWindow() != h) throw new TargetMissingException("The active window changed."); Native.mouse_event(2, 0, 0, 0, 0); Native.mouse_event(4, 0, 0, 0, 0); return Task.CompletedTask; }
        ActivateExpected(target); var element = Find(target); if (element.Current.IsPassword) throw new TargetMissingException("Enter sensitive information manually, then continue.");
        switch (step.Operation)
        {
            case Operation.Click:
                if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) ((InvokePattern)invoke).Invoke(); else if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var select)) ((SelectionItemPattern)select).Select(); else if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle)) ((TogglePattern)toggle).Toggle(); else if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expand)) ((ExpandCollapsePattern)expand).Expand(); else throw new TargetMissingException("This control needs another execution method. Repair the step."); break;
            case Operation.Type: if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) || ((ValuePattern)value).Current.IsReadOnly) throw new TargetMissingException("This field cannot be filled through Windows accessibility."); ((ValuePattern)value).SetValue(Safety.Expand(step.Get("text"), context.Values)); break;
            default: throw new InvalidDataException("This Windows action is not yet supported by this connector.");
        }
        return Task.CompletedTask;
    }
}
public sealed class ApplicationProcessor(IEnumerable<IConnector> connectors, Func<Step, Exception, Task<RepairDecision>> repair) : IItemProcessor
{
    public async Task<string> ProcessAsync(Workflow workflow, string input, int index, IReadOnlyDictionary<string, string> variables, PauseGate pause, CancellationToken token)
    {
        var values = Safety.Values(input, index); values["input"] = input; foreach (var v in variables) values[v.Key] = v.Value; var context = new Context { Input = input, Output = "", Values = values };
        foreach (var recordedStep in workflow.Steps.Where(x => x.Enabled).ToList())
        {
            var step = recordedStep; await pause.WaitAsync(token); if (!Conditions.Evaluate(step.When, values)) continue; var connector = connectors.FirstOrDefault(c => c.Supports(step)) ?? throw new InvalidDataException("Enable a connector for " + step.Name); int attempt = 0;
            while (true) { try { using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds)); await connector.ExecuteAsync(step, context, timeout.Token); break; } catch (Exception e) when (e is not OperationCanceledException || !token.IsCancellationRequested) { if (attempt++ < step.Retries) continue; var choice = await repair(step, e); if (choice.Replacement is not null) { step = choice.Replacement; var indexOfStep = workflow.Steps.FindIndex(s => s.Id == recordedStep.Id); workflow.Steps[indexOfStep] = step; } if (choice.Action == "retry") continue; if (choice.Action is "skip" or "manual") break; throw new InvalidDataException("The workflow stopped at: " + step.Name); } }
        }
        if (context.Output != "") Safety.VerifyOutput(context.Output); return context.Output;
    }
}
