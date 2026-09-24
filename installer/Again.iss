#define AppVersion "0.2.0"
[Setup]
AppId={{9A36231D-FA9C-4CD1-A91F-26DE99431101}
AppName=AGAIN
AppVersion={#AppVersion}
AppPublisher=6ixMedia SA
AppPublisherURL=https://www.6ixmediasa.com
DefaultDirName={localappdata}\Programs\AGAIN
DefaultGroupName=AGAIN
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=AGAIN-Setup-v{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\AGAIN.exe
SetupIconFile=..\assets\again.ico
VersionInfoVersion=0.2.0.0
VersionInfoCompany=6ixMedia SA
VersionInfoDescription=AGAIN — Do it once. Never do it twice.
CloseApplications=yes
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\AGAIN"; Filename: "{app}\AGAIN.exe"
Name: "{autodesktop}\AGAIN"; Filename: "{app}\AGAIN.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\AGAIN.exe"; Description: "Open AGAIN"; Flags: nowait postinstall skipifsilent
