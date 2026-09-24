param([string]$Executable='artifacts/publish/AGAIN.exe')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$process=Start-Process -FilePath (Resolve-Path $Executable) -PassThru
try {
 $deadline=(Get-Date).AddSeconds(30)
 do { Start-Sleep -Milliseconds 250; $process.Refresh(); if ($process.HasExited) { throw "AGAIN exited during launch: $($process.ExitCode)" } } until ($process.MainWindowHandle -ne 0 -or (Get-Date) -gt $deadline)
 if ($process.MainWindowHandle -eq 0) { throw 'AGAIN did not create a desktop window within 30 seconds.' }
 $root=[System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
 if ($root.Current.Name -notlike 'AGAIN*') { throw 'Unexpected main window title.' }
 foreach($name in @('AGAIN Home','Watch Me','New Workflow','My Workflows','Run History','Applications','Quick Tools','Settings','Help')) {
  $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$name)
  $control=$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
  if ($null -eq $control) { throw "Navigation control missing: $name" }
 }
 Write-Output 'PASS: packaged process launches and all nine navigation controls are accessible.'
} finally {
 if (!$process.HasExited) { [void]$process.CloseMainWindow(); if (!$process.WaitForExit(5000)) { $process.Kill() } }
}
