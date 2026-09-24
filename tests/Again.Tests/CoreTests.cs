using Again.Core;
using Again.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
namespace Again.Tests;
public sealed class CoreTests:IDisposable {
 readonly string root=Path.Combine(Path.GetTempPath(),"again-tests-"+Guid.NewGuid());
 public CoreTests()=>Directory.CreateDirectory(root);
 string PathFor(string name)=>Path.Combine(root,name);
 Store NewStore()=>new(PathFor("test.db"));
 public void Dispose()=>Directory.Delete(root,true);
 [Fact] public void SerializationPreservesTextAndCrop(){var w=new Workflow{Steps=[new(){Operation=Operation.Crop,Args=new(){["ratio"]="0.8"}},new(){Operation=Operation.Text,Args=new(){["text"]="Hello {original-name}"}}]};var copy=WorkflowJson.Import(WorkflowJson.Write(w));Assert.Equal("Hello {original-name}",copy.Steps[1].Get("text"));Assert.Equal(Operation.Crop,copy.Steps[0].Operation);}
 [Fact] public void FutureSchemaRejected()=>Assert.Throws<InvalidDataException>(()=>WorkflowJson.Validate(new(){SchemaVersion=42}));
 [Fact] public void EmbeddedSecretsRejected()=>Assert.Throws<InvalidDataException>(()=>WorkflowJson.Validate(new(){Variables=[new("password",Secret:true,Default:"secret")]}));
 [Theory][InlineData("../secret")][InlineData("a/../../secret")][InlineData("a\\..\\secret")][InlineData("/etc/passwd")]
 public void TraversalRejected(string path)=>Assert.Throws<InvalidDataException>(()=>Safety.Under(root,path));
 [Fact] public void FilenameSanitized(){Assert.Equal("a_b_c",Safety.Filename("a/b:c"));Assert.Equal("_CON",Safety.Filename("CON"));}
 [Fact] public void UnknownVariableFails()=>Assert.Throws<InvalidDataException>(()=>Safety.Expand("{missing}",new Dictionary<string,string>()));
 [Fact] public void VariablesExpand()=>Assert.Equal("photo-003",Safety.Expand("{original-name}-{number}",Safety.Values("photo.png",3)));
 [Fact] public void Redaction()=>Assert.Equal("token=[REDACTED]",Safety.Redact("token=abc123",["abc123"]));
 [Theory][InlineData("https://example.com","https://example.com/path",true)][InlineData("https://example.com","https://example.com.evil.test",false)][InlineData("https://example.com","http://example.com",false)]
 public void DomainVerification(string expected,string actual,bool valid)=>Assert.Equal(valid,Safety.SameDomain(expected,actual));
 [Fact] public void DraftRestoresFromNewStore(){var d=new Draft(Guid.NewGuid(),new(){Name="Not manually saved",Steps=[new(){Operation=Operation.Text,Args=new(){["text"]="Persistent"}}]},["photo.png"],2);NewStore().SaveDraft(d);var restored=NewStore().List<Draft>("draft").Single();Assert.Equal(2,restored.NextItem);Assert.Equal("Persistent",restored.Workflow.Steps[0].Get("text"));}
 [Fact] public void ForgottenSaveKeepsDemonstration(){var store=NewStore();var d=new Draft(Guid.NewGuid(),new(){Steps=[new(){Operation=Operation.Crop},new(){Operation=Operation.Text}]},["input.png"]);store.SaveDraft(d);var saved=store.SaveAndRun(d);Assert.Equal(2,saved.Steps.Count);Assert.Single(store.List<Workflow>("workflow"));Assert.Equal("Ready to run",store.List<Draft>("draft").Single().State);}
 [Fact] public void VersionHistoryRetained(){var store=NewStore();var d=new Draft(Guid.NewGuid(),new(){Name="First"},[]);store.SaveAndRun(d);store.SaveAndRun(d with{Workflow=d.Workflow with{Name="Second"}});Assert.Equal(2,store.List<Workflow>("workflow",true).Count);Assert.Equal("Second",store.List<Workflow>("workflow").Single().Name);}
 [Fact] public void SettingsPersist(){NewStore().SaveSettings(new(){JpegQuality=78,Theme="Dark"});Assert.Equal(78,NewStore().Settings().JpegQuality);}
 [Fact] public void MissingFileCondition(){Assert.True(Conditions.Evaluate(new("File does not exist",PathFor("missing")),new Dictionary<string,string>()));}
 [Theory][InlineData(400,600)][InlineData(600,400)][InlineData(500,500)][InlineData(1200,2400)]
 public void CropPreservesAspectWithoutDistortion(int w,int h){var r=CropMath.Calculate(w,h,.8);Assert.InRange(Math.Abs((double)r.Width/r.Height-.8),0,.003);Assert.True(r.Right<=w&&r.Bottom<=h);}
 [Fact] public void InvalidRelativeCropRejected()=>Assert.Throws<InvalidDataException>(()=>CropMath.Relative(500,500,.8,0,.4,1));
 [Fact] public async Task PauseResumeAndCancellation(){var gate=new PauseGate();gate.Pause();using var cancel=new CancellationTokenSource();var waiting=gate.WaitAsync(cancel.Token);Assert.False(waiting.IsCompleted);gate.Resume();await waiting;gate.Pause();cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>gate.WaitAsync(cancel.Token));}
 [Fact] public void EmptyOutputRejected(){File.WriteAllText(PathFor("empty"),"");Assert.Throws<IOException>(()=>Safety.VerifyOutput(PathFor("empty")));}
 [Theory][InlineData(400,600,"text")][InlineData(600,400,"crop")][InlineData(500,500,"both")][InlineData(600,400,"both")][InlineData(400,600,"logo")]
 public async Task ImageOperationsPersistAcrossOrientations(int width,int height,string mode){
  var source=PathFor("original.png");using(var initial=new Image<Rgba32>(width,height,new Rgba32(25,50,90)))await initial.SaveAsPngAsync(source);var before=await File.ReadAllBytesAsync(source);
  var steps=new List<Step>();if(mode!="text")steps.Add(new(){Operation=Operation.Crop,Args=new(){["ratio"]="0.8"}});if(mode!="crop")steps.Add(new(){Operation=Operation.Text,Args=new(){["text"]="HELLO",["font"]="missing-font-for-fallback",["color"]="FFFFFF"}});
  if(mode=="logo"){using(var logo=new Image<Rgba32>(50,50,new Rgba32(255,0,0)))await logo.SaveAsPngAsync(PathFor("logo.png"));steps.Add(new(){Operation=Operation.Logo,Args=new(){["file"]=PathFor("logo.png")}});}
  var w=new Workflow{Steps=steps,OutputFolder=PathFor("out")};var p=new ImageProcessor(new());var output=await p.ProcessAsync(w,source,1,new Dictionary<string,string>(),new(),default);
  using var result=await Image.LoadAsync<Rgba32>(output);if(mode!="text")Assert.InRange(Math.Abs((double)result.Width/result.Height-.8),0,.003);else Assert.Equal(width,result.Width);
  if(mode!="crop"){var bright=0;result.ProcessPixelRows(accessor=>{for(int y=0;y<accessor.Height;y++)foreach(var pixel in accessor.GetRowSpan(y))if(pixel.R>180&&pixel.G>180&&pixel.B>180)bright++;});Assert.True(bright>20,"Rendered output must contain actual text pixels.");}
  Assert.Equal(before,await File.ReadAllBytesAsync(source));
 }
 [Fact] public async Task DuplicateOutputsNumbered(){var source=PathFor("original.png");using(var image=new Image<Rgba32>(20,20))await image.SaveAsPngAsync(source);var p=new ImageProcessor(new());var w=new Workflow{OutputFolder=PathFor("out")};var first=await p.ProcessAsync(w,source,1,new Dictionary<string,string>(),new(),default);var second=await p.ProcessAsync(w,source,1,new Dictionary<string,string>(),new(),default);Assert.NotEqual(first,second);Assert.True(File.Exists(first));}
 sealed class FakeProcessor:IItemProcessor{public Task<string> ProcessAsync(Workflow w,string input,int index,IReadOnlyDictionary<string,string> variables,PauseGate pause,CancellationToken token)=>input=="bad"?throw new InvalidDataException("Unsupported input"):Task.FromResult(input+"-out");}
 [Fact] public async Task FailureIsolationAndHistory(){var store=NewStore();var run=await new BatchRunner(store,new FakeProcessor()).RunAsync(new(Guid.NewGuid(),new(),["one","bad","three"]));Assert.Equal(2,run.Items.Count(x=>x.Status==ItemStatus.Completed));Assert.Single(run.Items,x=>x.Status==ItemStatus.Failed);Assert.Single(store.List<RunRecord>("run"));Assert.Empty(store.List<Draft>("draft"));}
 [Fact] public async Task SaveAndRunContinuesImmediately(){var store=NewStore();var draft=new Draft(Guid.NewGuid(),new(){Name="Unsaved"},["one"]);var run=await new BatchRunner(store,new FakeProcessor()).RunAsync(draft);Assert.Equal(ItemStatus.Completed,run.Items.Single().Status);Assert.Single(store.List<Workflow>("workflow"));}
 [Fact] public void ClearHistoryPreservesOutput(){var output=PathFor("result.txt");File.WriteAllText(output,"Keep me");var store=NewStore();store.Save("run","1",1,new RunRecord(Guid.NewGuid(),Guid.NewGuid(),1,"test",DateTimeOffset.Now,DateTimeOffset.Now,[new("source",output,ItemStatus.Completed)]));store.Delete("run");Assert.True(File.Exists(output));}
}
