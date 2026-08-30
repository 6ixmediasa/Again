# Build status — AGAIN v0.1.0

## Verified V0.1 vertical slice

- Clean C#/.NET 8 solution and WPF application shell.
- Local demonstration session observer.
- Foreground process/window + safe UI Automation metadata observation.
- Image-file activity observation.
- Semantic resize/export detector based on demonstrated input/output.
- Variable filename inference for common source-number patterns.
- Direct Windows image replay engine (no coordinate macro playback).
- Output collision protection and no-source-overwrite rule.
- Output existence/size/dimension validation.
- Pause / Skip / Stop batch controls.
- System tray behavior.
- Local workflow/history persistence.
- Inno Setup installer definition.
- GitHub Actions Windows build workflow.
- Core smoke-test executable with no third-party test framework dependency.

## Compilation status

**COMPILED AND PACKAGED SUCCESSFULLY on Windows.**

Verified by GitHub Actions run `33336051248` on a Windows Server 2025 runner using .NET 8 SDK 8.0.424.

The verified pipeline completed all stages successfully:

1. Restore
2. Release build of the full solution
3. Core workflow smoke tests
4. Self-contained `win-x64` publish
5. Inno Setup installation
6. `AGAIN-Setup-v0.1.0.exe` packaging
7. Artifact upload

Verified artifact: `AGAIN-Windows-v0.1.0`

Installer SHA-256:
`a5d9fb2a1e41e6fce9b658a6e267d6c87f005f66b17bb3171b687df142e11c7d`

Full artifact ZIP SHA-256:
`e383706bc2b77fe70cd1f86c2720352d6f179a24d37e87e3413eacfdf4179473`

This confirms compilation and packaging. Real-world workflow behavior still requires field testing on a normal Windows desktop, which is the next V0.1 stage.
