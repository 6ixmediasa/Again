using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using Again.Core;
using Again.Windows;

namespace Again.App;

public partial class MainWindow : Window
{
    private enum UiState { Empty, Ready, Watching, Running, Completed }

    private readonly List<string> _selectedFiles = [];
    private readonly LocalStateStore _stateStore = new();
    private LocalState _state;
    private DemonstrationSession? _session;
    private WorkflowDefinition? _workflow;
    private CancellationTokenSource? _runCts;
    private readonly ManualResetEventSlim _pauseGate = new(initialState: true);
    private volatile bool _skipRequested;
    private UiState _uiState = UiState.Empty;
    private string? _resultsDirectory;

    public MainWindow()
    {
        InitializeComponent();
        _state = _stateStore.Load();
        SetState(UiState.Empty);
    }

    private void SelectFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select images for AGAIN",
            Multiselect = true,
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.gif|All files|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        _selectedFiles.Clear();
        _selectedFiles.AddRange(dialog.FileNames.Select(Path.GetFullPath));
        FilesList.ItemsSource = null;
        FilesList.ItemsSource = _selectedFiles.Select((x, i) => i == 0 ? $"DEMO  ·  {Path.GetFileName(x)}" : $"{i + 1:00}  ·  {Path.GetFileName(x)}").ToArray();
        _workflow = null;
        _resultsDirectory = null;
        WorkflowName.Text = "Nothing yet";
        WorkflowSteps.Text = "The first selected image is the demonstration item.";
        StatusHeadline.Text = _selectedFiles.Count == 1 ? "Add at least one more image to prove repetition." : $"{_selectedFiles.Count} images ready.";
        StatusDetail.Text = "Click WATCH ME. In Paint you can crop, proportionally resize, add localized text/marks, rename and Save As. A text-only edit is supported too.";
        SetState(_selectedFiles.Count >= 2 ? UiState.Ready : UiState.Empty);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        StopSession();
        _selectedFiles.Clear();
        FilesList.ItemsSource = null;
        _workflow = null;
        WorkflowName.Text = "Nothing yet";
        WorkflowSteps.Text = "AGAIN will summarize the demonstrated image workflow here.";
        StatusHeadline.Text = "Choose the images you want to process.";
        StatusDetail.Text = "The first image is the demonstration. AGAIN compares the before/after image locally to infer relative crop, fixed resize, preserve-size visual edits, output format, destination and naming intent.";
        SetState(UiState.Empty);
    }

    private void Watch_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFiles.Count < 2) return;
        try
        {
            StopSession();
            _session = new DemonstrationSession(_selectedFiles[0], _state.ExcludedProcesses);
            _session.Start();

            Process.Start(new ProcessStartInfo
            {
                FileName = "mspaint.exe",
                Arguments = Quote(_selectedFiles[0]),
                UseShellExecute = true
            });

            StatusHeadline.Text = "I’m watching this demonstration locally.";
            StatusDetail.Text = "In Paint: crop, resize, or leave the dimensions unchanged and only add localized text/paint marks. Then rename and Save As/export it. When finished, return here and click AGAIN.";
            WorkflowName.Text = "Watching…";
            WorkflowSteps.Text = "AGAIN does not record your typed text. It infers supported visual edits by comparing the local before/after image.";
            SetState(UiState.Watching);
        }
        catch (Exception ex)
        {
            StopSession();
            SetState(UiState.Ready);
            ShowError("AGAIN could not start the Paint demonstration.", ex.Message);
        }
    }

    private async void Again_Click(object sender, RoutedEventArgs e)
    {
        if (_uiState == UiState.Watching)
        {
            await FinishDemonstrationAndRunAsync();
            return;
        }

        if (_workflow is not null && _uiState is UiState.Ready or UiState.Completed)
            await RunWorkflowAsync(_workflow);
    }

    private async Task FinishDemonstrationAndRunAsync()
    {
        if (_session is null) return;
        AgainButton.IsEnabled = false;
        StatusHeadline.Text = "Understanding what changed…";

        await Task.Delay(450);
        _session.Stop();

        var outputPath = _session.FindBestOutputCandidate();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            StatusHeadline.Text = "I couldn’t find the demonstrated output.";
            StatusDetail.Text = "AGAIN watches the selected image folder plus Desktop, Documents, Pictures and Downloads. Save/export the demo into one of those locations, then run WATCH ME again.";
            WorkflowName.Text = "No workflow detected";
            SetState(UiState.Ready);
            StopSession();
            return;
        }

        if (!ImageInspector.TryRead(outputPath, out var outputInfo) || outputInfo is null)
        {
            ShowError("The output was detected but could not be read as an image.", outputPath);
            SetState(UiState.Ready);
            StopSession();
            return;
        }

        var sourceInfo = _session.OriginalSourceInfo;
        var sawPaint = _session.SawPaint();
        var detection = WorkflowDetector.Detect(sourceInfo, outputInfo);
        StopSession();

        if (!detection.Success || detection.Workflow is null)
        {
            StatusHeadline.Text = "I watched it, but this workflow isn’t supported yet.";
            StatusDetail.Text = detection.Message;
            WorkflowName.Text = "Unsupported demonstration";
            SetState(UiState.Ready);
            return;
        }

        VisualIntentAnalysis visual;
        try
        {
            visual = VisualIntentAnalyzer.AnalyzeAndPersist(sourceInfo.Path, outputPath, detection.Workflow.Id);
        }
        catch (Exception ex)
        {
            StatusHeadline.Text = "I couldn’t safely understand the visual edit.";
            StatusDetail.Text = ex.Message + " No remaining images were changed.";
            WorkflowName.Text = "Unsupported demonstration";
            SetState(UiState.Ready);
            return;
        }

        if (!visual.Success || visual.Step is null)
        {
            StatusHeadline.Text = "I stopped instead of distorting the images.";
            StatusDetail.Text = visual.Message;
            WorkflowName.Text = "Ambiguous image transformation";
            SetState(UiState.Ready);
            return;
        }

        var name = visual.Step.GeometryMode switch
        {
            ImageGeometryMode.CropRelative when visual.Step.HasOverlay => "Relative crop + visual edit + export image",
            ImageGeometryMode.CropRelative => "Relative crop + export image",
            ImageGeometryMode.PreserveOriginal when visual.Step.HasOverlay => "Visual edit + export image",
            ImageGeometryMode.PreserveOriginal => "Rename/convert + export image",
            _ when visual.Step.HasOverlay => "Resize + visual edit + export image",
            _ => "Resize + export image"
        };

        _workflow = detection.Workflow with
        {
            Name = name,
            Resize = visual.Step,
            Adapter = "Paint demonstration → visual intent → Windows Imaging"
        };

        _resultsDirectory = _workflow.Output.DestinationDirectory;
        WorkflowName.Text = _workflow.Name;
        WorkflowSteps.Text = $"{_workflow.Resize}\nExport: {_workflow.Output.Format.ToString().ToUpperInvariant()}\nName rule: {_workflow.Output.FilenameTemplate}\nFolder: {_workflow.Output.DestinationDirectory}";
        StatusHeadline.Text = "Workflow detected. Doing the rest now.";
        StatusDetail.Text = visual.Message + " " + detection.Message + (sawPaint ? " Paint was observed during the demonstration." : string.Empty);

        SaveWorkflow(_workflow);
        await RunWorkflowAsync(_workflow);
    }

    private async Task RunWorkflowAsync(WorkflowDefinition workflow)
    {
        var remaining = _selectedFiles.Skip(1).ToArray();
        if (remaining.Length == 0) return;

        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;
        _pauseGate.Set();
        _skipRequested = false;
        SetState(UiState.Running);

        var started = DateTimeOffset.Now;
        var results = new List<BatchItemResult>();
        Progress.Maximum = remaining.Length;
        Progress.Value = 0;
        _resultsDirectory = workflow.Output.DestinationDirectory;

        for (var i = 0; i < remaining.Length; i++)
        {
            var input = remaining[i];
            try
            {
                await Task.Run(() => _pauseGate.Wait(token), token);
                token.ThrowIfCancellationRequested();

                if (_skipRequested)
                {
                    _skipRequested = false;
                    results.Add(new BatchItemResult(input, null, false, true, "Skipped by user."));
                    Progress.Value = i + 1;
                    continue;
                }

                var proposed = workflow.Output.ResolveOutputPath(input, sequenceNumber: i + 2);
                SafetyGuard.ValidateReplayTarget(input, proposed);
                var output = SafetyGuard.MakeCollisionSafe(proposed);

                CurrentItem.Text = Path.GetFileName(input);
                ProgressText.Text = $"Processing {i + 1} of {remaining.Length}  ·  {workflow.Resize}";
                StatusHeadline.Text = $"AGAIN · Processing {i + 1} of {remaining.Length}";
                StatusDetail.Text = workflow.Resize.GeometryMode switch
                {
                    ImageGeometryMode.CropRelative => "Current step: relative crop → visual overlay (if detected) → encode → validate.",
                    ImageGeometryMode.PreserveOriginal => "Current step: preserve image size → visual overlay (if detected) → encode → validate.",
                    _ => "Current step: proportional fixed resize → visual overlay (if detected) → encode → validate."
                };

                await ImageProcessor.ProcessAsync(input, output, workflow.Resize, workflow.Output, token);
                ImageProcessor.Validate(output, input, workflow.Resize);

                results.Add(new BatchItemResult(input, output, true, false, "Completed and validated."));
                Progress.Value = i + 1;
            }
            catch (OperationCanceledException)
            {
                results.Add(new BatchItemResult(input, null, false, true, "Stopped by user."));
                break;
            }
            catch (Exception ex)
            {
                results.Add(new BatchItemResult(input, null, false, false, ex.Message));
                StatusHeadline.Text = "Stopped safely.";
                StatusDetail.Text = $"{Path.GetFileName(input)} failed validation or processing: {ex.Message} No later items were attempted.";
                break;
            }
        }

        var summary = new BatchRunSummary(workflow.Id, started, DateTimeOffset.Now, results);
        SaveHistory(workflow, summary);
        CurrentItem.Text = string.Empty;
        ProgressText.Text = $"{summary.Completed} completed · {summary.Skipped} skipped · {summary.Errors} errors";

        if (summary.Errors == 0 && !token.IsCancellationRequested)
        {
            StatusHeadline.Text = "Done.";
            StatusDetail.Text = $"{summary.Completed} tasks completed, {summary.Errors} errors. Every produced image passed existence and per-item dimension validation.";
        }
        else if (token.IsCancellationRequested)
        {
            StatusHeadline.Text = "Stopped.";
            StatusDetail.Text = $"{summary.Completed} completed before the run was stopped.";
        }

        SetState(UiState.Completed);
        OpenResultsButton.Visibility = Directory.Exists(_resultsDirectory) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_pauseGate.IsSet)
        {
            _pauseGate.Reset();
            PauseButton.Content = "RESUME";
            StatusDetail.Text = "Paused. AGAIN will not start the next item until you resume.";
        }
        else
        {
            _pauseGate.Set();
            PauseButton.Content = "PAUSE";
        }
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        _skipRequested = true;
        StatusDetail.Text = "Skip requested. AGAIN will skip the current/next safe item boundary.";
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _pauseGate.Set();
        _runCts?.Cancel();
    }

    public void PauseMonitoringFromTray()
    {
        if (_uiState == UiState.Watching)
        {
            StopSession();
            StatusHeadline.Text = "Monitoring paused.";
            StatusDetail.Text = "The current demonstration was discarded for privacy/safety. Click WATCH ME when you want to demonstrate again.";
            SetState(_selectedFiles.Count >= 2 ? UiState.Ready : UiState.Empty);
        }
    }

    private void OpenResults_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_resultsDirectory) || !Directory.Exists(_resultsDirectory)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", Quote(_resultsDirectory)) { UseShellExecute = true });
    }

    private void FooterLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void SaveWorkflow(WorkflowDefinition workflow)
    {
        _state.Workflows.RemoveAll(x => x.Id == workflow.Id);
        _state.Workflows.Insert(0, workflow);
        if (_state.Workflows.Count > 25) _state.Workflows.RemoveRange(25, _state.Workflows.Count - 25);
        _stateStore.Save(_state);
    }

    private void SaveHistory(WorkflowDefinition workflow, BatchRunSummary summary)
    {
        _state.History.Insert(0, new WorkflowHistoryEntry(workflow.Id, workflow.Name, summary.FinishedAt, summary.Completed, summary.Skipped, summary.Errors, workflow.Summary));
        if (_state.History.Count > 100) _state.History.RemoveRange(100, _state.History.Count - 100);
        _stateStore.Save(_state);
    }

    private void StopSession()
    {
        _session?.Dispose();
        _session = null;
        MonitoringBadge.Text = "MONITORING OFF";
    }

    private void SetState(UiState state)
    {
        _uiState = state;
        SelectFilesButton.IsEnabled = state is not UiState.Watching and not UiState.Running;
        ClearButton.IsEnabled = state is not UiState.Watching and not UiState.Running;
        WatchButton.IsEnabled = state is UiState.Ready or UiState.Completed;
        AgainButton.IsEnabled = state == UiState.Watching || (_workflow is not null && state == UiState.Completed);
        PauseButton.IsEnabled = state == UiState.Running;
        SkipButton.IsEnabled = state == UiState.Running;
        StopButton.IsEnabled = state == UiState.Running;
        MonitoringBadge.Text = state == UiState.Watching ? "WATCHING LOCALLY" : "MONITORING OFF";
        if (state != UiState.Running) PauseButton.Content = "PAUSE";
        if (state != UiState.Completed) OpenResultsButton.Visibility = Visibility.Collapsed;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private void ShowError(string title, string detail)
    {
        MessageBox.Show(this, detail, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var app = (App)System.Windows.Application.Current;
        if (!app.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        StopSession();
        _runCts?.Cancel();
        _runCts?.Dispose();
        _pauseGate.Dispose();
        base.OnClosing(e);
    }
}
