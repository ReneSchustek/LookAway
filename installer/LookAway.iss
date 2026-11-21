; Inno-Setup-Skript für LookAway — erzeugt eine Setup.exe aus dem
; self-contained Publish (dist\setup-publish). Installiert für alle Benutzer unter
; C:\Program Files und braucht dafür Adminrechte; die App lebt im Tray und verwaltet
; ihren Autostart danach selbst.
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
; Überschreibbar (/DPublishDir=...), damit die Auslieferung aus einem eigenen
; Zwischenordner bauen kann. Ohne die Guard-Klammer wäre der Pfad fest verdrahtet
; und ein zweiter Aufrufer müsste die Datei ändern.
#ifndef PublishDir
  #define PublishDir "..\dist\setup-publish"
#endif

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
; Mit Adminrechten löst {autopf} auf C:\Program Files auf. Programme gehören dorthin und nicht
; ins Benutzerprofil: Dort kann sie jedes Programm des Benutzers verändern. Der Pfad bleibt frei
; wählbar.
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Verzeichnis-Auswahlseite immer anzeigen — Speicherort ist frei wählbar.
DisableDirPage=no
AllowUNCPath=no
; Immer mit Adminrechten, ohne Wahl „nur für mich“: sonst landete eine Installation wieder im
; Profil. Das Setup erhöht seine Rechte selbst; so kennt es den ursprünglichen Benutzer und kann
; LookAway mit dessen Rechten beenden und neu starten.
PrivilegesRequired=admin
; Der Autostart ist bewusst ein Eintrag je Benutzer (HKCU\...\Run), auch bei einer Installation
; für alle: Ob LookAway mit Windows startet, entscheidet jeder selbst.
UsedUserAreasWarning=no
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

[InstallDelete]
; Bis 1.4 lag die statische Roboto in drei Schnitten bei, mit Apache-Lizenz. Seit dem Wechsel auf
; Roboto Flex und Roboto Mono braucht die Oberfläche sie nicht mehr; ohne das blieben sie bei einem
; Update über eine ältere Installation liegen.
Type: files; Name: "{app}\Assets\Fonts\Roboto-Regular.ttf"
Type: files; Name: "{app}\Assets\Fonts\Roboto-Medium.ttf"
Type: files; Name: "{app}\Assets\Fonts\Roboto-Bold.ttf"
Type: files; Name: "{app}\Assets\Fonts\LICENSE.txt"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optionaler Autostart pro Benutzer (HKCU\...\Run). Die App hält den Eintrag
; danach selbst aktuell; bei Deinstallation wird er entfernt. Bei einem Update
; entscheidet nicht die Vorgabe der Aufgabe, sondern der bisherige Zustand (siehe
; WantsAutostart): Die App liest den Eintrag als führende Quelle und übernähme
; sonst einen Autostart, den der Benutzer abgeschaltet hatte.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Check: WantsAutostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
; Nach einem Update aus LookAway heraus (still, mit /RELAUNCH) startet LookAway wieder —
; mit den Rechten des Benutzers, nicht mit denen des Setups.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait runasoriginaluser; Check: WantsRelaunch

[Code]
const
  // Dieselben Werte stehen in LookAway.Core.Domain.InstallerArguments (/RELAUNCH) und in der
  // Einzelinstanz-Sperre LookAway.Data.Services.SingleInstanceLock (Mutex und Ereignis). Wer
  // einen ändert, ändert beide Seiten.
  RelaunchSwitch = '/RELAUNCH';
  EVENT_MODIFY_STATE = $0002;
  // Die Instanz sichert beim Beenden Countdown und Protokoll; das dauert Sekundenbruchteile.
  // Die Grenze fängt nur eine hängende Instanz ab.
  QuitWaitMilliseconds = 30000;
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{6E2D9F1A-7C4B-4E2A-9B57-2C8E5A0F1D34}_is1';

var
  HadAutostart: Boolean;
  IsUpgrade: Boolean;

