using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Threading;
using Again.Core;
namespace Again.Windows;
public sealed class Recorder:IDisposable {
 readonly Dispatcher dispatcher;readonly Action<Step> captured;readonly HashSet<string> approved;
 Native.HookProc? callback;nint hook;AutomationElement? field;AutomationPropertyChangedEventHandler? valueHandler;AutomationFocusChangedEventHandler? focusHandler;
 public bool Active{get;private set;}public bool Paused{get;set;}public bool Sensitive{get;set;}public bool CaptureText{get;set;}
 public Recorder(Dispatcher dispatcher,IEnumerable<string> approved,Action<Step> captured){this.dispatcher=dispatcher;this.approved=new(approved,StringComparer.OrdinalIgnoreCase);this.captured=captured;}
 public void Start(){if(Active)return;if(approved.Count==0)throw new InvalidDataException("Select at least one application to watch.");callback=OnMouse;hook=Native.SetWindowsHookEx(14,callback,Native.GetModuleHandle(null),0);if(hook==0)throw new InvalidOperationException("Windows could not start the recorder.");Active=true;focusHandler=OnFocus;Automation.AddAutomationFocusChangedEventHandler(focusHandler);}
 bool Allowed(AutomationElement e){try{return Active&&!Paused&&!Sensitive&&!e.Current.IsPassword&&approved.Contains(System.Diagnostics.Process.GetProcessById(e.Current.ProcessId).MainModule?.FileName??"");}catch{return false;}}
 nint OnMouse(int code,nint message,nint data){if(code>=0&&message==(nint)0x0201&&Active&&!Paused&&!Sensitive){var p=Marshal.PtrToStructure<Native.MouseHook>(data).Point;dispatcher.BeginInvoke(()=>CaptureClick(p));}return Native.CallNextHookEx(hook,code,message,data);}
 void CaptureClick(Native.Point point){try{var e=AutomationElement.FromPoint(new System.Windows.Point(point.X,point.Y));if(!Allowed(e))return;var target=WindowsConnector.Describe(e);if(target.AutomationId==""&&target.Name=="")return;captured(new(){Name="Click "+(target.Name!=""?target.Name:target.AutomationId),Operation=Operation.Click,Method=Method.UIAutomation,Target=target});}catch(ElementNotAvailableException){}catch(System.ComponentModel.Win32Exception){}}
 void OnFocus(object sender,AutomationFocusChangedEventArgs args){if(field is not null&&valueHandler is not null){try{Automation.RemoveAutomationPropertyChangedEventHandler(field,valueHandler);}catch(ElementNotAvailableException){}field=null;}
  if(!CaptureText||sender is not AutomationElement e||!Allowed(e))return;field=e;valueHandler=(source,change)=>{if(source is not AutomationElement control||!CaptureText||!Allowed(control))return;var text=change.NewValue as string;if(text is null)return;try{var target=WindowsConnector.Describe(control);dispatcher.BeginInvoke(()=>{if(Allowed(control))captured(new(){Name="Enter text in "+target.Name,Operation=Operation.Type,Method=Method.UIAutomation,Target=target,Args=new(){["text"]=text}});});}catch(ElementNotAvailableException){}};
  Automation.AddAutomationPropertyChangedEventHandler(e,TreeScope.Element,valueHandler,ValuePattern.ValueProperty);
 }
 public void Dispose(){Active=false;if(hook!=0){Native.UnhookWindowsHookEx(hook);hook=0;}if(focusHandler is not null)Automation.RemoveAutomationFocusChangedEventHandler(focusHandler);if(field is not null&&valueHandler is not null)try{Automation.RemoveAutomationPropertyChangedEventHandler(field,valueHandler);}catch(ElementNotAvailableException){}field=null;callback=null;}
}
