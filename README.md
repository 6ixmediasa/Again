# AGAIN v0.1.0

**Do it once. Never do it twice.**

AGAIN is a Windows-first local automation product by 6ixMedia SA. V0.1 proves one narrow but real vertical slice: demonstrate an image resize/export once in Microsoft Paint, then have AGAIN infer the resulting image transformation and apply that intent to the rest of a selected batch without replaying Paint mouse coordinates.

## What this build is designed to do

1. Select 2 or more JPG/PNG/BMP/TIFF/GIF images in AGAIN.
2. Click **WATCH ME**. AGAIN opens the first image in Paint and begins a local demonstration session.
3. Resize the image in Paint, then Save As/export it.
4. Save the demonstration output in the source folder or a normal Desktop/Documents/Pictures/Downloads location.
5. Return to AGAIN and click **AGAIN**.
6. AGAIN compares the original demonstration image with the produced image, infers target dimensions, output format, destination folder and a safe filename rule.
7. AGAIN processes the remaining selected images directly with Windows imaging APIs.
8. Every output is validated for file existence, non-zero length and expected pixel dimensions. One unexpected failure stops the remaining batch.

This deliberately demonstrates **repetition of intent**, not coordinate macro replay.

## V0.1 safety/privacy rules

- Local-only. No cloud calls are required by the application.
- No screenshots are captured.
- No typed text or edit-field values are recorded.
- Foreground observation stores process/window metadata and safe non-text UI control metadata only.
- Known credential/password-manager processes are excluded from observation by default.
- Replay never overwrites an input file. An in-place demonstration is normalized to an `AGAIN Results` folder.
- Existing outputs are collision-safe (`name (2).jpg`, etc.).
- Batch execution supports Pause, Skip and Stop.
- A validation/processing failure stops later items.

## Deliberate V0.1 limits

- Windows only.
- English Microsoft Paint is the intended first demonstration application, although the result-based detector can sometimes infer a supported resize/export from another editor.
- The demonstration output must be observable in the selected source folder, Desktop, Documents, Pictures or Downloads.
- Only resize + image-format export is normalized in this first vertical slice.
- WebP/AVIF are not encoded in V0.1.
- Crop, rotate, filters, drawing, text overlays and Photoshop/Premiere workflows are not claimed as supported.
- EXIF orientation normalization is not yet implemented.
- UI Automation observation is present as architecture scaffolding; V0.1 replay intentionally uses the more reliable Windows image transformation path once intent is inferred.

## Technology

- C# / .NET 8
- WPF
- Win32 foreground-window observation
- Windows UI Automation metadata
- FileSystemWatcher
- Windows/WPF imaging codecs
- Local JSON persistence under `%LOCALAPPDATA%\6ixMedia SA\AGAIN\state.json`
- Inno Setup packaging

JSON persistence is intentionally used instead of adding a database dependency in the first vertical slice. The persistence boundary can move to SQLite later without changing workflow semantics.

## Build on Windows

```powershell
./build-windows.ps1
```

The script runs smoke tests, publishes a self-contained x64 application, and—when Inno Setup 6 is installed—creates `artifacts/installer/AGAIN-Setup-v0.1.0.exe`.

GitHub Actions is also configured in `.github/workflows/windows-build.yml` to perform the same Windows compilation and installer packaging.

## Test scenario

Use copies of images for the first field test.

Example:

- Select `IMG_001.png` through `IMG_010.png`.
- WATCH ME opens `IMG_001.png` in Paint.
- Resize it to `1200 × 800`.
- Save As `Edited/Holiday 001.jpg`.
- Click AGAIN.

Expected normalized output naming includes:

- `Holiday 002.jpg`
- `Holiday 003.jpg`
- …

and every output should be exactly `1200 × 800`.

## Branding

AGAIN — “Do it once. Never do it twice.”

Crafted with <3 by 6ixMedia SA — https://www.6ixmediasa.com
