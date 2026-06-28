# LookAway

[![CI](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml)

Eine schlanke Windows-Tray-Anwendung, die dezent an Bildschirmpausen erinnert. Mehrere wissenschaftlich fundierte Pausenmodelle, dreisprachig (Deutsch, Englisch, Franzoesisch), und vollstaendig konfigurierbar pro Windows-Benutzer.

> **KI-Workflow-Material:** Briefs, Rules, Memory, Reviews und Skripte liegen unter
> `F:\Entwicklung\dotnet\_ai\LookAway\` (zentrale KI-Topologie). Die Junction
> `_ai\` im Repo-Root verweist auf diesen Pfad und ist via `.gitignore` ausgeklammert.

## Voraussetzungen

- Windows 10 1809 oder neuer / Windows 11
- .NET 10 SDK (fuer den Build)
- Windows App SDK (fuer WinUI 3)

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

## Architektur

Das Projekt folgt einer klassischen Schichtenarchitektur mit strikter Abhaengigkeitsrichtung von aussen nach innen:

```
App  →  Application  →  Core  ←  Data
```

- **Core** ist unabhaengig und kennt keine aeusseren Schichten
- **Application** orchestriert Use Cases ueber Core-Interfaces
- **Data** implementiert Persistenz-Adapter fuer Core-Interfaces
- **App** bindet alles ueber Dependency Injection zusammen

## Konfiguration

Die Benutzerkonfiguration liegt pro Windows-Benutzer unter:

```
%APPDATA%\LookAway\settings.json
```

- Format: JSON (`System.Text.Json`, camelCase, Enums als Strings)
- Schreibvorgaenge sind atomar (Temp-Datei + Rename) – kein halbgeschriebenes File bei Crashes
- Beschaedigte oder fehlende Datei: Defaults werden geladen, der naechste Speichervorgang ueberschreibt sie
- Mehrere Lese-/Schreibzugriffe sind parallel sicher (Reader oeffnen mit `FileShare.Delete`, Writes ueber Semaphor serialisiert)

Die Persistenz wird ueber `ISettingsRepository` (Core) abstrahiert; `JsonSettingsRepository` (Data) ist die Standardimplementierung und wird in `App.xaml.cs` als Singleton registriert.

## Logging und Crash-Handling

LookAway protokolliert in eine tagesbasierte Datei pro Windows-Benutzer:

```
%APPDATA%\LookAway\logs\lookaway-YYYY-MM-DD.log
%APPDATA%\LookAway\logs\crashes\crash-YYYYMMDD-hhmmss-fff.json
```

- **Format pro Eintrag:** `[Timestamp ISO-8601 UTC] [Level] Category: Message` plus optional Stack-Trace
- **Rotation:** taeglich, alte Dateien (>7 Tage) werden beim ersten Schreibvorgang des Tages aufgeraeumt
- **Log-Level:** Debug-Build = `Debug`, Release = `Information`. Microsoft- und System-Kategorien standardmaessig auf `Warning`
- **Sanitisierung:** der Windows-Benutzername und Pfade unter `%LOCALAPPDATA%`, `%APPDATA%`, `%USERPROFILE%` werden vor dem Schreiben durch generische Platzhalter ersetzt
- **Robustheit:** IO-Fehler im Logger werden geschluckt — die Anwendung crasht nicht, wenn die Festplatte voll ist
- **Globaler Crash-Hook:** unbehandelte Exceptions aus `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` und WinUI's `Application.UnhandledException` werden als JSON-Crash-Bericht im `crashes/`-Ordner persistiert (mit Sanitisierung)
- **Erkennung:** beim naechsten Start meldet `LogService` ueber `ICrashReporter.HasUnresolvedCrashes()`, ob der vorherige Lauf gecrasht ist; das Ergebnis wird im Start-Log geschrieben

Die Implementierung ist Microsoft-Standard (kein Serilog/NLog): eigener `RollingFileLoggerProvider` (Data) mit `LoggerMessage`-Source-Generator-Aufrufern in den Konsumenten.

## Timer-Engine

Domain-Modell der Pausen-Erinnerung in `LookAway.Application/Services/TimerService.cs`. Reine Logik, keine UI- oder Plattform-Abhaengigkeit; konsumiert die Plattformdienste ueber Interfaces:

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

State-Machine: `Idle` → `Working` ↔ `OnBreak` (mit `Paused` als Querzustand). Events werden ueber einen unbeschraenkten `Channel<TimerEvent>` als `IAsyncEnumerable` ausgegeben:

- `BreakDueEvent` → Pause faellig (Working → OnBreak)
- `BreakCompletedEvent` + `WorkResumedEvent` → Pause beendet (OnBreak → Working)
- `WorkResumedEvent` → Resume nach User-Pause (Paused → Working)
- `TimerPausedEvent(state, bySystem)` → Pause begonnen (User oder System-Suspend)
- `TimerStoppedEvent` → Stop

System-Sleep wird konsequent als Pause behandelt: `WindowsPowerModeWatcher` uebersetzt `PowerModeChanged` in plattformneutrale Events, der `TimerService` friert die Restzeit ein und nimmt sie nach dem Aufwachen wieder auf. Eine Benutzer-Pause hat Vorrang vor System-Resume.

Tests: deterministisch ueber `FakeClock` und `FakePowerModeWatcher` in `LookAway.Tests.Unit`. Der reale Hintergrund-Loop wird in Tests durch ein hohes Tickintervall stillgelegt; Phasenwechsel werden ueber `internal void Tick()` (sichtbar via `InternalsVisibleTo`) ausgeloest.

## Tray-Integration und Single-Instance

LookAway laeuft als Hintergrund-Anwendung mit Tray-Icon (kein Hauptfenster im Vordergrund, keine Taskleisten-Praesenz):

- `H.NotifyIcon.WinUI` (`TaskbarIcon`-Control) liefert das Tray-Icon
- `TrayIconService` (`internal` in App) bindet das Kontextmenue an den `ITimerService` und an Settings-/Exit-Callbacks
- Menue-Eintraege: "Einstellungen…", "Pause jetzt starten", "Pausieren"/"Fortsetzen" (zustandsabhaengig), "Ueber LookAway", "Beenden"
- Doppelklick auf das Tray-Icon oeffnet das (in BRIEF008 noch zu fuellende) Settings-Fenster
- Hauptfenster ist beim Start verborgen (`AppWindow.Hide()`) und wird nur auf User-Aktion sichtbar

Status-Anzeige (BRIEF015): das Icon spiegelt den Timer-Zustand wider (Arbeit/Pause/pausiert/DND), ein Tooltip zeigt live Restzeit und aktives Modell. Die UI-freie Uebersetzung Zustand → Icon-Variante + Tooltip liegt im `TrayStatusPresenter` (Application) und ist ohne Tray-Control testbar; der `TrayIconService` pollt den `ITimerService` im Sekundentakt ueber einen `DispatcherQueueTimer` und tauscht die Icon-Variante (`tray-working/onbreak/paused/disabled.png`) nur bei Zustandswechsel. Der Timer wird beim App-Start mit dem konfigurierten Modell (`BreakModelRegistry.GetEffective`) gestartet.

Single-Instance-Sperre via `SingleInstanceLock` (Application):

- Mutex im `Local\`-Namespace pro Windows-Benutzer (`Local\LookAway-{userName}`)
- Zweite Instanz erkennt die laufende, signalisiert sie ueber einen `EventWaitHandle` (`Local\LookAway-Activate-{userName}`) und beendet sich
- Die laufende Instanz horcht im Hintergrund auf das Event und blendet auf Wunsch das Hauptfenster ein
- Beim sauberen Beenden wird der Mutex freigegeben, damit ein erneuter Start moeglich ist

## Autostart mit Windows

LookAway kann optional mit dem Windows-Login starten — als Opt-in pro Benutzer, ohne Administrator-Rechte:

- Eingetragen wird ausschliesslich unter `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` (nie `HKLM`)
- Eintragsname `LookAway`, Wert = vollstaendiger, in Anfuehrungszeichen gesetzter Pfad zur `LookAway.App.exe` plus `--minimized` (Quoting wegen moeglicher Leerzeichen, z. B. `C:\Program Files\…`)
- Der Pfad wird ueber `Process.GetCurrentProcess().MainModule.FileName` ermittelt

Die Abstraktion liegt in `IAutoStartService` (Core); `RegistryAutoStartService` (Data) ist die Windows-Implementierung. Fehler (z. B. durch Gruppenrichtlinien gesperrter Run-Schluessel) werden als `AutoStartException` (Core) signalisiert, damit Aufrufer sie gezielt behandeln koennen.

`AutoStartCoordinator` (Application) haelt Einstellung und Registry synchron:

- **Benutzeraenderung → Registry:** beim Umschalten der Option wird der Registry-Eintrag sofort geschrieben bzw. entfernt und die Einstellung persistiert (`SetEnabledAsync`)
- **Startup-Abgleich Registry → Einstellung:** beim App-Start ist die Registry die fuehrende Quelle (`SynchronizeFromRegistryAsync`). Ein manueller Eingriff — z. B. Deaktivieren ueber den Task-Manager-Autostart — wird in die Einstellung uebernommen
- **Pfadkorrektur:** wurde die Anwendung verschoben, bringt der naechste Start den hinterlegten Pfad wieder auf den aktuellen Stand. `Enable()` ist idempotent und schreibt nur bei abweichendem Wert
- Der Abgleich ist optional und nicht startkritisch: schlaegt er fehl, wird das geloggt, der Start laeuft weiter

Tests: `AutoStartCoordinator` deterministisch ueber `FakeAutoStartService` und `InMemorySettingsRepository` in `LookAway.Tests.Unit`; `RegistryAutoStartService` gegen die echte Registry in `LookAway.Tests.Integration` (eindeutiger Eintragsname je Test mit Cleanup, daher keine Admin-Rechte noetig).

## Pause-Erinnerung

Wird eine Pause faellig (`BreakDueEvent`), zeigt LookAway ein dezentes Overlay-Fenster (BRIEF007):

- Die UI-freie Aktionslogik liegt im `BreakReminderViewModel` (Application) und ist ohne WinUI testbar: drei Aktionen (Pause starten / 5 Min spaeter / Ueberspringen), Timeout-Default nach 30 s = "Pause starten", die erste Aktion gewinnt (kein Ueberschreiben durch Doppelklick oder Timeout-Race).
- `BreakReminderWindow` (App/Views) ist eine eigenstaendige, nicht minimierbare `Window`-Instanz (480×320, zentriert, Farbpalette Indigo `#4361EE` / Weiss / Dunkelgrau). `IReminderPresenter`/`ReminderPresenter` (App) erzeugen sie auf dem UI-Thread und verhindern Stapel (zweite Meldung bei offenem Fenster wird ignoriert, `_isReminderOpen`).
- Der App-Event-Loop konsumiert den `ITimerService.Events`-Stream; bei `BreakDue` wird die Erinnerung nur gezeigt, wenn kein DND aktiv ist (`FullscreenDetectionService.TryShowReminder`), sonst nachgeholt. Snooze startet einen 5-min-Arbeitszyklus, Ueberspringen den regulaeren. Texte sind Platzhalter bis BRIEF010.

## Idle- und Vollbild-Erkennung (DND)

LookAway pausiert den Timer bei laengerer Inaktivitaet und unterdrueckt Erinnerungen waehrend Vollbild-Apps (BRIEF016):

- **Idle:** `IIdleDetector` (Core) → `WindowsIdleDetector` (Data, Win32 `GetLastInputInfo` via `LibraryImport`-P/Invoke). `IdleDetectionService` (Application) pausiert den Timer bei Inaktivitaet ueber der Schwelle (Default 5 min, konfigurierbar 1–30) und setzt ihn bei wiederkehrender Aktivitaet fort. Eine selbst ausgeloeste Idle-Pause wird gemerkt, damit eine Benutzer-Pause nicht faelschlich fortgesetzt wird.
- **Vollbild/DND:** `IFullscreenDetector` (Core) → `WindowsFullscreenDetector` (Data, `GetForegroundWindow` + Monitorvergleich, Shell/Sperrbildschirm ausgeschlossen). `FullscreenDetectionService` (Application) setzt den DND-Zustand, unterdrueckt faellige Erinnerungen und holt maximal eine verpasste Erinnerung nach Verlassen des Vollbildmodus nach.
- Beide Dienste werden in einem Hintergrund-`PeriodicTimer` (5 s, nicht im UI-Thread) ausgewertet; der DND-Zustand spiegelt sich ins Tray-Icon (`SetDndActive`). Settings: `PauseOnIdle`, `IdleThresholdMinutes`, `SuppressOnFullscreen`. Die Plattform-Calls sind als P/Invoke in der Data-Schicht isoliert; die Entscheidungslogik ist ueber Fakes ohne Win32 testbar.

## Settings-Fenster

Ueber das Tray-Menue ("Einstellungen…", Doppelklick oder Linksklick) oeffnet sich ein
WinUI-3-Fenster mit vier Bereichen (Pivot): Allgemein, Pausenmodell, Eigene Intervalle und
Ueber LookAway (BRIEF008).

- Die gesamte Lade-, Validierungs- und Persistenzlogik liegt im UI-freien `SettingsViewModel`
  (Application, `CommunityToolkit.Mvvm`) und ist ohne WinUI testbar. Das Fenster
  (`SettingsWindow`, App/Views) bindet nur daran; `ISettingsPresenter`/`SettingsPresenter`
  erzeugen es auf dem UI-Thread und verhindern Mehrfach-Fenster.
- **Allgemein:** Sprache (DE/EN/FR), Autostart, Auto-Pause bei Inaktivitaet samt Schwelle, DND im Vollbild.
- **Pausenmodell:** Auswahl aus allen sieben Modellen.
- **Eigene Intervalle:** optionale Ueberschreibung von Arbeits-/Pausendauer; die Grenzen folgen dem
  Modell (z. B. PhysicalCounter 30–45 min). Ungueltige Werte werden markiert und blockieren "Speichern".
- **Speichern** schliesst das Fenster, **Anwenden** speichert ohne zu schliessen, **Abbrechen** verwirft.
  Gespeicherte Aenderungen werden sofort uebernommen (`SettingsApplied` → Timer-Neustart mit neuem Modell,
  Idle-/Vollbild-Dienste, Tray), nicht erst beim naechsten Start.
- **Autostart** wird beim Speichern ueber den `AutoStartCoordinator` mit der Registry synchronisiert; ist
  der Run-Schluessel gesperrt, werden die uebrigen Einstellungen trotzdem gespeichert.

### Lokalisierung (DE/EN/FR)

LookAway ist vollstaendig dreisprachig (Deutsch, Englisch, Franzoesisch) mit Sprachwechsel zur
Laufzeit (BRIEF010): `ILocalizationService` (Core) → `JsonLocalizationService` (Data) liefert Texte
ueber sprachneutrale Schluessel aus eingebetteten JSON-Tabellen (`Localization/<sprache>.json`).

- Alle UI-Texte (Settings, Wizard, Reminder, Tray-Menue und -Tooltips, Pausenmodell-Namen und
  Uebungs-Hinweise) verwenden den Service. Schluessel-Konvention: `Bereich.Element` (z. B.
  `Settings.Title`, `Reminder.StartBreak`).
- Der Wechsel ueber das Settings-Fenster aktualisiert die UI sofort (`LanguageChanged` →
  `INotifyPropertyChanged` in den ViewModels, Live-Refresh des Tray-Menues).
- Deutsch ist die Referenzsprache und der Fallback bei einem fehlenden Schluessel (danach der
  Schluesselname). Ein Konsistenztest stellt sicher, dass alle drei Tabellen exakt denselben
  Schlusselsatz haben.
- Beim Erststart wird die Sprache aus `CultureInfo.CurrentUICulture` vorbelegt (de/fr, sonst Englisch).

## First-Run-Wizard

Beim allerersten Start (keine `settings.json` vorhanden, `Settings.IsFirstRun`) fuehrt ein dreistufiger
Assistent durch die Erstkonfiguration (BRIEF009): Sprache, Pausenmodell und Autostart.

- UI-freies `WelcomeViewModel` (Application, getestete State-Machine: Schritte vor/zurueck, Abschluss nur
  im letzten Schritt). `WelcomeWindow` (App/Views, nicht resizable, zentriert) bindet daran;
  `WelcomePresenter` zeigt es und meldet ueber `Task<bool>`, ob der Wizard abgeschlossen wurde.
- Die Startsprache wird aus `CultureInfo.CurrentUICulture` erkannt (de/fr, sonst Englisch), Default-Modell
  ist ModifiedPomodoro, Autostart vorausgewaehlt.
- "Fertig" speichert die Konfiguration (Autostart registry-synchron) und startet die App in den Tray-Modus.
  Schliesst der Benutzer das Fenster ohne Abschluss, beendet sich die App und der Wizard erscheint beim
  naechsten Start erneut.

## Review

`tools/review.ps1` orchestriert lokale Qualitaets-Checks:

```powershell
./tools/review.ps1 -Mode build       # nur Build
./tools/review.ps1 -Mode test        # Build + Tests
./tools/review.ps1 -Mode security    # Secret-Scan + sensible Patterns
./tools/review.ps1 -Mode all         # Build + Tests + Security
./tools/review.ps1 -Mode enterprise  # all + ERP-2026-Report-Skeleton in .ai/reviews/
```

`enterprise` legt einen Markdown-Report nach `_ai/LookAway/rules/enterprise-review.md` an, in den der Reviewer die Bewertung eintraegt. Die Skript-Pfade gehen vom Solution-Root aus.

## Continuous Integration

`.github/workflows/ci.yml` laeuft auf jedem Push/Pull-Request gegen `main` und auf manuellem `workflow_dispatch`:

| Step | Was | Warum |
|------|-----|-------|
| `actions/checkout@v5` | Quelltext laden | Standard |
| `actions/setup-dotnet@v5` | .NET 10 SDK | Solution-Target |
| `actions/cache@v5` | NuGet-Cache pro `csproj`-Hash | Build-Beschleunigung |
| `dotnet workload restore` | Windows App SDK Workloads | WinUI 3 |
| `dotnet build -c Release` | Compile mit `TreatWarningsAsErrors` | Quality-Gate |
| `dotnet test --logger trx --collect "XPlat Code Coverage"` | xUnit + Coverage | Substanz-Gate |
| `tools/review.ps1 -Mode security` | Pattern-Scan | Zusaetzliche Heuristik (Secrets, BinaryFormatter, etc.) |
| `actions/upload-artifact@v4` | trx + Coverage hochladen | Detail-Analyse |
| `dorny/test-reporter@v2` | Test-Summary in der PR | Sichtbarkeit |

Runner: `windows-latest` (zwingend wegen WinUI 3). Concurrency-Group bricht aeltere Laeufe pro Ref ab. Timeout 15 Minuten.

## Lizenz

Proprietaer – alle Rechte vorbehalten.

<!-- TRIAGE-WORKFLOW: auto-managed by triage-deploy.ps1 -->
## Triage und Reviews

- **Watcher starten:** `.\triage-watch.ps1` (bzw. `.\triage-watch-php.ps1` / `.\triage-watch-shopware.ps1`) im Projekt-Root
- **Review on-demand:** `.\triage-review.ps1` -- laedt Projekt-Regeln aus `_ai/<Projekt>/rules/` und uebergibt sie an Ollama
- **Status-Dateien:** `_ai/<Projekt>/triage-status.json`, `_ai/<Projekt>/triage-escalation.md`, `_ai/<Projekt>/reviews/*.md`, `_ai/<Projekt>/erp/*.md`

Volle Doku: `F:\Entwicklung\_Anleitungen\allgemein\triage-workflow.md`
Routing-Regeln: `_ai/<Projekt>/rules/ollama-delegation.md` und `_ai/<Projekt>/rules/enterprise-review.md`
<!-- /TRIAGE-WORKFLOW -->
