$ErrorActionPreference='Stop'
$installer=Resolve-Path 'artifacts/installer/AGAIN-Setup-v0.2.0.exe'
$destination=Join-Path $env:RUNNER_TEMP 'AGAIN-installed-test'
$process=Start-Process $installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=`"$destination`"") -Wait -PassThru
if($process.ExitCode -ne 0){throw "Installer failed: $($process.ExitCode)"}
$exe=Join-Path $destination 'AGAIN.exe'
if(!(Test-Path $exe)){throw 'Installed executable is missing.'}
& "$PSScriptRoot/WindowsSmoke.ps1" -Executable $exe
$uninstaller=Join-Path $destination 'unins000.exe'
$remove=Start-Process $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
if($remove.ExitCode -ne 0 -or (Test-Path $exe)){throw 'Uninstall did not remove the installed executable.'}
Write-Output 'PASS: silent installer, installed application launch, and uninstall on Windows CI.'
