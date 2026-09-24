using Again.Core;
using Microsoft.Playwright;
using System.Text.Json;
namespace Again.Windows;
public sealed partial class BrowserConnector
{
    bool recording, recordPaused, captureText, bindingInstalled; string approvedOrigin = ""; Action<Step>? onRecord;
    public bool Recording => recording;
    public static bool SameApprovedOrigin(string expected, string actual) { if (Safety.SameDomain(expected, actual)) return true; return Uri.TryCreate(expected, UriKind.Absolute, out var a) && Uri.TryCreate(actual, UriKind.Absolute, out var b) && a.IsLoopback && b.IsLoopback && a.Scheme == "http" && b.Scheme == "http" && a.Host == b.Host && a.Port == b.Port; }
    public async Task StartRecordingAsync(string url, bool text, Action<Step> capture)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))) throw new InvalidDataException("Use an HTTPS address, or localhost for a controlled test.");
        var p = await Page(); approvedOrigin = uri.GetLeftPart(UriPartial.Authority); captureText = text; onRecord = capture; recording = true; recordPaused = false;
        if (!bindingInstalled)
        {
            await p.ExposeBindingAsync<string>("__againEvent", (source, payload) =>
            {
                if (!recording || recordPaused || source.Frame != source.Page.MainFrame || !SameApprovedOrigin(approvedOrigin, source.Frame.Url)) return;
                using var json = JsonDocument.Parse(payload); var data = json.RootElement; var kind = data.GetProperty("kind").GetString(); if (kind == "type" && !captureText) return;
                var target = data.GetProperty("target").GetString() ?? ""; if (target.Length == 0 || target.Length > 500) return;
                var args = new Dictionary<string, string> { ["by"] = data.GetProperty("by").GetString() ?? "label", ["target"] = target };
                if (data.TryGetProperty("role", out var role)) args["role"] = role.GetString() ?? "Button";
                if (kind == "type") args["text"] = data.GetProperty("value").GetString() ?? "";
                onRecord?.Invoke(new() { Name = (kind == "type" ? "Enter text in " : "Click ") + target, Operation = kind == "type" ? Operation.BrowserType : Operation.BrowserClick, Method = Method.Browser, Target = new() { Domain = approvedOrigin }, Args = args });
            });
            await p.AddInitScriptAsync(RecorderScript);
            p.DOMContentLoaded += async (_, _) => { try { if (recording && !recordPaused && SameApprovedOrigin(approvedOrigin, p.Url)) { await p.EvaluateAsync("window.__againRecordEnabled=true"); onRecord?.Invoke(new() { Name = "Open " + new Uri(p.Url).Host, Operation = Operation.BrowserOpen, Method = Method.Browser, Args = new() { ["url"] = p.Url } }); } } catch (PlaywrightException) { } };
            bindingInstalled = true;
        }
        await p.GotoAsync(url); await p.EvaluateAsync(RecorderScript); await p.EvaluateAsync("window.__againRecordEnabled=true");
    }
    public async Task PauseRecordingAsync(bool paused) { recordPaused = paused; if (page is not null) await page.EvaluateAsync("enabled => window.__againRecordEnabled=enabled", recording && !paused); }
    public async Task StopRecordingAsync() { recording = false; onRecord = null; if (page is not null) try { await page.EvaluateAsync("window.__againRecordEnabled=false"); } catch (PlaywrightException) { } }
    const string RecorderScript = """
(()=>{
 if(window.__againRecorderInstalled)return;window.__againRecorderInstalled=true;window.__againRecordEnabled=false;
 const sensitive=e=>e.matches('input[type=password],input[type=hidden]')||/password|secret|token|credit|card.number|security.code|cc-number|cc-csc/i.test([e.name,e.id,e.autocomplete,e.getAttribute('aria-label')].join(' '));
 const identify=e=>{
  const label=e.labels?.[0]?.innerText?.trim()||e.getAttribute('aria-label');if(label)return {by:'label',target:label};
  if(e.placeholder)return {by:'placeholder',target:e.placeholder};
  const role=e.getAttribute('role')||(e.tagName==='BUTTON'?'Button':e.tagName==='A'?'Link':null);
  const text=e.innerText?.trim();if(role&&text)return {by:'role',role,target:text};return null;
 };
 document.addEventListener('click',event=>{if(!window.__againRecordEnabled||!event.isTrusted)return;const e=event.target.closest('button,a,input,[role],select');if(!e||sensitive(e)||e.tagName==='INPUT')return;const target=identify(e);if(target)window.__againEvent(JSON.stringify({kind:'click',...target}));},true);
 document.addEventListener('change',event=>{if(!window.__againRecordEnabled||!event.isTrusted)return;const e=event.target;if(!e.matches('input,textarea')||sensitive(e)||e.type==='file')return;const target=identify(e);if(target)window.__againEvent(JSON.stringify({kind:'type',...target,value:e.value}));},true);
})();
""";
}
