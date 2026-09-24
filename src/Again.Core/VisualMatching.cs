namespace Again.Core;
public sealed record VisualMatch(int X,int Y,int Width,int Height,double Confidence,double RunnerUpConfidence){public bool Reliable(double threshold=.9,double separation=.03)=>Confidence>=threshold&&Confidence-RunnerUpConfidence>=separation;}
/// <summary>Offline normalized cross-correlation, evaluated over a caller-approved region.</summary>
public static class VisualMatching {
 public static VisualMatch Match(ReadOnlySpan<byte> pixels,int width,int height,ReadOnlySpan<byte> template,int tw,int th,CancellationToken token=default){
  if(width<1||height<1||tw<2||th<2||tw>width||th>height||pixels.Length!=checked(width*height)||template.Length!=checked(tw*th))throw new InvalidDataException("The visual target or search region is invalid.");
  if((long)(width-tw+1)*(height-th+1)*tw*th>500_000_000)throw new InvalidDataException("Select a smaller search region for this visual target.");
  double mean=0;foreach(var p in template)mean+=p;mean/=template.Length;double energy=0;foreach(var p in template)energy+=(p-mean)*(p-mean);if(energy<1)throw new InvalidDataException("Choose a target containing distinct visual details.");
  var scores=new List<(int X,int Y,double Score)>();for(int y=0;y<=height-th;y++){token.ThrowIfCancellationRequested();for(int x=0;x<=width-tw;x++){double sum=0,square=0,cross=0;for(int j=0;j<th;j++)for(int i=0;i<tw;i++){double p=pixels[(y+j)*width+x+i];sum+=p;square+=p*p;cross+=p*(template[j*tw+i]-mean);}double variance=square-sum*sum/(tw*th);double score=variance>0?cross/Math.Sqrt(energy*variance):0;scores.Add((x,y,score));}}
  var best=scores.MaxBy(x=>x.Score);var runnerUp=scores.Where(s=>Math.Abs(s.X-best.X)>tw/2||Math.Abs(s.Y-best.Y)>th/2).Select(s=>s.Score).DefaultIfEmpty(-1).Max();return new(best.X,best.Y,tw,th,Math.Clamp(best.Score,0,1),Math.Clamp(runnerUp,0,1));
 }
}
