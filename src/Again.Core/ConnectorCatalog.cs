namespace Again.Core;
public sealed record ConnectorManifest(string Id,string Name,string Version,string Kind,string[] SupportedActions,Method Method,Reliability Reliability,string? MinimumVersion=null);
public sealed record ConnectorState(string Id,bool Enabled=true,DateTimeOffset? LastTested=null,string? Health=null);
public sealed class ConnectorCatalog(Store store){
 public static IReadOnlyList<ConnectorManifest> BuiltIn {get;}=[
  new("images","Image Quick Tools","0.2.0","Built in",["Crop","Resize","Text","Logo","Rotate","Flip","Brightness","Contrast","Saturation","Export"],Method.Internal,Reliability.High),
  new("windows","Windows applications","0.2.0","Accessibility",["Click","Type"],Method.UIAutomation,Reliability.High),
  new("browser","Browser","0.2.0","Playwright",["BrowserOpen","BrowserClick","BrowserType","BrowserExtract","BrowserUpload","BrowserDownload"],Method.Browser,Reliability.High),
  new("photoshop","Photoshop","0.2.0","Windows scripting bridge",["Launch","Crop","Resize","Text","Rotate","Export"],Method.Native,Reliability.High),
  new("flstudio","FL Studio","0.2.0","General accessibility profile",["Click","Type"],Method.UIAutomation,Reliability.Medium),
  new("capcut","CapCut","0.2.0","General accessibility profile",["Click","Type"],Method.UIAutomation,Reliability.Medium)];
 public ConnectorState State(string id)=>store.List<ConnectorState>("connector").FirstOrDefault(s=>s.Id==id)??new(id);
 public void Enable(string id,bool enabled){if(!BuiltIn.Any(c=>c.Id==id))throw new InvalidDataException("Unknown connector.");store.Save("connector",id,1,State(id) with{Enabled=enabled});}
 public void RecordHealth(string id,string health)=>store.Save("connector",id,1,State(id) with{LastTested=DateTimeOffset.UtcNow,Health=health});
}
