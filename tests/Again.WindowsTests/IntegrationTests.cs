using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Again.Core;
using Again.Windows;
using Xunit;
using Context = Again.Core.ExecutionContext;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Again.WindowsTests;
public class IntegrationTests
{
    [DllImport("user32.dll")] static extern bool MoveWindow(nint hwnd, int x, int y, int width, int height, bool repaint);
    static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null) { if (File.Exists(Path.Combine(d.FullName, "Again.sln"))) return d.FullName; d = d.Parent; } throw new DirectoryNotFoundException("Run tests from the repository build."); }
    [Fact]
    public async Task SemanticReplaySurvivesWindowMoveAndResize()
    {
        var path = Path.Combine(Root(), "tests", "Again.TestHost", "bin", "Release", "net8.0-windows", "Again.TestHost.exe"); using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = false })!;
        try
        {
            process.WaitForInputIdle(10000); var until = DateTime.UtcNow.AddSeconds(10); while (process.MainWindowHandle == 0 && DateTime.UtcNow < until) { await Task.Delay(100); process.Refresh(); }
            Assert.NotEqual(nint.Zero, process.MainWindowHandle);
            var root = AutomationElement.FromHandle(process.MainWindowHandle); var field = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "project-name")); Assert.NotNull(field); var target = WindowsConnector.Describe(field); var connector = new WindowsConnector(); var context = new Context { Input = "test", Output = "", Values = new() };
            var type = new Step { Name = "Enter project name", Operation = Operation.Type, Method = Method.UIAutomation, Target = target, Args = new() { ["text"] = "AGAIN semantic test" } };
            await connector.ExecuteAsync(type, context, default); MoveWindow(process.MainWindowHandle, 120, 160, 850, 550, true); await connector.ExecuteAsync(type with { Args = new() { ["text"] = "Resized successfully" } }, context, default);
            var button = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "apply")); await connector.ExecuteAsync(new() { Operation = Operation.Click, Method = Method.UIAutomation, Target = WindowsConnector.Describe(button) }, context, default);
            var result = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "result")); Assert.Equal("Applied: Resized successfully", result.Current.Name);
            Assert.Throws<TargetMissingException>(() => WindowsConnector.Find(target with { ProcessPath = "C:\\unexpected-app.exe" }));
        }
        finally { if (!process.HasExited) { process.CloseMainWindow(); if (!process.WaitForExit(3000)) process.Kill(); } }
    }
    [Fact]
    public async Task BrowserRecordsSemanticActionsAndVerifiesDownload()
    {
        using var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start(); var port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop(); var origin = $"http://127.0.0.1:{port}";
        using var server = new HttpListener(); server.Prefixes.Add(origin + "/"); server.Start(); using var stop = new CancellationTokenSource(); var html = await File.ReadAllTextAsync(Path.Combine(Root(), "tests", "browser-host", "index.html"));
        var serving = Task.Run(async () => { while (!stop.IsCancellationRequested) { try { var request = await server.GetContextAsync(); var bytes = Encoding.UTF8.GetBytes(html); request.Response.ContentType = "text/html"; await request.Response.OutputStream.WriteAsync(bytes); request.Response.Close(); } catch (HttpListenerException) { break; } catch (ObjectDisposedException) { break; } } });
        var folder = Path.Combine(Path.GetTempPath(), "again-browser-" + Guid.NewGuid()); Directory.CreateDirectory(folder); await using var connector = new BrowserConnector(); var events = new List<Step>();
        try
        {
            await connector.StartRecordingAsync(origin, true, step => { lock (events) events.Add(step); }); var context = new Context { Input = "test", Output = "", Values = new() }; var target = new Target { Domain = origin };
            await connector.ExecuteAsync(new() { Operation = Operation.BrowserType, Method = Method.Browser, Target = target, Args = new() { ["by"] = "label", ["target"] = "Project name", ["text"] = "Browser acceptance" } }, context, default);
            await connector.ExecuteAsync(new() { Operation = Operation.BrowserClick, Method = Method.Browser, Target = target, Args = new() { ["by"] = "role", ["role"] = "Button", ["target"] = "Apply" } }, context, default);
            await Task.Delay(250); await connector.StopRecordingAsync(); lock (events) { Assert.Contains(events, e => e.Operation == Operation.BrowserClick && e.Get("target") == "Apply"); Assert.Contains(events, e => e.Operation == Operation.BrowserType && e.Get("target") == "Project name"); }
            var output = Path.Combine(folder, "result.txt"); await connector.ExecuteAsync(new() { Operation = Operation.BrowserDownload, Method = Method.Browser, Target = target, Args = new() { ["by"] = "role", ["role"] = "Button", ["target"] = "Download result", ["output"] = output } }, context, default); Assert.Equal("Applied: Browser acceptance", await File.ReadAllTextAsync(output)); Assert.Equal(output, context.Output);
            await Assert.ThrowsAsync<TargetMissingException>(() => connector.ExecuteAsync(new() { Operation = Operation.BrowserClick, Method = Method.Browser, Target = new() { Domain = "https://unexpected.example" } }, context, default));
        }
        finally { stop.Cancel(); server.Stop(); await serving; Directory.Delete(folder, true); }
    }
}
