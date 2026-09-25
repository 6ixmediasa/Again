using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
namespace Again.TestHost;
public static class Program
{
    [STAThread] public static void Main(string[] args) { var app = new Application(); var window = new Window { Title = "AGAIN Controlled Test Host", Width = 600, Height = 420 }; var panel = new StackPanel { Margin = new(30) }; var name = new TextBox { Margin = new(0, 10, 0, 20) }; AutomationProperties.SetName(name, "Project name"); AutomationProperties.SetAutomationId(name, "project-name"); var output = new TextBlock(); AutomationProperties.SetAutomationId(output, "result"); var button = new Button { Content = "Apply", Padding = new(20), Margin = new(0, 0, 0, 20) }; AutomationProperties.SetName(button, "Apply"); AutomationProperties.SetAutomationId(button, args.Length > 0 && args[0] == "--changed" ? "apply-repaired" : "apply"); button.Click += (s, e) => { output.Text = "Applied: " + name.Text; AutomationProperties.SetName(output, output.Text); }; panel.Children.Add(new TextBlock { Text = "AGAIN controlled application — accessible test controls" }); panel.Children.Add(name); panel.Children.Add(button); panel.Children.Add(output); window.Content = panel; app.Run(window); }
}
