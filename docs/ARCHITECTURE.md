# Architecture
The model stores semantic `Operation`, execution `Method`, reliability, target and argument values independently of UI controls. `WorkflowJson` uses a closed enum and rejects unknown members, future schemas and embedded scripts/passwords. Existing schema 1 files are read directly; no legacy migration is claimed.

SQLite uses WAL and synchronous FULL transactions. Workflow versions are append-only by version key; drafts update their own record after edits and before/after batch items. A crash between export and checkpoint may leave an already-created output; numbered-copy mode preserves it on rerun. Exactly-once external effects are not guaranteed.

`IItemProcessor` owns an item and `BatchRunner` owns isolation, pause/cancel, history and checkpoints. The image engine is shared by preview and export. Cropping removes pixels; resize selects fit or fill and never uses stretch mode. Every text step re-renders a semantic text specification on the current image. Exports use a same-directory temporary file and publish only after image verification.

`IConnector` separates application operations from orchestration. Windows semantic replay requires exact process path, expected active window, unique enabled visible control and compatible pattern. Ambiguity causes a safe failure. Browser actions use Playwright semantic locators and exact approved HTTPS origin checks. Photoshop uses its optional Windows automation object with fixed generated commands, never arbitrary imported script text.

The current UI has a workspace view model and view-owned page construction; it is not a full MVVM command/data-template implementation. The visual correlation library is implemented independently and tested, but screen recognition/OCR integration is incomplete. See the matrix for gaps.

Official references: https://learn.microsoft.com/dotnet/desktop/wpf/ and https://developer.adobe.com/photoshop/ . Real application support must be qualified against the installed version.
