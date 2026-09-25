# Privacy
Core processing is local and requires no account. No telemetry or cloud uploads are implemented. Browser workflows intentionally access user-selected sites and may upload files only through configured upload steps.

The Windows recorder installs hooks only while a visible Watch Me session is active. It observes only approved process paths. Text capture is opt-in, password controls are excluded, and sensitive mode suspends capture. No screen capture/OCR is currently integrated.

Workflows, demonstration file paths, non-sensitive field text, drafts and history are stored under `%LOCALAPPDATA%\6ixMediaSA\AGAIN`. Workflow exports can contain private paths and ordinary text; review before sharing. The credential helper uses Windows Credential Manager; automatic credential-entry UX is not yet wired.

Delete local workflow data in Settings clears app records and preserves generated files. Uninstall preserves local drafts by default to avoid deleting unfinished work. Delete records before uninstall if desired. Structured log events exclude text, paths, URLs and exception messages by design; the logging helper is not yet wired into every execution path.
