using System.Windows;
namespace Again.App;
public partial class App:Application {
 protected override void OnStartup(StartupEventArgs e){base.OnStartup(e);DispatcherUnhandledException+=(s,args)=>{MessageBox.Show(Again.Core.UserErrors.Describe(args.Exception),"AGAIN · Needs your attention",MessageBoxButton.OK,MessageBoxImage.Warning);args.Handled=true;};}
}
