using System.Runtime.InteropServices;
using System.Text.Json;
using Again.Core;
using Context=Again.Core.ExecutionContext;
namespace Again.Windows;
/// <summary>Optional Windows Photoshop scripting bridge. Uses fixed, typed commands only.</summary>
public sealed class PhotoshopConnector:IConnector {
 public string Id=>"photoshop";public string Name=>"Adobe Photoshop";public bool Supports(Step s)=>s.Method==Method.Native&&s.Get("connector")==Id;
 public bool Installed=>Type.GetTypeFromProgID("Photoshop.Application") is not null;
 public Task ExecuteAsync(Step step,Context context,CancellationToken token){token.ThrowIfCancellationRequested();if(!Installed)throw new InvalidDataException("Photoshop's Windows scripting bridge is not installed. Open Photoshop or repair its installation.");
  var type=Type.GetTypeFromProgID("Photoshop.Application")!;object? app=null;try{app=Activator.CreateInstance(type);dynamic photoshop=app!;
   var args=step.Args.ToDictionary(x=>x.Key,x=>Safety.Expand(x.Value,context.Values));args["operation"]=step.Operation.ToString();args["input"]=context.Input;string payload=JsonSerializer.Serialize(args);
   var script="var a="+payload+";\n"+Script;string result=photoshop.DoJavaScript(script);if(result.StartsWith("ERROR:",StringComparison.Ordinal))throw new InvalidDataException("Photoshop could not complete this step: "+result[6..]);
   if(step.Operation==Operation.Export){context.Output=args["output"];Safety.VerifyOutput(context.Output);}return Task.CompletedTask;
  }finally{if(app is not null&&Marshal.IsComObject(app))Marshal.FinalReleaseComObject(app);}
 }
 const string Script="""
(function(){
 var oldUnits=app.preferences.rulerUnits; app.preferences.rulerUnits=Units.PIXELS;
 try {
  if(a.operation==='Launch'){app.open(new File(a.input)); return 'OK';}
  if(!app.documents.length)throw new Error('Open a document first.');
  var d=app.activeDocument,w=d.width.as('px'),h=d.height.as('px');
  if(a.operation==='Crop'){
   var ratio=Number(a.ratio||0.8),cw=w,ch=h,ax=Number(a.anchorX||0.5),ay=Number(a.anchorY||0.5);
   if(!(ratio>0&&isFinite(ratio)))throw new Error('Invalid crop ratio.');
   if(w/h>ratio)cw=h*ratio;else ch=w/ratio;
   var x=(w-cw)*ax,y=(h-ch)*ay;d.crop([UnitValue(x,'px'),UnitValue(y,'px'),UnitValue(x+cw,'px'),UnitValue(y+ch,'px')]);
  }else if(a.operation==='Resize'){
   var scale=Math.min(Number(a.width||1080)/w,Number(a.height||1080)/h);d.resizeImage(UnitValue(w*scale,'px'),UnitValue(h*scale,'px'),null,ResampleMethod.BICUBIC);
  }else if(a.operation==='Text'){
   var layer=d.artLayers.add();layer.kind=LayerKind.TEXT;layer.name='AGAIN text';var t=layer.textItem;t.contents=a.text||'';
   t.size=UnitValue(w*Number(a.scale||0.065),'px');try{t.font=a.font||'ArialMT';}catch(fontError){t.font='ArialMT';}
   var color=new SolidColor();color.rgb.hexValue=a.color||'FFFFFF';t.color=color;t.justification=Justification.CENTER;
   t.position=[UnitValue(w*Number(a.x||0.5),'px'),UnitValue(h*Number(a.y||0.9),'px')];
  }else if(a.operation==='Rotate'){d.rotateCanvas(Number(a.degrees||90));
  }else if(a.operation==='Export'){
   var f=new File(a.output);if(f.exists)throw new Error('Output already exists. Choose a new name.');
   var opts;if((a.format||'jpg')==='png'){opts=new PNGSaveOptions();}else{opts=new JPEGSaveOptions();opts.quality=Math.max(1,Math.min(12,Number(a.quality||10)));}
   d.saveAs(f,opts,true,Extension.LOWERCASE);
  }else throw new Error('This action is not supported by the scripting bridge.');
  return 'OK';
 }catch(e){return 'ERROR:'+e.message;}finally{app.preferences.rulerUnits=oldUnits;}
})();
""";
}
