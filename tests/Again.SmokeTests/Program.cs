using Again.Core;

var failures = new List<string>();

void Check(bool condition, string name)
{
    if (!condition) failures.Add(name);
}

Check(FilenameTemplateEngine.Infer("IMG_001", "Holiday 001") == "Holiday {number}", "numeric filename inference");
Check(FilenameTemplateEngine.Apply("Holiday {number}", "IMG_002", "002") == "Holiday 002", "numeric filename apply");
Check(FilenameTemplateEngine.Infer("photo", "edited-photo") == "edited-{stem}", "stem filename inference");
Check(FilenameTemplateEngine.Infer("Screenshot (261)", "test1") == "test{sequence}", "sequential filename inference");
Check(FilenameTemplateEngine.Apply("test{sequence}", "Screenshot (262)", null, 2) == "test2", "sequential filename apply");
Check(FilenameTemplateEngine.Apply("frame{sequence:3}", "anything", null, 7) == "frame007", "padded sequence apply");
Check(WorkflowDetector.TryGetFormat("x.jpeg") == ImageOutputFormat.Jpeg, "jpeg format");
Check(WorkflowDetector.TryGetFormat("x.webp") is null, "unsupported webp in v0.1.1");

var source = new ImageFileInfo(@"C:\Input\IMG_001.png", 2000, 1000, 100, DateTime.UtcNow);
var output = new ImageFileInfo(@"C:\Input\Edited\Holiday 001.jpg", 1200, 600, 80, DateTime.UtcNow);
var detected = WorkflowDetector.Detect(source, output);
Check(detected.Success, "workflow detection succeeds");
Check(detected.Workflow?.Resize.Width == 1200 && detected.Workflow.Resize.Height == 600, "target dimensions inferred");
Check(detected.Workflow?.Output.FilenameTemplate == "Holiday {number}", "output filename variable inferred");

var crop = new NormalizedCrop(0.05, 0.10, 0.90, 0.80);
var cropStep = new ImageResizeStep(1800, 800, crop, @"C:\overlay.png");
Check(cropStep.HasCrop, "crop step reports crop");
Check(cropStep.HasOverlay, "crop step reports overlay");

if (failures.Count > 0)
{
    Console.Error.WriteLine("AGAIN smoke tests failed:");
    foreach (var failure in failures) Console.Error.WriteLine(" - " + failure);
    Environment.Exit(1);
}

Console.WriteLine("AGAIN smoke tests passed.");
