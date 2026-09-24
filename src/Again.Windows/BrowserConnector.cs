using Again.Core;
using Microsoft.Playwright;
using Context = Again.Core.ExecutionContext;
namespace Again.Windows;
public sealed partial class BrowserConnector : IConnector, IAsyncDisposable
{
    IPlaywright? playwright; IBrowser? browser; IPage? page;
    public string Id => "browser"; public string Name => "Browser"; public bool Supports(Step s) => s.Method == Method.Browser;
    async Task<IPage> Page() { if (page is not null) return page; playwright = await Playwright.CreateAsync(); browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false }); page = await browser.NewPageAsync(new() { AcceptDownloads = true }); return page; }
    static ILocator Find(IPage page, Step s) => s.Get("by", "label") switch { "role" => page.GetByRole(Enum.Parse<AriaRole>(s.Get("role", "Button"), true), new() { Name = s.Get("target"), Exact = true }), "text" => page.GetByText(s.Get("target"), new() { Exact = true }), "placeholder" => page.GetByPlaceholder(s.Get("target"), new() { Exact = true }), _ => page.GetByLabel(s.Get("target"), new() { Exact = true }) };
    public async Task ExecuteAsync(Step s, Context c, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var p = await Page(); p.SetDefaultTimeout(s.TimeoutSeconds * 1000);
        if (s.Operation == Operation.BrowserOpen) { var url = Safety.Expand(s.Get("url"), c.Values); if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) throw new InvalidDataException("Use an HTTP or HTTPS address."); await p.GotoAsync(url); return; }
        if (s.Target?.Domain is not { Length: > 0 } domain || !SameApprovedOrigin(domain, p.Url)) throw new TargetMissingException("The browser domain changed. Review the address before continuing.");
        var element = Find(p, s); switch (s.Operation)
        {
            case Operation.BrowserClick: await element.ClickAsync(); break;
            case Operation.BrowserType: if (await element.GetAttributeAsync("type") == "password") throw new TargetMissingException("Enter this password yourself, then resume."); await element.FillAsync(Safety.Expand(s.Get("text"), c.Values)); break;
            case Operation.BrowserUpload: await element.SetInputFilesAsync(c.Input); break;
            case Operation.BrowserExtract: var text = await element.InnerTextAsync(); var destination = Safety.Expand(s.Get("output"), c.Values); await File.WriteAllTextAsync(destination, text, token); Safety.VerifyOutput(destination); c.Output = destination; break;
            case Operation.BrowserDownload: var download = await p.RunAndWaitForDownloadAsync(() => element.ClickAsync()); var output = Safety.Expand(s.Get("output"), c.Values); if (File.Exists(output)) throw new IOException("The output already exists."); await download.SaveAsAsync(output); if (await download.FailureAsync() is not null) throw new IOException("The browser download failed."); Safety.VerifyOutput(output); c.Output = output; break;
            default: throw new InvalidDataException("Choose a supported browser action.");
        }
    }
    public async ValueTask DisposeAsync() { if (browser is not null) await browser.CloseAsync(); playwright?.Dispose(); }
}
