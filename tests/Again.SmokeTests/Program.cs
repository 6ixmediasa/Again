using Again.Core;

var failures = new List<string>();

void Check(bool condition, string name)
{
    if (!condition) failures.Add(name);
}

Check(FilenameTemplateEngine.Infer("IMG_001", "Holiday 001") == "Holiday {number}", "numeric filename inference");
Check(FilenameTemplateEngine.Apply("Holiday {number}", "IMG_002", "002") == "Holiday 002", "numeric filename apply");
Check(FilenameTemplateEngine.Infer("photo", "edited-photo") == "edited-{stem}", "stem filename inference");
Check(WorkflowDetector.TryGetFormat("x.jpeg") == ImageOutputFormat.Jpeg, "jpeg format");
Check(WorkflowDetector.TryGetFormat("x.webp") is null, "unsupported webp in v0.1");

var source = new ImageFileInfo(@"C:\Input\IMG_001.png", 2000, 1000, 100, DateTime.UtcNow);
var output = new ImageFileInfo(@"C:\Input\Edited\Holiday 001.jpg", 1200, 600, 80, DateTime.UtcNow);
var detected = WorkflowDetector.Detect(source, output);
Check(detected.Success, "workflow detection succeeds");
Check(detected.Workflow?.Resize.Width == 1200 && detected.Workflow.Resize.Height == 600, "resize inferred");
Check(detected.Workflow?.Output.FilenameTemplate == "Holiday {number}", "output filename variable inferred");

if (failures.Count > 0)
{
    Console.Error.WriteLine("AGAIN smoke tests failed:");
    foreach (var failure in failures) Console.Error.WriteLine(" - " + failure);
    Environment.Exit(1);
}

Console.WriteLine("AGAIN smoke tests passed.");
