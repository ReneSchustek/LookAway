# LookAway

[![CI](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml)

**Deutsch** · [English](DEVELOPMENT.en.md)

Eine schlanke Windows-Tray-Anwendung, die dezent an Bildschirmpausen erinnert. Mehrere wissenschaftlich fundierte Pausenmodelle, dreisprachig (Deutsch, Englisch, Französisch), und vollständig konfigurierbar pro Windows-Benutzer.

## Voraussetzungen

- Windows 10 1809 oder neuer / Windows 11
- .NET 10 SDK (für den Build)
- Windows App SDK (für WinUI 3)

## Solution-Struktur

```
LookAway/
├── src/
│   ├── LookAway.App/             WinUI 3 App, Views, ViewModels, Services
│   ├── LookAway.Application/     Use Cases, DTOs, Application Services
│   ├── LookAway.Core/            Entities, Value Objects, Interfaces, Enums
│   └── LookAway.Data/            Repositories, Persistenz, externe Zugriffe
├── tests/
│   ├── LookAway.Tests.Unit/         xUnit, isolierte Domain-Tests
│   └── LookAway.Tests.Integration/  xUnit, echtes Dateisystem / Registry
├── Directory.Build.props         Globale Build-Einstellungen
├── .editorconfig                 Code-Style-Konventionen
└── LookAway.slnx                 Solution-Datei
```

## Build

```bash
dotnet restore
dotnet build --configuration Release
```

## Tests

```bash
dotnet test
```

## Entwicklung

```bash
dotnet run --project src/LookAway.App
```

Die App läuft standardmäßig **unpackaged** (`WindowsPackageType=None`) — also als normale `.exe`
ohne MSIX-Registrierung. So funktionieren `dotnet run`, F5 und die portable Variante ohne Deploy.
Ein MSIX-Paket entsteht nur mit explizitem Override, z. B.:

```bash
msbuild src/LookAway.App/LookAway.App.csproj -p:Configuration=Release -p:Platform=x64 -p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true
```

## Architektur

Das Projekt folgt einer klassischen Schichtenarchitektur mit strikter Abhängigkeitsrichtung von aussen nach innen:

```
App  →  Application  →  Core  ←  Data
```

- **Core** ist unabhängig und kennt keine äußeren Schichten
- **Application** orchestriert Use Cases über Core-Interfaces
- **Data** implementiert Persistenz-Adapter für Core-Interfaces
- **App** bindet alles über Dependency Injection zusammen

## Konfiguration

Die Benutzerkonfiguration liegt pro Windows-Benutzer unter:

```
%APPDATA%\LookAway\settings.json
```

- Format: JSON (`System.Text.Json`, camelCase, Enums als Strings)
- Schreibvorgänge sind atomar (Temp-Datei + Rename) – kein halbgeschriebenes File bei Crashes
- Beschädigte oder fehlende Datei: Defaults werden geladen, der nächste Speichervorgang überschreibt sie
- Mehrere Lese-/Schreibzugriffe sind parallel sicher (Reader öffnen mit `FileShare.Delete`, Writes über Semaphor serialisiert)

Die Persistenz wird über `ISettingsRepository` (Core) abstrahiert; `JsonSettingsRepository` (Data) ist die Standardimplementierung und wird in `App.xaml.cs` als Singleton registriert.

## Logging und Crash-Handling

LookAway protokolliert in eine tagesbasierte Datei pro Windows-Benutzer:

```
%APPDATA%\LookAway\logs\lookaway-YYYY-MM-DD.log
%APPDATA%\LookAway\logs\crashes\crash-YYYYMMDD-hhmmss-fff.json
```

- **Format pro Eintrag:** `[Timestamp ISO-8601 UTC] [Level] Category: Message` plus optional Stack-Trace
- **Rotation:** täglich, alte Dateien (>7 Tage) werden beim ersten Schreibvorgang des Tages aufgeräumt
- **Log-Level:** Debug-Build = `Debug`, Release = `Information`. Microsoft- und System-Kategorien standardmäßig auf `Warning`
- **Sanitisierung:** der Windows-Benutzername und Pfade unter `%LOCALAPPDATA%`, `%APPDATA%`, `%USERPROFILE%` werden vor dem Schreiben durch generische Platzhalter ersetzt
- **Robustheit:** IO-Fehler im Logger werden geschluckt — die Anwendung crasht nicht, wenn die Festplatte voll ist
- **Globaler Crash-Hook:** unbehandelte Exceptions aus `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` und WinUI's `Application.UnhandledException` werden als JSON-Crash-Bericht im `crashes/`-Ordner persistiert (mit Sanitisierung)
- **Erkennung:** beim nächsten Start meldet `LogService` über `ICrashReporter.HasUnresolvedCrashes()`, ob der vorherige Lauf gecrasht ist; das Ergebnis wird im Start-Log geschrieben

Die Implementierung ist Microsoft-Standard (kein Serilog/NLog): eigener `RollingFileLoggerProvider` (Data) mit `LoggerMessage`-Source-Generator-Aufrufern in den Konsumenten.

## Timer-Engine

Domain-Modell der Pausen-Erinnerung in `LookAway.Application/Services/TimerService.cs`. Reine Logik, keine UI- oder Plattform-Abhängigkeit; konsumiert die Plattformdienste über Interfaces:

- `IClock` (Core) → `SystemClock` (Data)
- `IPowerModeWatcher` (Core) → `WindowsPowerModeWatcher` (Data, `Microsoft.Win32.SystemEvents`)
- `BreakModelRegistry` (Core) liefert die Standardintervalle pro `BreakModel`

| Modell | Arbeit | Pause |
|---|---|---|
| `ShortBreaks` | 60 min | 5 min |
| `ClassicPomodoro` | 25 min | 5 min |
| `ModifiedPomodoro` | 50 min | 10 min |
| `Ultradian` | 90 min | 20 min |
| `PhysicalCounter` | 40 min | 2 min |
| `TaskBased` | manuell, max 120 min | 10 min |
| `LegalCompliance` | 120 min | 15 min |

State-Machine: `Idle` → `Working` ↔ `OnBreak` (mit `Paused` als Querzustand). Events werden über einen unbeschränkten `Channel<TimerEvent>` als `IAsyncEnumerable` ausgegeben:

- `BreakDueEvent` → Pause fällig (Working → OnBreak)
- `BreakCompletedEvent` + `WorkResumedEvent` → Pause beendet (OnBreak → Working)
- `WorkResumedEvent` → Resume nach User-Pause (Paused → Working)
- `TimerPausedEvent(state, bySystem)` → Pause begonnen (User oder System-Suspend)
- `TimerStoppedEvent` → Stop

System-Sleep wird konsequent als Pause behandelt: `WindowsPowerModeWatcher` übersetzt `PowerModeChanged` in plattformneutrale Events, der `TimerService` friert die Restzeit ein und nimmt sie nach dem Aufwachen wieder auf. Eine Benutzer-Pause hat Vorrang vor System-Resume.

Tests: deterministisch über `FakeClock` und `FakePowerModeWatcher` in `LookAway.Tests.Unit`. Der reale Hintergrund-Loop wird in Tests durch ein hohes Tickintervall stillgelegt; Phasenwechsel werden über `internal void Tick()` (sichtbar via `InternalsVisibleTo`) ausgelöst.

## Tray-Integration und Single-Instance

LookAway läuft als Hintergrund-Anwendung mit Tray-Icon (kein Hauptfenster im Vordergrund, keine Taskleisten-Präsenz):

- `H.NotifyIcon.WinUI` (`TaskbarIcon`-Control) liefert das Tray-Icon
- `TrayIconService` (`internal` in App) bindet das Kontextmenü an den `ITimerService` und an Settings-/Exit-Callbacks
- Menü-Einträge: "Einstellungen…", "Pause jetzt starten", "Pausieren"/"Fortsetzen" (zustandsabhängig), "Über LookAway", "Beenden"
- Doppelklick auf das Tray-Icon öffnet das Settings-Fenster
- Hauptfenster ist beim Start verborgen (`AppWindow.Hide()`) und wird nur auf User-Aktion sichtbar

Status-Anzeige: das Icon spiegelt den Timer-Zustand wider (Arbeit/Pause/pausiert/DND), ein Tooltip zeigt live Restzeit und aktives Modell. Die UI-freie Übersetzung Zustand → Icon-Variante + Tooltip liegt im `TrayStatusPresenter` (Application) und ist ohne Tray-Control testbar; der `TrayIconService` pollt den `ITimerService` im Sekundentakt über einen `DispatcherQueueTimer` und tauscht die Icon-Variante (`tray-working/onbreak/paused/disabled.ico`) nur bei Zustandswechsel. Der Timer wird beim App-Start mit dem konfigurierten Modell (`BreakModelRegistry.GetEffective`) gestartet.

Single-Instance-Sperre via `ISingleInstanceLock` (Core) → `SingleInstanceLock` (Data):

- Mutex im `Local\`-Namespace pro Windows-Benutzer (`Local\LookAway-{userName}`)
- Zweite Instanz erkennt die laufende, signalisiert sie über einen `EventWaitHandle` (`Local\LookAway-Activate-{userName}`) und beendet sich
- Die laufende Instanz horcht im Hintergrund auf das Event und blendet auf Wunsch das Hauptfenster ein
- Beim sauberen Beenden wird der Mutex freigegeben, damit ein erneuter Start möglich ist

## Autostart mit Windows

LookAway kann optional mit dem Windows-Login starten — als Opt-in pro Benutzer, ohne Administrator-Rechte:

- Eingetragen wird ausschließlich unter `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` (nie `HKLM`)
- Eintragsname `LookAway`, Wert = vollständiger, in Anführungszeichen gesetzter Pfad zur `LookAway.App.exe` plus `--minimized` (Quoting wegen möglicher Leerzeichen, z. B. `C:\Program Files\…`)
- Der Pfad wird über `Process.GetCurrentProcess().MainModule.FileName` ermittelt

Die Abstraktion liegt in `IAutoStartService` (Core); `RegistryAutoStartService` (Data) ist die Windows-Implementierung. Fehler (z. B. durch Gruppenrichtlinien gesperrter Run-Schlüssel) werden als `AutoStartException` (Core) signalisiert, damit Aufrufer sie gezielt behandeln können.

`AutoStartCoordinator` (Application) hält Einstellung und Registry synchron:

- **Benutzeränderung → Registry:** beim Umschalten der Option wird der Registry-Eintrag sofort geschrieben bzw. entfernt und die Einstellung persistiert (`SetEnabledAsync`)
- **Startup-Abgleich Registry → Einstellung:** beim App-Start ist die Registry die führende Quelle (`SynchronizeFromRegistryAsync`). Ein manueller Eingriff — z. B. Deaktivieren über den Task-Manager-Autostart — wird in die Einstellung übernommen
- **Pfadkorrektur:** wurde die Anwendung verschoben, bringt der nächste Start den hinterlegten Pfad wieder auf den aktuellen Stand. `Enable()` ist idempotent und schreibt nur bei abweichendem Wert
- Der Abgleich ist optional und nicht startkritisch: schlägt er fehl, wird das geloggt, der Start läuft weiter

Tests: `AutoStartCoordinator` deterministisch über `FakeAutoStartService` und `InMemorySettingsRepository` in `LookAway.Tests.Unit`; `RegistryAutoStartService` gegen die echte Registry in `LookAway.Tests.Integration` (eindeutiger Eintragsname je Test mit Cleanup, daher keine Admin-Rechte nötig).

## Pause-Erinnerung

Wird eine Pause fällig (`BreakDueEvent`), zeigt LookAway ein dezentes Overlay-Fenster:

- Die UI-freie Aktionslogik liegt im `BreakReminderViewModel` (Application) und ist ohne WinUI testbar: drei Aktionen (Pause starten / 5 Min später / Überspringen), Timeout-Default nach 30 s = "Pause starten", die erste Aktion gewinnt (kein Überschreiben durch Doppelklick oder Timeout-Race).
- `BreakReminderWindow` (App/Views) ist eine eigenständige, nicht minimierbare `Window`-Instanz (480×320, zentriert, Farbpalette Indigo `#4361EE` / Weiß / Dunkelgrau). `IReminderPresenter`/`ReminderPresenter` (App) erzeugen sie auf dem UI-Thread und verhindern Stapel (zweite Meldung bei offenem Fenster wird ignoriert, `_isReminderOpen`).
- Der App-Event-Loop konsumiert den `ITimerService.Events`-Stream; bei `BreakDue` wird die Erinnerung nur gezeigt, wenn kein DND aktiv ist (`FullscreenDetectionService.TryShowReminder`), sonst nachgeholt. Snooze startet einen 5-min-Arbeitszyklus, Überspringen den regulären. Texte sind Platzhalter.

## Idle- und Vollbild-Erkennung (DND)

LookAway pausiert den Timer bei längerer Inaktivität und unterdrückt Erinnerungen während Vollbild-Apps:

- **Idle:** `IIdleDetector` (Core) → `WindowsIdleDetector` (Data, Win32 `GetLastInputInfo` via `LibraryImport`-P/Invoke). `IdleDetectionService` (Application) pausiert den Timer bei Inaktivität über der Schwelle (Default 5 min, konfigurierbar 1–30) und setzt ihn bei wiederkehrender Aktivität fort. Eine selbst ausgelöste Idle-Pause wird gemerkt, damit eine Benutzer-Pause nicht fälschlich fortgesetzt wird.
- **Vollbild/DND:** `IFullscreenDetector` (Core) → `WindowsFullscreenDetector` (Data, `GetForegroundWindow` + Monitorvergleich, Shell/Sperrbildschirm ausgeschlossen). `FullscreenDetectionService` (Application) setzt den DND-Zustand, unterdrückt fällige Erinnerungen und holt maximal eine verpasste Erinnerung nach Verlassen des Vollbildmodus nach.
- Beide Dienste werden in einem Hintergrund-`PeriodicTimer` (5 s, nicht im UI-Thread) ausgewertet; der DND-Zustand spiegelt sich ins Tray-Icon (`SetDndActive`). Settings: `PauseOnIdle`, `IdleThresholdMinutes`, `SuppressOnFullscreen`. Die Plattform-Calls sind als P/Invoke in der Data-Schicht isoliert; die Entscheidungslogik ist über Fakes ohne Win32 testbar.

## Settings-Fenster

Über das Tray-Menü ("Einstellungen…", Doppelklick oder Linksklick) öffnet sich ein
WinUI-3-Fenster mit vier Bereichen (Pivot): Allgemein, Pausenmodell, Eigene Intervalle und
Über LookAway.

- Die gesamte Lade-, Validierungs- und Persistenzlogik liegt im UI-freien `SettingsViewModel`
  (Application, `CommunityToolkit.Mvvm`) und ist ohne WinUI testbar. Das Fenster
  (`SettingsWindow`, App/Views) bindet nur daran; `ISettingsPresenter`/`SettingsPresenter`
  erzeugen es auf dem UI-Thread und verhindern Mehrfach-Fenster.
- **Allgemein:** Sprache (DE/EN/FR), Autostart, Auto-Pause bei Inaktivität samt Schwelle, DND im Vollbild.
- **Pausenmodell:** Auswahl aus allen sieben Modellen.
- **Eigene Intervalle:** optionale Überschreibung von Arbeits-/Pausendauer; die Grenzen folgen dem
  Modell (z. B. PhysicalCounter 30–45 min). Ungültige Werte werden markiert und blockieren "Speichern".
- **Speichern** schließt das Fenster, **Anwenden** speichert ohne zu schließen, **Abbrechen** verwirft.
  Gespeicherte Änderungen werden sofort übernommen (`SettingsApplied` → Timer-Neustart mit neuem Modell,
  Idle-/Vollbild-Dienste, Tray), nicht erst beim nächsten Start.
- **Autostart** wird beim Speichern über den `AutoStartCoordinator` mit der Registry synchronisiert; ist
  der Run-Schlüssel gesperrt, werden die übrigen Einstellungen trotzdem gespeichert.

### Lokalisierung (DE/EN/FR)

LookAway ist vollständig dreisprachig (Deutsch, Englisch, Französisch) mit Sprachwechsel zur
Laufzeit: `ILocalizationService` (Core) → `JsonLocalizationService` (Data) liefert Texte
über sprachneutrale Schlüssel aus eingebetteten JSON-Tabellen (`Localization/<sprache>.json`).

- Alle UI-Texte (Settings, Wizard, Reminder, Tray-Menü und -Tooltips, Pausenmodell-Namen und
  Übungs-Hinweise) verwenden den Service. Schlüssel-Konvention: `Bereich.Element` (z. B.
  `Settings.Title`, `Reminder.StartBreak`).
- Der Wechsel über das Settings-Fenster aktualisiert die UI sofort (`LanguageChanged` →
  `INotifyPropertyChanged` in den ViewModels, Live-Refresh des Tray-Menüs).
- Deutsch ist die Referenzsprache und der Fallback bei einem fehlenden Schlüssel (danach der
  Schlüsselname). Ein Konsistenztest stellt sicher, dass alle drei Tabellen exakt denselben
  Schlusselsatz haben.
- Beim Erststart wird die Sprache aus `CultureInfo.CurrentUICulture` vorbelegt (de/fr, sonst Englisch).

## First-Run-Wizard

Beim allerersten Start (keine `settings.json` vorhanden, `Settings.IsFirstRun`) führt ein dreistufiger
Assistent durch die Erstkonfiguration: Sprache, Pausenmodell und Autostart.

- UI-freies `WelcomeViewModel` (Application, getestete State-Machine: Schritte vor/zurück, Abschluss nur
  im letzten Schritt). `WelcomeWindow` (App/Views, nicht resizable, zentriert) bindet daran;
  `WelcomePresenter` zeigt es und meldet über `Task<bool>`, ob der Wizard abgeschlossen wurde.
- Die Startsprache wird aus `CultureInfo.CurrentUICulture` erkannt (de/fr, sonst Englisch), Default-Modell
  ist ModifiedPomodoro, Autostart vorausgewählt.
- "Fertig" speichert die Konfiguration (Autostart registry-synchron) und startet die App in den Tray-Modus.
  Schließt der Benutzer das Fenster ohne Abschluss, beendet sich die App und der Wizard erscheint beim
  nächsten Start erneut.

## Theme und Design

Das visuelle Design ist zentral in `src/LookAway.App/Themes/` definiert und wird in `App.xaml`
gemerged:

- `Colors.xaml`: die Farbpalette als `SolidColorBrush`-Ressourcen — `RcBackground` (`#FFFFFF`),
  `RcAccent` (Indigo `#4361EE`), `RcTextPrimary` (`#1F2937`), `RcInteraction` (`#F3F4F6`),
  `RcDivider` (`#E5E7EB`), `RcError`.
- `Typography.xaml`: `RcFontFamily` (Roboto mit System-Fallback), Schriftgrößen (H1 28, H2 22, H3 18,
  Body 14, Caption 12) sowie ein implizites `TextBlock`-Standardformat und Heading-Styles.
- `Controls.xaml`: `RcButtonStyle` (implizit, abgerundete Ecken, Roboto) und `RcPrimaryButtonStyle`
  (voller Indigo-Akzent für Primäraktionen wie Speichern/Fertig/Pause starten).

Die Views (Reminder, Settings, Wizard) referenzieren ausschließlich diese Ressourcen — keine
hartcodierten Farben mehr. **Hinweis:** Eine eingebettete `Roboto`-TTF unter `Assets/Fonts/` ist
noch nachzulegen (Open Font License); bis dahin greift die installierte Roboto bzw. die
Windows-Standardschrift über die Fallback-Kette in `RcFontFamily`.

## Sound-Optionen

Optional spielt LookAway bei einer Pause-Erinnerung einen dezenten Ton (Default: aus):

- Drei eingebettete, selbst synthetisierte PCM-WAVs unter `Assets/Sounds/` (`chime`, `bell`, `pop`) —
  jeweils unter 200 KB, lizenzfrei.
- `ISoundService` (Core) → `SoundService` (App, `Windows.Media.Playback.MediaPlayer`). Die Lautstärke
  wird pro Wiedergabe gesetzt (kein Eingriff in die System-Lautstärke); Wiedergabefehler (fehlendes
  Gerät, Gerätewechsel) werden geloggt und geschluckt, damit die App nie abstürzt.
- Settings-Tab "Sound": Ton aktivieren, Auswahl (Chime/Bell/Pop), Lautstärke-Slider (0–100 %, Default 30)
  und ein "Vorhören"-Button. Bei `SoundVolumePercent = 0` bleibt die Erinnerung stumm.
- Der Ton wird nur beim tatsächlichen Öffnen der Erinnerung abgespielt — ein bereits offenes
  Reminder-Fenster löst keinen zweiten Ton aus.

## Statistiken, History und CSV-Export

LookAway zeichnet jede angebotene Pause auf und zeigt Statistiken im Settings-Tab "Statistik":

- `BreakSession` (Core, validiert) mit Beginn, Ende, Modell und Ergebnis (`Taken`/`Snoozed`/`Skipped`).
  `IBreakHistoryRepository` (Core) → `JsonBreakHistoryRepository` (Data): append-only nach
  `%APPDATA%\LookAway\history.json`, atomar geschrieben; Einträge älter als 365 Tage werden beim
  Start entfernt.
- `StatisticsService` (Application, UI-frei, getestet über `IClock`/Fakes) aggregiert Heute (Anzahl,
  Pausenzeit, übersprungen), diese Woche (7 Tagesbalken) und dieses Jahr (12 Monatsbalken).
- Der Tab visualisiert die Balken mit schlichten `Border`-Elementen (kein Chart-Framework) und bietet
  einen CSV-Export (`CsvExporter`, UTF-8 mit BOM, Spalten `StartedAt,EndedAt,Duration,Model,Outcome`)
  über den `FileSavePicker`.
- Aufgezeichnet wird beim Schließen der Erinnerung (Outcome aus der gewählten Aktion); für gemachte
  Pausen wird die Pausendauer als Zeitraum hinterlegt.

## Globale Hotkeys

LookAway lässt sich systemweit per Tastenkombination bedienen (Standard aktiv):

- `Strg+Alt+P` → Pause-Erinnerung sofort anzeigen
- `Strg+Alt+S` → überspringen / Arbeitszyklus neu starten
- `Strg+Alt+D` → Nicht-stören manuell umschalten (Tray-Icon spiegelt den Zustand)

`IHotkeyService` (Core) → `WindowsHotkeyService` (Data) nutzt die Win32-API `RegisterHotKey` auf einem
eigenen Hintergrund-Thread mit nachrichtenfreiem Fenster (`HWND_MESSAGE`) und Message-Loop.
`HotkeyValidator`/`HotkeyDefaults` (Core, getestet) liefern Validierung (Modifikator + Taste,
Kollisionsprüfung) und die Standardbelegung. Die Bindungen werden in den Einstellungen persistiert;
der Settings-Tab "Hotkeys" erlaubt Aktivieren und Zurücksetzen auf die Standardwerte. Fehlgeschlagene
Einzelregistrierungen (Konflikt mit einer anderen App) werden geloggt, ohne die übrigen zu verhindern;
beim Beenden werden alle Hotkeys freigegeben.

## Update-Prüfung

LookAway prüft optional auf neue Versionen über die GitHub-Releases-API (Standard aktiv):

- `IUpdateChecker` (Core) → `GitHubUpdateChecker` (Data) ruft `releases/latest` ab und vergleicht das
  Tag mit der installierten Version. Der HTTP-Zugriff ist hinter `IHttpGetClient` (→ `HttpGetClient`,
  User-Agent + 10 s Timeout) gekapselt und damit testbar; Netzwerk-/Parsefehler ergeben "kein Update"
  statt einer Exception. Die Versionsvergleichslogik liegt im UI-freien `UpdateInfo` (führendes `v`
  und Prärelease-Suffix werden abgeschnitten).
- `UpdateSchedule` (Core) entscheidet anhand der Häufigkeit (`OnStartup`/`Daily`/`Weekly`) und des
  letzten Prüfzeitpunkts, ob beim Start geprüft wird — schont das GitHub-Rate-Limit.
- Einstellungen (Über-Tab): Aktivieren, Häufigkeit, "Jetzt prüfen" mit Statusanzeige sowie die
  Option "Automatisch aktualisieren".
- **Automatische Installation:** Findet die Prüfung ein Update, kann LookAway es selbst einspielen.
  `UpdateInstallerService` (Application) lädt die Portable-ZIP aus den Release-Assets (nur HTTPS auf
  GitHub-Hosts, mit Größen-/Zip-Bomben-Limit), entpackt sie in `%LOCALAPPDATA%\LookAway\updates\<Version>`
  und tauscht beim nächsten Start über einen kurzlebigen Helfer-Prozess (`--apply-update`, in
  `UpdateProcess`/`UpdateApplyArgs`) die Programmdateien — mit Backup/Rollback, ohne `portable.flag` zu
  übernehmen, unter Erhalt der Benutzerdaten. Vor dem Einspielen werden Version und SHA-256 der
  heruntergeladenen Datei gegen den vermerkten Wert geprüft. Manuell löst der Tray-Eintrag "Update"
  denselben Ablauf sofort aus.
- **Echtheit (Release-Signatur):** Vor dem Entpacken/Einspielen wird das Paket gegen eine losgelöste
  **ECDSA-P-256/SHA-256-Signatur** geprüft (`ReleaseSignatureVerifier`, fail-closed): Der Updater lädt
  zusätzlich zum ZIP die `*.sig` aus den Release-Assets und verifiziert sie gegen den fest eingebetteten
  öffentlichen Schlüssel. Ohne gültige, zur Datei passende Signatur wird das Update abgewiesen — ein
  übernommener Release-Kanal kann ohne den privaten Schlüssel keine gültige Signatur erzeugen. Der private
  Schlüssel wird **offline** gehalten (nie im Repo). Werkzeuge: `tools/new-signing-key.ps1` erzeugt/rotiert
  das Schlüsselpaar (öffentlichen Teil in `ReleaseSignatureVerifier.DefaultPublicKeySpkiBase64` eintragen),
  `tools/sign-release.ps1` signiert ein Paket. Die CI signiert automatisch, wenn das Secret
  `LOOKAWAY_SIGNING_KEY` (privates PEM) gesetzt ist; sonst bleibt das Paket unsigniert und muss lokal
  signiert werden.
- **Grenzen:** Der Datei-Tausch funktioniert nur, wenn der Programmordner beschreibbar ist
  (portable und Per-User-Installation); bei einer "für alle Benutzer"-Installation in `Programme` fällt
  LookAway auf das Öffnen der Release-Seite zurück. Zusätzlich zur Signatur sichern HTTPS +
  GitHub-Host-Pinning den Transport und der SHA-256-Abgleich das beim nächsten Start einzuspielende
  Staging-Verzeichnis.

## Pause-Aktionen

Optional verstärkt LookAway den Pausencharakter (beide opt-in und reversibel):

- **Bildschirm dimmen:** `IScreenDimmer` (Core) → `WindowsScreenDimmer` (Data) senkt die Helligkeit
  DDC/CI-fähiger Monitore (Dxva2) und stellt sie am Pausenende wieder her. Auf Hardware ohne DDC/CI
  (viele Notebooks) bleibt der Aufruf wirkungslos; Fehler werden geschluckt und geloggt.
- **Medien pausieren:** `IMediaController` (Core) → `WindowsMediaController` (Data) pausiert über die
  SMTC-API alle laufenden Wiedergabe-Sessions und setzt am Pausenende nur die zuvor laufenden fort.
- Die UI-freie `PauseActionService` (Application, getestet) koordiniert beides anhand der Einstellungen
  (`BeginBreakAsync`/`EndBreakAsync`); die App ruft sie beim Pausenbeginn (gewählte Pause) und beim
  `BreakCompletedEvent` auf. Settings-Tab "Pause-Aktionen": Dimmen + Helligkeit (10–80 %), Medien
  pausieren + nach der Pause fortsetzen.

## Distribution und Installation

LookAway wird als portable ZIP und als MSIX-Paket verteilt. Die Versionsnummer ist
zentral in `Directory.Build.props` (`<Version>`) gepflegt; die CI kann sie beim Tag-Push über
`-p:Version=…` überschreiben.

### Portable

```powershell
./tools/publish.ps1 -Version 1.0.0
```

Das Skript führt ein Self-Contained-`dotnet publish` (win-x64) aus, legt die Markierung
`portable.flag` neben die EXE und packt alles als `dist/LookAway-Portable-v<Version>.zip`. Liegt die
`portable.flag` neben `LookAway.exe`, speichert die App Konfiguration, Historie und Logs **neben der
EXE** statt unter `%APPDATA%\LookAway` (`AppDataLocation` / `AppPaths.ResolveDataDirectory`, getestet).

### MSIX

Das MSIX-Paket entsteht über die Windows-Packaging-Tools (Visual Studio „Paket erstellen" oder
`msbuild /p:WindowsPackageType=MSIX /p:GenerateAppxPackageOnBuild=true`). Für lokale Tests genügt ein
selbst signiertes Zertifikat mit eindeutiger Publisher-CN; für die Produktion ist ein Code-Signing-
Zertifikat einer vertrauenswürdigen CA nötig. Capabilities bleiben minimal (kein
`broadFileSystemAccess`).

### CI-Release

Bei einem Tag-Push `v*.*.*` baut die CI nach dem grünen `build-test`-Job die portable ZIP und
veröffentlicht sie als GitHub-Release-Artefakt (`.github/workflows/ci.yml`, Job `release`).

## Review

`tools/review.ps1` orchestriert lokale Qualitäts-Checks:

```powershell
./tools/review.ps1 -Mode build       # nur Build
./tools/review.ps1 -Mode test        # Build + Tests
./tools/review.ps1 -Mode security    # Secret-Scan + sensible Patterns
./tools/review.ps1 -Mode all         # Build + Tests + Security
```

`enterprise` legt ein Markdown-Report-Skelett unter `.ai/reviews/` an, in das die Bewertung eingetragen wird. Die Skript-Pfade gehen vom Solution-Root aus.

## Continuous Integration

`.github/workflows/ci.yml` läuft auf jedem Push/Pull-Request gegen `main` und auf manuellem `workflow_dispatch`:

| Step | Was | Warum |
|------|-----|-------|
| `actions/checkout@v5` | Quelltext laden | Standard |
| `actions/setup-dotnet@v5` | .NET 10 SDK | Solution-Target |
| `actions/cache@v5` | NuGet-Cache pro `csproj`-Hash | Build-Beschleunigung |
| `dotnet workload restore` | Windows App SDK Workloads | WinUI 3 |
| `dotnet build -c Release` | Compile mit `TreatWarningsAsErrors` | Quality-Gate |
| `dotnet test --logger trx --collect "XPlat Code Coverage"` | xUnit + Coverage | Substanz-Gate |
| `tools/review.ps1 -Mode security` | Pattern-Scan | Zusätzliche Heuristik (Secrets, BinaryFormatter, etc.) |
| `actions/upload-artifact@v4` | trx + Coverage hochladen | Detail-Analyse |
| `dorny/test-reporter@v2` | Test-Summary in der PR | Sichtbarkeit |

Runner: `windows-latest` (zwingend wegen WinUI 3). Concurrency-Group bricht ältere Läufe pro Ref ab. Timeout 15 Minuten.

## Lizenz

MIT-Lizenz – siehe [LICENSE](../LICENSE).
