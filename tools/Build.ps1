param([string]$Configuration='Release')
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
function Check-Exit { if ($LASTEXITCODE -ne 0) { throw "Build command failed with exit code $LASTEXITCODE" } }
dotnet restore Again.sln
Check-Exit
dotnet build Again.sln -c $Configuration --no-restore
Check-Exit
dotnet test tests/Again.Tests -c $Configuration --no-build --logger "trx;LogFileName=core.trx"
Check-Exit
dotnet test tests/Again.WindowsTests -c $Configuration --no-build --logger "trx;LogFileName=windows.trx"
Check-Exit
dotnet publish src/Again.App -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/publish
Check-Exit
New-Item artifacts/portable -ItemType Directory -Force | Out-Null
Compress-Archive -Path artifacts/publish/* -DestinationPath artifacts/portable/AGAIN-Windows-v0.2.0.zip -Force
$iscc="${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (!(Test-Path $iscc)) { throw 'Install Inno Setup 6 to compile the installer.' }
& $iscc installer/Again.iss
Check-Exit
$files=@('artifacts/portable/AGAIN-Windows-v0.2.0.zip','artifacts/installer/AGAIN-Setup-v0.2.0.exe')
$files | ForEach-Object { $hash=Get-FileHash $_ -Algorithm SHA256; "$($hash.Hash.ToLowerInvariant())  $(Split-Path $_ -Leaf)" } | Set-Content artifacts/SHA256SUMS.txt
