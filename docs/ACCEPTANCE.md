# Acceptance evidence

| Test | Status | Evidence / missing verification |
|---|---|---|
| A Photoshop | Not run | Real COM connector source exists; Photoshop unavailable. No Photoshop host equivalence claimed. |
| B Internal images | Partial pass | Pixel-level text and orientation tests pass for text-only, crop-only, crop/text and crop/text/logo cases; broader full visual review remains. |
| C Forgotten save | Core pass, UI pending | SaveAndRunContinuesImmediately and ForgottenSaveKeepsDemonstration pass. WPF dialog acceptance pending. |
| D Restart | Store pass, process test pending | New Store restores actions, inputs and progress. Forced Windows closure test pending. |
| E Windows recording/replay | Partial pass on Windows CI | Semantic replay against the controlled WPF host survived movement/resizing; wrong process rejected. Full mouse-hook recording acceptance pending. |
| F Browser | Partial pass on Windows CI | Recorded labelled fields and role-based clicks; verified downloaded contents; rejected unexpected origin. Layout-change/multi-page acceptance pending. |
| G Repair | Not complete | Retarget/learn/continue end-to-end acceptance pending implementation. |
| H Failed-item isolation | Core pass | Real corrupt-image batch isolation passed locally; full retry UI acceptance pending. |
| I Installed app | Not run | Installer compiled and packaged launch passed on Windows CI; installation/uninstall check added for the next run. |

The current implementation does not meet the production definition of done.
