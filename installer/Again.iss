#define MyAppName "AGAIN"
#define MyAppVersion "0.1.1"
#define MyAppPublisher "6ixMedia SA"
#define MyAppURL "https://www.6ixmediasa.com"
#define MyAppExeName "Again.exe"
#define PublishDir "..\artifacts\publish"

[Setup]
AppId={{C56FFB9F-64F5-4E5E-976B-9CE2E6928BE2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\AGAIN
DefaultGroupName=AGAIN
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=AGAIN-Setup-v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=0.1.1.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=AGAIN - Do it once. Never do it twice.

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\AGAIN"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\AGAIN"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch AGAIN"; Flags: nowait postinstall skipifsilent
