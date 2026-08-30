# Build status — AGAIN v0.1.0

## Completed in this source package

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
- XML validation of project/XAML/manifest files and source delimiter checks.

## Compilation status

This source package has **not** been claimed as compiled. The original authoring environment was Linux and could not perform the required WPF/Windows compile locally.

This repository includes a Windows GitHub Actions workflow that performs the actual .NET 8 build, smoke tests, self-contained win-x64 publish and Inno Setup packaging. Successful CI artifacts are the authoritative test builds.
