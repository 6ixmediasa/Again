using Again.Core;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
namespace Again.Imaging;
public static class CropMath {
 public static Rectangle Calculate(int width,int height,double ratio,double anchorX=.5,double anchorY=.5){
  if(width<1||height<1||!double.IsFinite(ratio)||ratio<=0||anchorX is <0 or >1||anchorY is <0 or >1)throw new InvalidDataException("Choose a valid crop ratio and position.");
  int w=width,h=height;if((double)w/h>ratio)w=Math.Max(1,(int)Math.Round(h*ratio));else h=Math.Max(1,(int)Math.Round(w/ratio));
  w=Math.Min(w,width);h=Math.Min(h,height);return new((int)Math.Round((width-w)*anchorX),(int)Math.Round((height-h)*anchorY),w,h);
 }
 public static Rectangle Relative(int width,int height,double x,double y,double w,double h){if(x<0||y<0||w<=0||h<=0||x+w>1||y+h>1)throw new InvalidDataException("The crop region must fit inside the image.");var left=(int)Math.Floor(width*x);var top=(int)Math.Floor(height*y);return new(left,top,Math.Min(width-left,Math.Max(1,(int)Math.Round(width*w))),Math.Min(height-top,Math.Max(1,(int)Math.Round(height*h))));}
}
public sealed class ImageEngine {
 public async Task<Image<Rgba32>> RenderAsync(string input,IEnumerable<Step> steps,IReadOnlyDictionary<string,string> variables,PauseGate? pause=null,CancellationToken token=default){
  var info=await Image.IdentifyAsync(input,token);if(info.Width*(long)info.Height>100_000_000)throw new InvalidDataException("This image is too large to process safely.");
  var image=await Image.LoadAsync<Rgba32>(input,token);
  try{image.Mutate(c=>c.AutoOrient());var values=new Dictionary<string,string>(variables){["width"]=image.Width.ToString(),["height"]=image.Height.ToString()};
   foreach(var step in steps.Where(s=>s.Enabled)){
    token.ThrowIfCancellationRequested();if(pause is not null)await pause.WaitAsync(token);if(!Conditions.Evaluate(step.When,values))continue;
    switch(step.Operation){
     case Operation.Crop:
      var crop=step.Get("strategy")=="relative"?CropMath.Relative(image.Width,image.Height,step.Number("x"),step.Number("y"),step.Number("width",1),step.Number("height",1)):CropMath.Calculate(image.Width,image.Height,step.Number("ratio",.8),step.Number("anchorX",.5),step.Number("anchorY",.5));image.Mutate(c=>c.Crop(crop));break;
     case Operation.Resize:
      var width=(int)step.Number("width",1080);var height=(int)step.Number("height",1080);if(width<1||height<1||width*(long)height>100_000_000)throw new InvalidDataException("Choose safe output dimensions.");image.Mutate(c=>c.Resize(new ResizeOptions{Size=new(width,height),Mode=step.Get("strategy")=="fill"?ResizeMode.Crop:ResizeMode.Max}));break;
     case Operation.Text:DrawText(image,step,values);break;
     case Operation.Logo:
      using(var logo=await Image.LoadAsync<Rgba32>(Safety.Expand(step.Get("file"),values),token)){
       int lw=Math.Clamp((int)(image.Width*step.Number("scale",.2)),1,image.Width);logo.Mutate(c=>c.Resize(new ResizeOptions{Size=new(lw,image.Height),Mode=ResizeMode.Max}));var x=(int)((image.Width-logo.Width)*Math.Clamp(step.Number("x",.9),0,1));var y=(int)((image.Height-logo.Height)*Math.Clamp(step.Number("y",.9),0,1));image.Mutate(c=>c.DrawImage(logo,new Point(x,y),(float)Math.Clamp(step.Number("opacity",1),0,1)));}break;
     case Operation.Rotate:image.Mutate(c=>c.Rotate((float)step.Number("degrees",90)));break;
     case Operation.Flip:image.Mutate(c=>c.Flip(step.Get("axis")=="vertical"?FlipMode.Vertical:FlipMode.Horizontal));break;
     case Operation.Brightness:image.Mutate(c=>c.Brightness((float)Math.Clamp(step.Number("amount",1),0,3)));break;
     case Operation.Contrast:image.Mutate(c=>c.Contrast((float)Math.Clamp(step.Number("amount",1),0,3)));break;
     case Operation.Saturation:image.Mutate(c=>c.Saturate((float)Math.Clamp(step.Number("amount",1),0,3)));break;
     case Operation.Export:break;
     default:throw new InvalidDataException("This action cannot run inside an image task: "+step.Name);
    }
    values["width"]=image.Width.ToString();values["height"]=image.Height.ToString();
   }
   return image;
  }catch{image.Dispose();throw;}
 }
 static void DrawText(Image<Rgba32> image,Step step,IReadOnlyDictionary<string,string> values){
  var text=Safety.Expand(step.Get("text","AGAIN"),values);if(string.IsNullOrEmpty(text))throw new InvalidDataException("Add text content before running this step.");
  FontFamily family;if(!SystemFonts.TryGet(step.Get("font","Segoe UI"),out family)&&!SystemFonts.TryGet(step.Get("fallback","DejaVu Sans"),out family))family=SystemFonts.Families.FirstOrDefault();
  if(string.IsNullOrEmpty(family.Name))throw new InvalidDataException("No usable font is installed. Choose an installed font.");
  var size=(float)Math.Clamp(image.Width*step.Number("scale",.065),5,500);var margin=(float)(Math.Min(image.Width,image.Height)*Math.Clamp(step.Number("margin",.05),0,.4));
  var maxWidth=(float)(image.Width*Math.Clamp(step.Number("maxWidth",.9),.1,1));maxWidth=Math.Min(maxWidth,image.Width-2*margin);
  RichTextOptions Options(float fs)=>new(family.CreateFont(fs,step.Get("weight")=="bold"?FontStyle.Bold:FontStyle.Regular)){WrappingLength=maxWidth,TextAlignment=step.Get("align")=="left"?TextAlignment.Start:step.Get("align")=="right"?TextAlignment.End:TextAlignment.Center};
  var options=Options(size);var bounds=TextMeasurer.MeasureSize(text,options);while(size>5&&(bounds.Height>image.Height-2*margin||bounds.Width>maxWidth+1)){size--;options=Options(size);bounds=TextMeasurer.MeasureSize(text,options);}
  float x=margin+(image.Width-2*margin-maxWidth)*(float)Math.Clamp(step.Number("x",.5),0,1);float y=margin+(image.Height-2*margin-bounds.Height)*(float)Math.Clamp(step.Number("y",.92),0,1);options.Origin=new(x,y);
  var color=Color.ParseHex(step.Get("color","FFFFFF")).WithAlpha((float)Math.Clamp(step.Number("opacity",1),0,1));
  if(step.Get("shadow","true")=="true"){options.Origin=new(x+2,y+2);image.Mutate(c=>c.DrawText(options,text,Color.Black.WithAlpha(.7f)));options.Origin=new(x,y);}
  image.Mutate(c=>c.DrawText(options,text,color));
 }
 public static async Task SaveAsync(Image image,string path,string format,int quality=90,bool preserveMetadata=false,CancellationToken token=default){
  if(!preserveMetadata){image.Metadata.ExifProfile=null;image.Metadata.IccProfile=null;image.Metadata.IptcProfile=null;image.Metadata.XmpProfile=null;}
  SixLabors.ImageSharp.Formats.IImageEncoder encoder=format.ToLowerInvariant() switch{"png"=>new PngEncoder(),"jpg" or "jpeg"=>new JpegEncoder{Quality=Math.Clamp(quality,1,100)},"webp"=>new WebpEncoder{Quality=Math.Clamp(quality,1,100)},_=>throw new InvalidDataException("Choose PNG, JPG or WebP for image output.")};
  await image.SaveAsync(path,encoder,token);Safety.VerifyOutput(path);await Image.IdentifyAsync(path,token);
 }
}
public sealed class ImageProcessor(ImageEngine engine):IItemProcessor {
 public async Task<string> ProcessAsync(Workflow w,string input,int index,IReadOnlyDictionary<string,string> variables,PauseGate pause,CancellationToken token){
  if(!File.Exists(input))throw new FileNotFoundException();var values=Safety.Values(input,index);foreach(var v in variables)values[v.Key]=v.Value;
  foreach(var v in w.Variables){if(v.Secret)throw new InvalidDataException("Secret inputs cannot be used in image workflows.");if(!values.ContainsKey(v.Name)&&v.Default is not null)values[v.Name]=v.Default;if(v.Required&&(!values.TryGetValue(v.Name,out var val)||string.IsNullOrWhiteSpace(val)))throw new InvalidDataException("Provide "+v.Name+" before running.");}
  using var image=await engine.RenderAsync(input,w.Steps,values,pause,token);Directory.CreateDirectory(w.OutputFolder);
  var name=Safety.Filename(Safety.Expand(w.Naming,values));var path=Safety.Under(w.OutputFolder,name+"."+Safety.Filename(w.Format));
  if(string.Equals(Path.GetFullPath(input),Path.GetFullPath(path),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new InvalidDataException("Choose a separate output path to preserve the original.");
  if(File.Exists(path)&&w.Conflict is Conflict.NeverOverwrite or Conflict.Ask)throw new InvalidDataException("An output already exists. Choose another name or the numbered-copy option.");
  var temporary=Path.Combine(w.OutputFolder,".again-"+Guid.NewGuid().ToString("N")+".tmp");
  try{await ImageEngine.SaveAsync(image,temporary,w.Format,w.Quality,w.PreserveMetadata,token);await pause.WaitAsync(token);token.ThrowIfCancellationRequested();
   if(w.Conflict==Conflict.NumberedCopy){var candidate=path;for(var n=1;;n++){try{File.Move(temporary,candidate,false);path=candidate;break;}catch(IOException)when(File.Exists(candidate)){candidate=Safety.Under(w.OutputFolder,name+" ("+n+")."+Safety.Filename(w.Format));}}}
   else File.Move(temporary,path,w.Conflict==Conflict.Replace);Safety.VerifyOutput(path);return path;
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}
