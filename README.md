# AGAIN
**Do it once. Never do it twice.** · 6ixMedia SA

Greenfield Windows rebuild, version 0.2.0. **Not production-complete.** Source and verification status are tracked in [the requirement matrix](docs/REQUIREMENT-MATRIX.md). Do not treat a compiled package as evidence that all requested integrations work.

## Build on Windows
Install .NET 8 SDK and Inno Setup 6 on the build computer, then run:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build.ps1
powershell -ExecutionPolicy Bypass -File tools/WindowsSmoke.ps1
```

End users do not need these developer tools. The portable package includes .NET and the Playwright driver. Browser automation uses separately installed Microsoft Edge.

Outputs:
- `artifacts/installer/AGAIN-Setup-v0.2.0.exe`
- `artifacts/portable/AGAIN-Windows-v0.2.0.zip`
- `artifacts/SHA256SUMS.txt`

## Repository
- `src/Again.Core`: typed workflow model, SQLite store, batch runner, safety, file tools, visual matching.
- `src/Again.Imaging`: proportional crop, semantic text, logo, color adjustments, verified atomic exports.
- `src/Again.Windows`: optional Photoshop scripting, Playwright browser, Windows accessibility recorder/replay and credential storage.
- `src/Again.App`: WPF desktop UI, editor, preview, recovery and history.
- `tests/Again.Tests`: portable unit and image integration tests.
- `tests/Again.TestHost`: controlled accessible Windows application.
- `tests/browser-host`: controlled browser page.
- `tools`: build and packaged launch verification.
- `installer`: per-user Inno Setup installer.

## Documentation
[Architecture](docs/ARCHITECTURE.md) · [User guide](docs/USER-GUIDE.md) · [Connectors](docs/CONNECTORS.md) · [Troubleshooting](docs/TROUBLESHOOTING.md) · [Privacy](docs/PRIVACY.md)
