using System.Runtime.InteropServices;
using System.Text;
namespace Again.Windows;
public static class Credentials
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Credential { public uint Flags, Type; public string TargetName, Comment; public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten; public uint BlobSize; public nint Blob; public uint Persist, AttributeCount; public nint Attributes; public string TargetAlias, UserName; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CredRead(string target, uint type, uint flags, out nint credential);
    [DllImport("advapi32.dll")] static extern void CredFree(nint buffer);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CredDelete(string target, uint type, uint flags);
    public static void StoreApproved(string name, string secret) { var bytes = Encoding.Unicode.GetBytes(secret); var memory = Marshal.AllocHGlobal(bytes.Length); try { Marshal.Copy(bytes, 0, memory, bytes.Length); var c = new Credential { Type = 1, TargetName = "AGAIN/" + name, Comment = "Approved AGAIN secret", BlobSize = (uint)bytes.Length, Blob = memory, Persist = 2, UserName = Environment.UserName }; if (!CredWrite(ref c, 0)) throw new System.ComponentModel.Win32Exception(); } finally { for (var i = 0; i < bytes.Length; i++) Marshal.WriteByte(memory, i, 0); Marshal.FreeHGlobal(memory); Array.Clear(bytes); } }
    public static string ReadApproved(string name) { if (!CredRead("AGAIN/" + name, 1, 0, out var pointer)) throw new InvalidOperationException("Approve and save this secret first."); try { var c = Marshal.PtrToStructure<Credential>(pointer); return Marshal.PtrToStringUni(c.Blob, (int)c.BlobSize / 2) ?? ""; } finally { CredFree(pointer); } }
    public static void Delete(string name) => CredDelete("AGAIN/" + name, 1, 0);
}
