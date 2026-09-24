# Requirement-to-test matrix

No row marked partial satisfies the complete requested section. Source presence is not evidence of runtime verification.

| ID | Requirement | Status | Evidence and remaining work |
|---|---|---|---|
| 1 | Product vision | Partial | Working engines and desktop source; universal demonstration inference incomplete. |
| 2 | Hybrid execution | Partial | Native, UIA and browser adapters; tested template matcher; OCR and live visual adapter missing. |
| 3 | Technology | Partial | C# .NET 8 WPF SQLite ImageSharp Playwright; no full DI container, OpenCV/OCR, FFmpeg integration. |
| 4 | Navigation | Implemented; Windows verification pending | All nine pages and icon-only home control. |
| 5 | Home dashboard | Partial | Introduction, workflow cards, drafts and successful count; connected-app/recent-run cards incomplete. |
| 6 | Watch Me | Partial; Windows verification pending | Approved running apps, visible indicator, pause, sensitive mode, accessible click/value capture. No drag/drop, shortcut, timeline, clipboard or filesystem inference. |
| 7 | Detection and editor | Partial | Semantic control steps; edit/name/order/disable/delete/duplicate/add. No combine/split or high-level intent inference. |
| 8 | Variables | Partial; unit tested | Filename/date/time/number/folder and image dimensions, defaults/required validation; full typed per-run/per-item/secret UX incomplete. |
| 9 | Conditions | Partial; unit tested | File existence and orientation predicates. Full branching, app-state conditions and recovery action graph incomplete. |
| 10 | Connections | Partial | Running-app discovery and UIA test; capability/state catalog. Independent install/update/remove UI incomplete. |
| 11 | Photoshop | Partial; untested in Photoshop | Optional COM fixed scripting: open, crop, resize, text, rotate, PNG/JPG export. Advanced layer/action commands and real app tests missing. |
| 12 | Browser | Partial; Windows verification pending | Edge launch; role/label/text/placeholder click/type/upload/extract/download. Recording source added; profiles, multiple-page UI, tables and credential approval UI incomplete. |
| 13 | FL Studio | Partial | General accessible controls only. Full action/native/MIDI/visual profile not implemented. |
| 14 | CapCut | Partial | General accessible controls only. Timeline/captions/render/export profile not implemented. |
| 15 | Windows automation | Partial; Windows verification pending | Guarded unique semantic clicks/fields and relative coordinate fallback. Launch/dialog/shortcut/drag/clipboard workflows incomplete. |
| 16 | Quick Tools | Partial; image tests pass | Crop/resize/text/logo/rotate/flip/color/format; file copy/move/rename/duplicates/preview/undo. Sorting/replacement/case tools and media engines incomplete. |
| 17 | Image processing | Partial; integration tested | Proportional/ratio crop, no stretch resize, semantic text, font fallback, wrapping, relative logo. Face detection, text outline/rotation and all styling controls incomplete. |
| 18 | Preview | Partial | Shared image engine, next/previous/regenerate, dimensions/format, step review and approval. Before/after boundaries, conflict grid and complete variables UI incomplete. |
| 19 | Workflow saving | Partial; unit tested | Typed steps, variables, metadata, naming/version history; embedded demonstrations and thumbnail asset management incomplete. |
| 20 | Forgotten save | Core passed; UI pending | Draft edits autosaved; Save and Run saves and executes in core tests; dialog compiled but needs Windows interaction test. |
| 21 | Crash recovery | Core passed; UI pending | New Store instance restores draft actions/input/checkpoint; forced-process/window/preview restoration acceptance incomplete. |
| 22 | Batch | Partial; unit tested | Multiple files/folder/subfolders/paste/drop, pause/resume/cancel/retry failed. Complete compatibility table and all statuses/manual controls incomplete. |
| 23 | Output safety | Partial; image tests pass | Originals preserved; atomic validated images; duplicate numbering and no-overwrite. Full ask/replace UI and application output transaction coverage incomplete. |
| 24 | Workflow library | Partial | Search/order/favorites/open/duplicate/delete/import/export/version restore. Grid/list toggle/categories/archive UI and metrics incomplete. |
| 25 | History | Partial; persistence tested | Per-item statuses/errors/outputs, retry failed, rerun, reports, clear without output deletion. Full step trace and application metrics incomplete. |
| 26 | Repair | Partial | Retry/skip/stop/manual and hover retargeting implemented; end-to-end repair and learning manual corrections still unverified. |
| 27 | Errors | Partial | Friendly common file/target errors and redacted-log helper; exhaustive recovery actions/log wiring incomplete. |
| 28 | Settings | Partial | Output folder/theme/reduced motion persisted; delete records. Full listed settings and update/retention execution incomplete. |
| 29 | Privacy | Partial | Offline core/no account/no telemetry; bounded recording; password exclusion; typed imports/path checks. Comprehensive security review and complete credential UX incomplete. |
| 30 | Design | Partial; Windows visual inspection pending | Dark/light cards, typography, SVG/ICO, Windows 11 backdrop request. Full polish and actual glass visibility unverified. |
| 31 | Accessibility | Partial; Windows verification pending | Semantic control names, focus, scrollable/resizable UI, per-monitor DPI manifest. Screen reader/high contrast/DPI acceptance not run. |
| 32 | Installer/portable | First Windows build passed | Self-contained win-x64 publish and Inno per-user installer configured; first installer/portable built and packaged launch passed on Windows; clean install/uninstall still pending. |
| 33 | Repository | Implemented for present code | Solution/projects/source/tests/docs/installer/CI/license placeholder; complete requested product implementation still pending. |
| 34 | GitHub Actions | First Windows execution passed | Restore/build/test/publish/package/smoke/artifacts/checksums/draft tag release. Full integration suite incomplete. |
| 35 | Automated tests | Partial | 38 passing core/image/visual cases at checkpoint; not every requested category has a test. |
| 36 | Acceptance A–I | Incomplete | See ACCEPTANCE.md; no fabricated external-app or installer results. |
| 37 | Definition of done | NOT MET | Several required implementations and Windows/external-app acceptance gates remain. |
| 38 | Working method | In progress | Plan/matrix created, implementation/build/test fixes progressing; no permission checkpoint requested. |
| 39 | Final delivery | In progress | Build artifacts and actual evidence are delivered separately from unresolved feature scope. |
