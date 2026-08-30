using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace Again.Windows;

public sealed class ForegroundWindowObserver : IDisposable
{
    private readonly HashSet<string> _excludedProcesses;
    private readonly List<WindowObservation> _samples = [];
    private readonly object _gate = new();
    private System.Threading.Timer? _timer;

    public ForegroundWindowObserver(IEnumerable<string>? excludedProcesses = null)
    {
        _excludedProcesses = new HashSet<string>(excludedProcesses ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<WindowObservation> Samples
    {
        get { lock (_gate) return _samples.ToArray(); }
    }

    public void Start() => _timer ??= new System.Threading.Timer(_ => Sample(), null, 0, 350);

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Sample()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return;

            using var process = Process.GetProcessById((int)pid);
            var processName = process.ProcessName;
            if (_excludedProcesses.Contains(processName)) return;

            var title = NativeMethods.GetWindowText(hwnd);
            string? controlType = null;
            string? automationId = null;
            string? safeName = null;

            try
            {
                var root = AutomationElement.FromHandle(hwnd);
                var focused = AutomationElement.FocusedElement;
                if (focused is not null && root is not null)
                {
                    var type = focused.Current.ControlType;
                    controlType = type?.ProgrammaticName;
                    automationId = focused.Current.AutomationId;
                    if (type == ControlType.Button || type == ControlType.MenuItem || type == ControlType.TabItem || type == ControlType.Window)
                    {
                        var name = focused.Current.Name;
                        safeName = string.IsNullOrWhiteSpace(name) ? null : name[..Math.Min(name.Length, 80)];
                    }
                }
            }
            catch
            {
                // UI Automation can fail for elevated/secure windows; metadata sampling still continues.
            }

            lock (_gate)
            {
                _samples.Add(new WindowObservation(DateTimeOffset.Now, processName, title, controlType, automationId, safeName));
                if (_samples.Count > 5000) _samples.RemoveRange(0, 500);
            }
        }
        catch
        {
            // Observation must never destabilize the desktop app.
        }
    }

    public void Dispose() => Stop();

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        public static string GetWindowText(IntPtr hWnd)
        {
            var buffer = new StringBuilder(512);
            _ = GetWindowText(hWnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }
    }
}