// Name des Mutex, den eine laufende LookAway-Instanz hält.
function InstanceMutex: String;
begin
  Result := 'Local\LookAway-' + GetUserNameString;
end;

function OpenEvent(DesiredAccess: DWORD; InheritHandle: BOOL; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Event: THandle): BOOL;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';

// Bittet eine laufende Instanz, sich wie über das Tray-Menü zu beenden, und wartet darauf. Das
// Ereignis legt erst LookAway 1.5 an; eine ältere Instanz hat keins und läuft weiter.
procedure QuitRunningInstance;
var
  QuitEvent: THandle;
  Waited: Integer;
begin
  QuitEvent := OpenEvent(EVENT_MODIFY_STATE, False, 'Local\LookAway-Quit-' + GetUserNameString);
  if QuitEvent = 0 then
    exit;
  SetEvent(QuitEvent);
  CloseHandle(QuitEvent);

  Waited := 0;
  while CheckForMutexes(InstanceMutex) and (Waited < QuitWaitMilliseconds) do
  begin
    Sleep(250);
    Waited := Waited + 250;
  end;
end;

// Eine frühere Installation im Benutzerprofil (bis 1.4 die Vorgabe) samt Ort und Deinstallation.
function FindProfileInstallation(var UninstallString, Location: String): Boolean;
begin
  Result := RegQueryStringValue(HKCU, UninstallKey, 'UninstallString', UninstallString)
    and RegQueryStringValue(HKCU, UninstallKey, 'InstallLocation', Location);
end;

function InitializeSetup: Boolean;
begin
  // Vor jeder Änderung festhalten: Die Deinstallation der Profil-Installation löscht den
  // Autostart-Eintrag, und das Update soll den Zustand des Benutzers behalten.
  HadAutostart := RegValueExists(HKCU, RunKey, '{#MyAppName}');
  IsUpgrade := RegKeyExists(HKLM, UninstallKey) or RegKeyExists(HKCU, UninstallKey);
  Result := True;
end;

function WantsAutostart: Boolean;
begin
  if IsUpgrade then
    Result := HadAutostart
  else
    Result := WizardIsTaskSelected('autostart');
end;

function WantsRelaunch: Boolean;
begin
  Result := WizardSilent and (Pos(RelaunchSwitch, Uppercase(GetCmdTail)) > 0);
end;

// Der Deinstaller entfernt nur, was das Setup einst selbst kopiert hat. Was spätere Updates per
// Datei-Tausch hinzugefügt haben, bliebe liegen. Gelöscht wird nur ein Ordner, der „LookAway“
// heißt und keine Programmdatei mehr enthält — ein frei gewählter Sammelordner bleibt unberührt.
procedure RemoveProfileLeftovers(const Location: String);
var
  Directory: String;
begin
  Directory := RemoveBackslash(Location);
  if (CompareText(ExtractFileName(Directory), '{#MyAppName}') = 0)
    and DirExists(Directory)
    and not FileExists(AddBackslash(Directory) + '{#MyAppExeName}') then
    DelTree(Directory, True, True, True);
end;

// Vor dem Kopieren: laufende Instanzen beenden und eine Installation im Benutzerprofil
// still entfernen. Einstellungen und Verlauf liegen unter %APPDATA% und bleiben unberührt.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  UninstallString, Location: String;
  HasProfileInstallation: Boolean;
  ResultCode: Integer;
begin
  Result := '';
  HasProfileInstallation := FindProfileInstallation(UninstallString, Location);

  QuitRunningInstance;

  // Eine Instanz vor 1.5 hört nicht auf das Ereignis; dann bleibt nur, den Benutzer zu bitten.
  if CheckForMutexes(InstanceMutex) then
  begin
    Result := 'LookAway läuft noch. Bitte LookAway über das Symbol im Infobereich beenden und das Setup erneut starten.';
    exit;
  end;

  if HasProfileInstallation then
  begin
    Exec(RemoveQuotes(UninstallString), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    RemoveProfileLeftovers(Location);
  end;
end;
