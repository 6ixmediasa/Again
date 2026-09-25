# Connectors
## Current installation
Windows accessibility and image processing are included. Photoshop is optional: install licensed Photoshop with its Windows scripting/COM registration. The bridge probes `Photoshop.Application`; no external plugin or arbitrary script file is imported. Open Photoshop before testing. Supported commands are open, proportional crop/resize, add text, rotate and PNG/JPG export. Remaining requested layer/action commands are incomplete.

Browser uses installed Microsoft Edge through the bundled Playwright driver in a separate session. Approved profile reuse and secure credential-entry UX remain incomplete. Scoped browser recording captures ordinary fields and semantic clicks on the approved origin. Password fields stop for manual entry.

FL Studio and CapCut currently have general accessibility capability declarations, not full native or timeline profiles. Do not assume invisible controls can be automated.

## Implementing future connectors
Implement `IConnector` with a stable ID and explicit supported method/operation set. Register its capabilities in `ConnectorCatalog` and inject it into `ApplicationProcessor`. Accept typed workflow values only. Never execute arbitrary code, shell commands or plugins from workflow imports. Verify app identity before effects, honor cancellation between operations, verify outputs, and return understandable repairable errors. Build a controlled host and real-application qualification tests. Dynamic connector package installation/update is not implemented yet; changes currently ship in an app build.
