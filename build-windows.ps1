$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host "AGAIN v0.1.0 - Windows build"

dotnet restore Again.sln
dotnet build Again.sln -c Release --no-restore
dotnet run --project tests/Again.SmokeTests/Again.SmokeTests.csproj -c Release --no-build

$publish = Join-Path $root "artifacts/publish"
$installer = Join-Path $root "artifacts/installer"
New-Item -ItemType Directory -Force -Path $publish, $installer | Out-Null

dotnet publish src/Again.App/Again.App.csproj -c Release -r win-x64 --self-contained true -o $publish

$isccCandidates = @(
  "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
  & $iscc "installer/Again.iss"
  Write-Host "Installer created under artifacts/installer"
} else {
  Write-Warning "Inno Setup 6 not found. The self-contained app was published under artifacts/publish."
}
