# Acceptance evidence

| Test | Status | Evidence / missing verification |
|---|---|---|
| A Photoshop | Not run | Real COM connector source exists; Photoshop unavailable. No Photoshop host equivalence claimed. |
| B Internal images | Partial pass | Pixel-level text and orientation tests pass for text-only, crop-only, crop/text and crop/text/logo cases; broader full visual review remains. |
| C Forgotten save | Core pass, UI pending | SaveAndRunContinuesImmediately and ForgottenSaveKeepsDemonstration pass. WPF dialog acceptance pending. |
| D Restart | Store pass, process test pending | New Store restores actions, inputs and progress. Forced Windows closure test pending. |
| E Windows recording/replay | Not run | Controlled WPF host included; interactive recorded workflow acceptance pending. |
| F Browser | Not run | Controlled page included; recording and layout-change acceptance pending. |
| G Repair | Not complete | Retarget/learn/continue end-to-end acceptance pending implementation. |
| H Failed-item isolation | Core pass | Fake processor isolation and real image corruption tests are separate; full retry UI acceptance pending. |
| I Installed app | Not run | CI configured to compile installer and verify packaged launch; clean install/uninstall acceptance still pending. |

The current implementation does not meet the production definition of done.
