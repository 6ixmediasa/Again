using System.Runtime.InteropServices;
using System.Diagnostics;
namespace Again.Windows;
internal static class Native {
 [StructLayout(LayoutKind.Sequential)] public struct Rect{public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] public struct Point{public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] public struct MouseHook{public Point Point;public uint Data,Flags,Time;public nint Extra;}
 public delegate nint HookProc(int code,nint wParam,nint lParam);
 [DllImport("user32.dll")]public static extern nint GetForegroundWindow();
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(nint h,out uint process);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(nint h,out Rect rect);
 [DllImport("user32.dll")]public static extern bool SetForegroundWindow(nint h);
 [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern nint SetWindowsHookEx(int id,HookProc callback,nint module,uint thread);
 [DllImport("user32.dll")]public static extern bool UnhookWindowsHookEx(nint hook);
 [DllImport("user32.dll")]public static extern nint CallNextHookEx(nint hook,int code,nint wParam,nint lParam);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]public static extern nint GetModuleHandle(string? name);
 [DllImport("user32.dll")]public static extern void mouse_event(uint flags,uint x,uint y,uint data,nuint extra);
 public static string ProcessPath(nint window){GetWindowThreadProcessId(window,out var id);using var p=Process.GetProcessById((int)id);return p.MainModule?.FileName??"";}
}
