; Inno-Setup-Skript für LookAway — erzeugt eine Setup.exe aus dem
; self-contained Publish (dist\setup-publish). Per-User-Installation ohne
; Adminrechte; die App lebt im Tray und verwaltet Autostart selbst.
;
; Build (vom Repo-Root):
;   tools\publish-setup.ps1            -> publiziert + kompiliert die Setup.exe
; oder direkt:
;   ISCC.exe /DMyAppVersion=1.1.1 installer\LookAway.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.1.1"
#endif
#define MyAppName "LookAway"
#define MyAppPublisher "Rene Schustek"
#define MyAppURL "https://github.com/ReneSchustek/LookAway"
#define MyAppExeName "LookAway.exe"
#define PublishDir "..\dist\setup-publish"

[Setup]
; Stabile AppId (nicht ändern — identifiziert Installation für Upgrade/Deinstallation).
AppId={{6E2D9F1A-7C4B-4E2A-9B57-2C8E5A0F1D34}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
; {autopf} passt sich der Installationsart an: Programme (alle Benutzer) oder
; %LOCALAPPDATA%\Programs (nur ich). Der Benutzer kann den Pfad frei ändern.
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Verzeichnis-Auswahlseite immer anzeigen — Speicherort ist frei wählbar.
DisableDirPage=no
AllowUNCPath=no
; Standard ohne Adminrechte (nur ich); der Benutzer kann im Dialog auf
; "für alle Benutzer" wechseln (dann UAC-Elevation für Programme-Ordner).
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=LookAway-Setup-v{#MyAppVersion}
SetupIconFile=setup-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "LookAway automatisch mit Windows starten"; GroupDescription: "Autostart:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optionaler Autostart pro Benutzer (HKCU\...\Run). Die App hält den Eintrag
; danach selbst aktuell; bei Deinstallation wird er entfernt.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
