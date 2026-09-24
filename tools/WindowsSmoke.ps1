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
 Add-Type -AssemblyName System.Drawing
 $rect=$root.Current.BoundingRectangle
 $bitmap=New-Object System.Drawing.Bitmap([int]$rect.Width,[int]$rect.Height)
 $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
 $graphics.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bitmap.Size)
 New-Item artifacts/evidence -ItemType Directory -Force | Out-Null
 $bitmap.Save((Join-Path (Get-Location) 'artifacts/evidence/AGAIN-Windows-home.png'))
 $graphics.Dispose();$bitmap.Dispose()
 Write-Output 'PASS: packaged process launches and all nine navigation controls are accessible.'
} finally {
 if (!$process.HasExited) { [void]$process.CloseMainWindow(); if (!$process.WaitForExit(5000)) { $process.Kill() } }
}
