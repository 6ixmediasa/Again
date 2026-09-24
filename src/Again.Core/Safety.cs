using System.Text.RegularExpressions;
namespace Again.Core;
public static partial class Safety {
 [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")] private static partial Regex InvalidName();
 [GeneratedRegex(@"\{([a-zA-Z0-9_-]+)\}")] private static partial Regex Token();
 public static string Filename(string value){var name=InvalidName().Replace(value,"_").Trim().TrimEnd('.');if(string.IsNullOrWhiteSpace(name)||name is "." or "..")throw new InvalidDataException("Choose a valid output name.");if(Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",RegexOptions.IgnoreCase))name="_"+name;return name.Length>180?name[..180]:name;}
 public static string Under(string root,string relative){if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(x=>x==".."))throw new InvalidDataException("The output must stay inside its selected folder.");var fullRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;var full=Path.GetFullPath(Path.Combine(fullRoot,relative));if(!full.StartsWith(fullRoot,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new InvalidDataException("Unsafe output path.");return full;}
 public static string Expand(string input,IReadOnlyDictionary<string,string> values)=>Token().Replace(input,m=>values.TryGetValue(m.Groups[1].Value,out var v)?v:throw new InvalidDataException($"Provide a value for {m.Groups[1].Value}."));
 public static Dictionary<string,string> Values(string file,int index)=>new(){["original-name"]=Path.GetFileNameWithoutExtension(file),["extension"]=Path.GetExtension(file).TrimStart('.'),["number"]=index.ToString("D3"),["date"]=DateTime.Now.ToString("yyyy-MM-dd"),["time"]=DateTime.Now.ToString("HH-mm-ss"),["folder"]=Path.GetDirectoryName(file)??"",["project-name"]=Path.GetFileNameWithoutExtension(file)};
 public static string Redact(string text,IEnumerable<string> secrets){foreach(var secret in secrets.Where(x=>!string.IsNullOrEmpty(x)).OrderByDescending(x=>x.Length))text=text.Replace(secret,"[REDACTED]",StringComparison.Ordinal);return text;}
 public static void VerifyOutput(string path){if(!File.Exists(path))throw new IOException("The application did not create the expected output.");using var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(stream.Length==0)throw new IOException("The exported file is empty.");}
 public static bool SameDomain(string expected,string actual)=>Uri.TryCreate(expected,UriKind.Absolute,out var a)&&Uri.TryCreate(actual,UriKind.Absolute,out var b)&&a.Scheme=="https"&&b.Scheme=="https"&&string.Equals(a.IdnHost,b.IdnHost,StringComparison.OrdinalIgnoreCase)&&a.Port==b.Port;
}
public static class Conditions {
 public static bool Evaluate(Predicate? p, IReadOnlyDictionary<string,string> values){if(p is null)return true;var v=Safety.Expand(p.Value,values);bool result=p.Kind switch{"File exists"=>File.Exists(v),"File does not exist"=>!File.Exists(v),"Portrait"=>Number(values,"height")>Number(values,"width"),"Landscape"=>Number(values,"width")>Number(values,"height"),"Square"=>Number(values,"height")==Number(values,"width"),_=>throw new InvalidDataException("This condition needs an application connector: "+p.Kind)};return p.Negate?!result:result;}
 static double Number(IReadOnlyDictionary<string,string> values,string key)=>double.Parse(values[key],System.Globalization.CultureInfo.InvariantCulture);
}
public sealed class PauseGate {
 private TaskCompletionSource _resume=Ready(); private static TaskCompletionSource Ready(){var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);t.SetResult();return t;}
 public bool Paused=>!_resume.Task.IsCompleted;
 public void Pause(){if(!Paused)Interlocked.Exchange(ref _resume,new(TaskCreationOptions.RunContinuationsAsynchronously));}
 public void Resume()=>_resume.TrySetResult();
 public Task WaitAsync(CancellationToken token)=>_resume.Task.WaitAsync(token);
}
