# LookAway

[![CI](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml)

Eine schlanke Windows-Tray-Anwendung, die dezent an Bildschirmpausen erinnert. Mehrere wissenschaftlich fundierte Pausenmodelle, dreisprachig (Deutsch, Englisch, Franzoesisch), und vollstaendig konfigurierbar pro Windows-Benutzer.

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
- Doppelklick auf das Tray-Icon oeffnet das Settings-Fenster
- Hauptfenster ist beim Start verborgen (`AppWindow.Hide()`) und wird nur auf User-Aktion sichtbar

Single-Instance-Sperre via `SingleInstanceLock` (Application):

- Mutex im `Local\`-Namespace pro Windows-Benutzer (`Local\LookAway-{userName}`)
- Zweite Instanz erkennt die laufende, signalisiert sie ueber einen `EventWaitHandle` (`Local\LookAway-Activate-{userName}`) und beendet sich
- Die laufende Instanz horcht im Hintergrund auf das Event und blendet auf Wunsch das Hauptfenster ein
- Beim sauberen Beenden wird der Mutex freigegeben, damit ein erneuter Start moeglich ist

## Review

`tools/review.ps1` orchestriert lokale Qualitaets-Checks:

```powershell
./tools/review.ps1 -Mode build       # nur Build
./tools/review.ps1 -Mode test        # Build + Tests
./tools/review.ps1 -Mode security    # Secret-Scan + sensible Patterns
./tools/review.ps1 -Mode all         # Build + Tests + Security
```


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
- **Status-Dateien:** `.ai/triage-status.json`, `.ai/triage-escalation.md`, `.ai/reviews/*.md`, `.ai/erp/*.md`

Volle Doku: `F:\Entwicklung\_Anleitungen\allgemein\triage-workflow.md`
<!-- /TRIAGE-WORKFLOW -->
