using Again.Core;
using Again.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
namespace Again.Tests;
public sealed class RecoveryIntegrationTests:IDisposable {
 readonly string root=Path.Combine(Path.GetTempPath(),"again-integration-"+Guid.NewGuid());
 public RecoveryIntegrationTests()=>Directory.CreateDirectory(root);
 public void Dispose()=>Directory.Delete(root,true);
 sealed class WarningProcessor:IItemProcessor{public Task<string> ProcessAsync(Workflow w,string input,int index,IReadOnlyDictionary<string,string> values,PauseGate pause,CancellationToken token)=>throw new ItemWarningException("","Skipped: optional action");}
 [Fact]public async Task SkippedStepIsNotReportedAsComplete(){var store=new Store(Path.Combine(root,"state.db"));var result=await new BatchRunner(store,new WarningProcessor()).RunAsync(new(Guid.NewGuid(),new(),["item"]));Assert.Equal(ItemStatus.CompletedWithWarning,result.Items.Single().Status);Assert.Contains("Skipped",result.Items.Single().Error);}
 [Fact]public async Task CorruptImageDoesNotStopRemainingImages(){
  var good1=Path.Combine(root,"one.png");var bad=Path.Combine(root,"broken.png");var good2=Path.Combine(root,"two.png");
  using(var image=new Image<Rgba32>(100,180,new Rgba32(20,30,40))){await image.SaveAsPngAsync(good1);await image.SaveAsPngAsync(good2);}await File.WriteAllTextAsync(bad,"not a valid image");
  var store=new Store(Path.Combine(root,"state.db"));var w=new Workflow{OutputFolder=Path.Combine(root,"out"),Steps=[new(){Operation=Operation.Crop,Args=new(){["ratio"]="0.8"}},new(){Operation=Operation.Text,Args=new(){["text"]="All images"}}]};
  var run=await new BatchRunner(store,new ImageProcessor(new())).RunAsync(new(Guid.NewGuid(),w,[good1,bad,good2]));
  Assert.Equal(new[]{ItemStatus.Completed,ItemStatus.Failed,ItemStatus.Completed},run.Items.Select(i=>i.Status));Assert.All(run.Items.Where(i=>i.Output is not null),i=>Safety.VerifyOutput(i.Output!));Assert.Equal("not a valid image",await File.ReadAllTextAsync(bad));
 }
 [Fact]public async Task ReplaceCannotOverwriteTheSource(){var source=Path.Combine(root,"source.png");using(var image=new Image<Rgba32>(100,100))await image.SaveAsPngAsync(source);var original=await File.ReadAllBytesAsync(source);var w=new Workflow{OutputFolder=root,Naming="{original-name}",Conflict=Conflict.Replace};await Assert.ThrowsAsync<InvalidDataException>(()=>new ImageProcessor(new()).ProcessAsync(w,source,1,new Dictionary<string,string>(),new(),default));Assert.Equal(original,await File.ReadAllBytesAsync(source));}
 [Fact]public async Task CancellationRetainsDraftAndDoesNotPublish(){var source=Path.Combine(root,"source.png");using(var image=new Image<Rgba32>(30,30))await image.SaveAsPngAsync(source);using var token=new CancellationTokenSource();token.Cancel();var store=new Store(Path.Combine(root,"state.db"));var w=new Workflow{OutputFolder=Path.Combine(root,"out")};var run=await new BatchRunner(store,new ImageProcessor(new())).RunAsync(new(Guid.NewGuid(),w,[source]),token:token.Token);Assert.Equal(ItemStatus.Cancelled,run.Items.Single().Status);Assert.Single(new Store(Path.Combine(root,"state.db")).List<Draft>("draft"));Assert.False(Directory.Exists(w.OutputFolder));}
 [Fact]public async Task FileMovesAndUndoPreserveContents(){var source=Path.Combine(root,"source.txt");await File.WriteAllTextAsync(source,"original contents");var tools=new FileTools();var plan=tools.Plan([source],Path.Combine(root,"out"),"{original-name}-{number}","Move");var changes=await tools.ExecuteAsync(plan,default);Assert.False(File.Exists(source));Assert.Equal("original contents",await File.ReadAllTextAsync(changes.Single().Destination));tools.Undo(changes);Assert.Equal("original contents",await File.ReadAllTextAsync(source));}
 [Fact]public async Task DuplicateDetectionUsesContents(){var a=Path.Combine(root,"a.txt");var b=Path.Combine(root,"b.txt");var c=Path.Combine(root,"c.txt");await File.WriteAllTextAsync(a,"same");await File.WriteAllTextAsync(b,"same");await File.WriteAllTextAsync(c,"other");var groups=await new FileTools().DuplicatesAsync([a,b,c],default);Assert.Equal(2,Assert.Single(groups).Count);}
}
