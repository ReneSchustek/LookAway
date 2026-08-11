# LookAway

[![CI](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml)

[Deutsch](DEVELOPMENT.md) · **English**

A lightweight Windows tray application that discreetly reminds you to take screen breaks. Several scientifically grounded break models, trilingual (German, English, French), and fully configurable per Windows user.

## Requirements

- Windows 10 1809 or newer / Windows 11
- .NET 10 SDK (for the build)
- Windows App SDK (for WinUI 3)

## Solution structure

```
LookAway/
├── src/
│   ├── LookAway.App/             WinUI 3 app, views, view models, composition root
│   ├── LookAway.Core/            Entities, value objects, services, interfaces, enums
│   └── LookAway.Data/            Repositories, persistence, Windows adapters
├── tests/
│   ├── LookAway.App.Tests/       xUnit, view models
│   ├── LookAway.Core.Tests/      xUnit, domain and services
│   └── LookAway.Data.Tests/      xUnit, real file system / registry
├── Directory.Build.props         Global build settings
├── .editorconfig                 Code-style conventions
└── LookAway.slnx                 Solution file
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

## Development

```bash
dotnet run --project src/LookAway.App
```

By default the app runs **unpackaged** (`WindowsPackageType=None`) — i.e. as a normal `.exe`
without MSIX registration. This way `dotnet run`, F5 and the portable variant work without a deploy.
An MSIX package is only produced with an explicit override, e.g.:

```bash
msbuild src/LookAway.App/LookAway.App.csproj -p:Configuration=Release -p:Platform=x64 -p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true
```

## Architecture

The project follows a classic layered architecture with a strict dependency direction from the outside inward:

```
App  →  Core  ←  Data
```

- **Core** is independent and knows nothing about the outer layers; it holds the domain and the services
- **Data** implements the Core interfaces against the file system, the registry and Windows APIs
- **App** holds views and view models and wires everything together via dependency injection

## Configuration

The user configuration lives per Windows user under:

```
%APPDATA%\LookAway\settings.json
```

- Format: JSON (`System.Text.Json`, camelCase, enums as strings)
- Writes are atomic (temp file + rename) – no half-written file on crashes
- Corrupt or missing file: defaults are loaded, the next save overwrites them
- Multiple concurrent reads/writes are safe (readers open with `FileShare.Delete`, writes serialized via a semaphore)

Persistence is abstracted via `ISettingsRepository` (Core); `JsonSettingsRepository` (Data) is the default implementation and is registered as a singleton in `App.xaml.cs`.

## Logging and crash handling

LookAway logs into a per-day file per Windows user:

```
%APPDATA%\LookAway\logs\lookaway-YYYY-MM-DD.log
%APPDATA%\LookAway\logs\crashes\crash-YYYYMMDD-hhmmss-fff.json
```

- **Format per entry:** `[Timestamp ISO-8601 UTC] [Level] Category: Message` plus an optional stack trace
- **Rotation:** daily; old files (>7 days) are cleaned up on the first write of the day
- **Log level:** Debug build = `Debug`, Release = `Information`. Microsoft and System categories default to `Warning`
- **Sanitization:** the Windows user name and paths under `%LOCALAPPDATA%`, `%APPDATA%`, `%USERPROFILE%` are replaced with generic placeholders before writing
- **Robustness:** IO errors in the logger are swallowed — the application does not crash when the disk is full
- **Global crash hook:** unhandled exceptions from `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` and WinUI's `Application.UnhandledException` are persisted as a JSON crash report in the `crashes/` folder (sanitized)
- **Detection:** on the next launch `LogService` reports via `ICrashReporter.HasUnresolvedCrashes` whether the previous run crashed; the result is written to the startup log

The implementation is Microsoft-standard (no Serilog/NLog): a custom `RollingFileLoggerProvider` (Data) with `LoggerMessage` source-generator callers in the consumers.

## Timer engine

Domain model of the break reminder in `LookAway.Core/Services/TimerService.cs`. Pure logic, no UI or platform dependency; it consumes platform services via interfaces:

- `IClock` (Core) → `SystemClock` (Data)
- `IPowerModeWatcher` (Core) → `WindowsPowerModeWatcher` (Data, `Microsoft.Win32.SystemEvents`)
- `BreakModelRegistry` (Core) provides the default intervals per `BreakModel`

| Model | Work | Break |
|---|---|---|
| `ShortBreaks` | 60 min | 5 min |
| `ClassicPomodoro` | 25 min | 5 min |
| `ModifiedPomodoro` | 50 min | 10 min |
| `Ultradian` | 90 min | 20 min |
| `PhysicalCounter` | 40 min | 2 min |
| `TaskBased` | manual, max 120 min | 10 min |
| `LegalCompliance` | 120 min | 15 min |

State machine: `Idle` → `Working` ↔ `OnBreak` (with `Paused` as a cross-cutting state). Events are emitted via an unbounded `Channel<TimerEvent>` as an `IAsyncEnumerable`:

- `BreakDueEvent` → break due (Working → OnBreak)
- `BreakCompletedEvent` + `WorkResumedEvent` → break finished (OnBreak → Working)
- `WorkResumedEvent` → resume after a user pause (Paused → Working)
- `TimerPausedEvent(state, bySystem)` → pause started (user or system suspend)
- `TimerStoppedEvent` → stop

System sleep is consistently treated as a pause: `WindowsPowerModeWatcher` translates `PowerModeChanged` into platform-neutral events, the `TimerService` freezes the remaining time and resumes it after wake-up. A user pause takes precedence over system resume.

Tests: deterministic via `FakeClock` and `FakePowerModeWatcher` in `LookAway.Core.Tests`. The real background loop is quiesced in tests with a high tick interval; phase transitions are triggered via `internal void Tick` (visible via `InternalsVisibleTo`).

## Tray integration and single instance

LookAway runs as a background application with a tray icon (no foreground main window, no taskbar presence):

- `H.NotifyIcon.WinUI` (`TaskbarIcon` control) provides the tray icon
- `TrayIconService` (`internal` in App) binds the context menu to the `ITimerService` and to settings/exit callbacks
- Menu items: "Settings…", "Start break now", "Pause"/"Resume" (state-dependent), "About LookAway", "Exit"
- A double click on the tray icon opens the settings window (not yet filled)
- The main window is hidden at startup (`AppWindow.Hide`) and only becomes visible on user action

Status display: the icon reflects the timer state (working/break/paused/DND), a tooltip shows the live remaining time and active model. The UI-free translation state → icon variant + tooltip lives in `TrayStatusPresenter` (Core) and is testable without the tray control; the `TrayIconService` polls the `ITimerService` once per second via a `DispatcherQueueTimer` and swaps the icon variant (`tray-working/onbreak/paused/disabled.ico`) only on a state change. The timer is started at app launch with the configured model (`BreakModelRegistry.GetEffective`).

Single-instance lock via `SingleInstanceLock` (Core):

- Mutex in the `Local\` namespace per Windows user (`Local\LookAway-{userName}`)
- A second instance detects the running one, signals it via an `EventWaitHandle` (`Local\LookAway-Activate-{userName}`) and exits
- The running instance listens for the event in the background and shows the main window on request
- On a clean exit the mutex is released so a fresh start is possible

## Autostart with Windows

LookAway can optionally start with the Windows login — as a per-user opt-in, without administrator rights:

- The entry is written exclusively under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` (never `HKLM`)
- Entry name `LookAway`, value = the full, quoted path to `LookAway.App.exe` plus `--minimized` (quoting because of possible spaces, e.g. `C:\Program Files\…`)
- The path is determined via `Process.GetCurrentProcess.MainModule.FileName`

The abstraction is in `IAutoStartService` (Core); `RegistryAutoStartService` (Data) is the Windows implementation. Errors (e.g. a Run key locked by group policy) are signaled as `AutoStartException` (Core) so callers can handle them deliberately.

`AutoStartCoordinator` (Core) keeps the setting and the registry in sync:

- **User change → registry:** when the option is toggled, the registry entry is written or removed immediately and the setting is persisted (`SetEnabledAsync`)
- **Startup reconciliation registry → setting:** at app launch the registry is the leading source (`SynchronizeFromRegistryAsync`). A manual change — e.g. disabling via the Task Manager autostart tab — is adopted into the setting
- **Path correction:** if the application was moved, the next launch brings the stored path back up to date. `Enable` is idempotent and only writes when the value differs
- The reconciliation is optional and not startup-critical: if it fails, this is logged and startup continues

Tests: `AutoStartCoordinator` deterministic via `FakeAutoStartService` and `InMemorySettingsRepository` in `LookAway.Core.Tests`; `RegistryAutoStartService` against the real registry in `LookAway.Data.Tests` (a unique entry name per test with cleanup, so no admin rights are needed).

## Break reminder

When a break becomes due (`BreakDueEvent`), LookAway shows a discreet overlay window:

- The UI-free action logic lives in `BreakReminderViewModel` (App) and is testable without WinUI: three actions (start break / 5 min later / skip), timeout default after 30 s = "start break", the first action wins (no override by a double click or a timeout race).
- `BreakReminderWindow` (App/Views) is a standalone, non-minimizable `Window` instance (480×320, centered, color palette indigo `#4361EE` / white / dark gray). `IReminderPresenter`/`ReminderPresenter` (App) create it on the UI thread and prevent stacking (a second message while a window is open is ignored, `_isReminderOpen`).
- The app event loop consumes the `ITimerService.Events` stream; on `BreakDue` the reminder is only shown when no DND is active (`FullscreenDetectionService.TryShowReminder`), otherwise it is deferred. Snooze starts a 5-min work cycle, skip starts the regular one. Texts are placeholders.

## Idle and full-screen detection (DND)

LookAway pauses the timer on prolonged inactivity and suppresses reminders during full-screen apps:

- **Idle:** `IIdleDetector` (Core) → `WindowsIdleDetector` (Data, Win32 `GetLastInputInfo` via `LibraryImport` P/Invoke). `IdleDetectionService` (Core) pauses the timer on inactivity above the threshold (default 5 min, configurable 1–30) and resumes it on returning activity. A self-triggered idle pause is remembered so a user pause is not resumed by mistake.
- **Full-screen/DND:** `IFullscreenDetector` (Core) → `WindowsFullscreenDetector` (Data, `GetForegroundWindow` + monitor comparison, shell/lock screen excluded). `FullscreenDetectionService` (Core) sets the DND state, suppresses due reminders and defers at most one missed reminder until after leaving full-screen mode.
- Both services are evaluated in a background `PeriodicTimer` (5 s, not on the UI thread); the DND state is mirrored to the tray icon (`SetDndActive`). Settings: `PauseOnIdle`, `IdleThresholdMinutes`, `SuppressOnFullscreen`. The platform calls are isolated as P/Invoke in the Data layer; the decision logic is testable via fakes without Win32.

## Settings window

Via the tray menu ("Settings…", double or left click) a WinUI 3 window opens with four areas (pivot): General, Break model, Custom intervals and About LookAway.

- All loading, validation and persistence logic lives in the UI-free `SettingsViewModel`
  (App, `CommunityToolkit.Mvvm`) and is testable without WinUI. The window
  (`SettingsWindow`, App/Views) only binds to it; `ISettingsPresenter`/`SettingsPresenter`
  create it on the UI thread and prevent multiple windows.
- **General:** language (DE/EN/FR), autostart, auto-pause on inactivity including the threshold, DND in full-screen.
- **Break model:** selection from all seven models.
- **Custom intervals:** optional override of the work/break duration; the limits follow the
  model (e.g. PhysicalCounter 30–45 min). Invalid values are flagged and block "Save".
- **Save** closes the window, **Apply** saves without closing, **Cancel** discards.
  Saved changes are applied immediately (`SettingsApplied` → timer restart with the new model,
  idle/full-screen services, tray), not only on the next launch.
- **Autostart** is synchronized with the registry on save via the `AutoStartCoordinator`; if the
  Run key is locked, the remaining settings are still saved.

### Localization (DE/EN/FR)

LookAway is fully trilingual (German, English, French) with runtime language switching: `ILocalizationService` (Core) → `JsonLocalizationService` (Data) provides texts via language-neutral keys from embedded JSON tables (`Localization/<language>.json`).

- All UI texts (settings, wizard, reminder, tray menu and tooltips, break model names and
  exercise hints) use the service. Key convention: `Area.Element` (e.g.
  `Settings.Title`, `Reminder.StartBreak`).
- Switching via the settings window updates the UI immediately (`LanguageChanged` →
  `INotifyPropertyChanged` in the view models, live refresh of the tray menu).
- German is the reference language and the fallback for a missing key (then the key name).
  A consistency test ensures all three tables have exactly the same key set.
- On first launch the language is pre-filled from `CultureInfo.CurrentUICulture` (de/fr, otherwise English).

## First-run wizard

On the very first launch (no `settings.json` present, `Settings.IsFirstRun`) a three-step wizard guides the initial configuration: language, break model and autostart.

- UI-free `WelcomeViewModel` (App, tested state machine: steps forward/back, completion only
  in the last step). `WelcomeWindow` (App/Views, not resizable, centered) binds to it;
  `WelcomePresenter` shows it and reports via `Task<bool>` whether the wizard was completed.
- The starting language is detected from `CultureInfo.CurrentUICulture` (de/fr, otherwise English), the default
  model is ModifiedPomodoro, autostart pre-selected.
- "Finish" saves the configuration (autostart registry-synced) and starts the app into tray mode.
  If the user closes the window without completing it, the app exits and the wizard appears again on the
  next launch.

## Theme and design

The visual design lives in `src/LookAway.App/Themes/` and is merged in `App.xaml`:

- `Tokens.xaml`: everything that is the same in both appearances — spacing (base unit 8 px),
  corner radii (4–6 px), border thickness, `RcFontFamily` (Roboto with system fallback) and the
  font scale (H1 28, H2 22, H3 18, Body 14, Caption 12). **No color value.**
- `Light.xaml` and `Dark.xaml`: the same keys, two sets of values. Surfaces, text, primary colour
  (`RcPrimary`), status colours — plus the framework brushes, so that standard controls do not
  pick up the accent colour configured in Windows.
- `ControlStyles.xaml`: text styles, buttons, cards, filter chips and the card whose border
  changes colour on hover.

The recurring building blocks are controls under `Controls/`: `ListPageHeader` (title, one
explaining line, primary action), `SearchBox` (magnifier, placeholder, clear, Escape empties it)
and `EmptyState`.

**Views bind colours as `ThemeResource`, never as a value.** `StaticResource` freezes the value at
load time, so that spot would stay behind when the appearance changes. Measurements stay
`StaticResource` — they never change at runtime.

`GestaltungslinieGuardTests` in the app test project holds this: no colour value in `Views/` and
`Controls/`, no colour value in `Tokens.xaml`, and the same set of keys in both palettes. A missing
key breaks the binding in one palette only — which otherwise surfaces at the user.

The chosen appearance (`Settings.AppTheme`: `System`, `Light`, `Dark`) is held by `ThemeService`;
the presenters apply it to each window as it is built. WinUI only knows the appearance per element,
not application-wide.

## Sound options

Optionally LookAway plays a discreet tone on a break reminder (default: off):

- Three embedded, self-synthesized PCM WAVs under `Assets/Sounds/` (`chime`, `bell`, `pop`) —
  each under 200 KB, license-free.
- `ISoundService` (Core) → `SoundService` (App, `Windows.Media.Playback.MediaPlayer`). The volume
  is set per playback (no interference with the system volume); playback errors (missing device,
  device change) are logged and swallowed so the app never crashes.
- Settings tab "Sound": enable tone, selection (Chime/Bell/Pop), volume slider (0–100 %, default 30)
  and a "Preview" button. With `SoundVolumePercent = 0` the reminder stays silent.
- The tone is only played when the reminder actually opens — an already-open reminder window does not
  trigger a second tone.

## Statistics, history and CSV export

LookAway records every offered break and shows statistics in the settings tab "Statistics":

- `BreakSession` (Core, validated) with start, end, model and outcome (`Taken`/`Snoozed`/`Skipped`).
  `IBreakHistoryRepository` (Core) → `JsonBreakHistoryRepository` (Data): append-only to
  `%APPDATA%\LookAway\history.json`, written atomically; entries older than 365 days are removed on
  startup.
- `StatisticsService` (Core, UI-free, tested via `IClock`/fakes) aggregates today (count,
  break time, skipped), this week (7 day bars) and this year (12 month bars).
- The tab visualizes the bars with simple `Border` elements (no chart framework) and offers a CSV
  export (`CsvExporter`, UTF-8 with BOM, columns `StartedAt,EndedAt,Duration,Model,Outcome`) via the
  `FileSavePicker`.
- Recording happens when the reminder closes (outcome from the chosen action); for taken breaks the
  break duration is stored as a time span.

## Global hotkeys

LookAway can be operated system-wide via key combinations (enabled by default):

- `Ctrl+Alt+P` → show the break reminder immediately
- `Ctrl+Alt+S` → skip / restart the work cycle
- `Ctrl+Alt+D` → toggle Do Not Disturb manually (the tray icon reflects the state)

`IHotkeyService` (Core) → `WindowsHotkeyService` (Data) uses the Win32 API `RegisterHotKey` on a
dedicated background thread with a message-only window (`HWND_MESSAGE`) and a message loop.
`HotkeyValidator`/`HotkeyDefaults` (Core, tested) provide validation (modifier + key, collision
check) and the default mapping. The bindings are persisted in the settings; the settings tab
"Hotkeys" allows enabling and resetting to the default values. Failed individual registrations
(conflict with another app) are logged without preventing the others; on exit all hotkeys are
released.

## Update check

LookAway optionally checks for new versions via the GitHub releases API (enabled by default):

- `IUpdateChecker` (Core) → `GitHubUpdateChecker` (Data) fetches `releases/latest` and compares the
  tag with the installed version. The HTTP access is encapsulated behind `IHttpGetClient` (→ `HttpGetClient`,
  user agent + 10 s timeout) and is therefore testable; network/parse errors yield "no update"
  instead of an exception. The version comparison logic lives in the UI-free `UpdateInfo` (a leading `v`
  and prerelease suffix are stripped).
- `UpdateSchedule` (Core) decides, based on the frequency (`OnStartup`/`Daily`/`Weekly`) and the
  last check time, whether to check at startup — sparing the GitHub rate limit.
- Settings (About tab): enable, frequency, "Check now" with a status display and a download link.
  If the background check finds an update at startup, the tray shows the entry "Download update",
  which opens the release page in the browser (no auto-install).

## Break actions

Optionally LookAway reinforces the break character (both opt-in and reversible):

- **Dim the screen:** `IScreenDimmer` (Core) → `WindowsScreenDimmer` (App) lowers the brightness of
  DDC/CI-capable monitors (Dxva2) and restores it at the end of the break. On hardware without DDC/CI
  (many laptops) the call has no effect; errors are swallowed and logged.
- **Pause media:** `IMediaController` (Core) → `WindowsMediaController` (App) pauses all running
  playback sessions via the SMTC API and resumes only the previously running ones at the end of the break.
- The UI-free `PauseActionService` (Core, tested) coordinates both based on the settings
  (`BeginBreakAsync`/`EndBreakAsync`); the app calls it at the break start (a taken break) and on the
  `BreakCompletedEvent`. Settings tab "Break actions": dim + brightness (10–80 %), pause media + resume
  after the break.

## Distribution and installation

LookAway is distributed as a portable ZIP and an MSIX package. The version number is
maintained centrally in `Directory.Build.props` (`<Version>`); the CI can override it on a tag push via
`-p:Version=…`.

### Portable

```powershell
./tools/publish-portable.ps1 -Version 1.0.0
```

The script runs a self-contained `dotnet publish` (win-x64), places the marker `portable.flag` next to
the EXE and packs everything as `dist/LookAway-Portable-v<Version>.zip`. If the `portable.flag` is next
to `LookAway.exe`, the app stores configuration, history and logs **next to the EXE** instead of under
`%APPDATA%\LookAway` (`AppDataLocation` / `AppPaths.ResolveDataDirectory`, tested).

### MSIX

The MSIX package is produced via the Windows packaging tools (Visual Studio "Create package" or
`msbuild /p:WindowsPackageType=MSIX /p:GenerateAppxPackageOnBuild=true`). For local tests a self-signed
certificate with a unique publisher CN is sufficient; for production a code-signing certificate from a
trusted CA is required. Capabilities remain minimal (no `broadFileSystemAccess`).

### CI release

On a tag push `v*.*.*` the CI, after the green `build-test` job, builds both distribution artifacts and
attaches them to the GitHub release (`.github/workflows/ci.yml`, job `release`):

| Artifact | Built by | Signature |
|----------|----------|-----------|
| `LookAway-Portable-v<version>.zip` | `tools/publish-portable.ps1` | `.zip.sig`, if the secret `LOOKAWAY_SIGNING_KEY` is present |
| `LookAway-Setup-v<version>.exe` | `tools/publish-setup.ps1` (Inno Setup from the runner) | none — SHA-256 in the release text |

The Setup.exe deliberately gets **no** `.sig`: the updater picks the first `.sig` of a release
(`GitHubUpdateChecker.FindSignatureAssetUrl`) and could mistake a second one for the ZIP's signature.
Both hashes go into the release text instead, which the job puts in front of the generated notes.

## Review

`tools/review.ps1` orchestrates local quality checks:

```powershell
./tools/review.ps1 -Mode build       # build only
./tools/review.ps1 -Mode test        # build + tests
./tools/review.ps1 -Mode security    # secret scan + sensitive patterns
./tools/review.ps1 -Mode docs        # README version against Directory.Build.props
./tools/review.ps1 -Mode all         # build + tests + security + docs
```

The script paths are relative to the solution root.

Mode `docs` calls `tools/check-readme-version.ps1`: the three READMEs state a version in their "latest
release" section that is derived from nothing — it read v1.1.1 while v1.2.8 was published. The check
compares it against `Directory.Build.props` and also runs in CI.

## Continuous integration

`.github/workflows/ci.yml` runs on every push/pull request against `main` and on manual `workflow_dispatch`:

| Step | What | Why |
|------|-----|-------|
| `actions/checkout` | Load the source | Standard |
| `actions/setup-dotnet` | .NET 10 SDK | Solution target |
| `actions/cache` | NuGet cache per `csproj` hash | Build speed-up |
| `dotnet workload restore` | Windows App SDK workloads | WinUI 3 |
| `dotnet build -c Release` | Compile with `TreatWarningsAsErrors` | Quality gate |
| `dotnet test --logger trx --collect "XPlat Code Coverage"` | xUnit + coverage | Substance gate |
| `tools/review.ps1 -Mode security` | Pattern scan | Additional heuristic (secrets, BinaryFormatter, etc.) |
| `actions/upload-artifact` | Upload trx + coverage | Detailed analysis |
| `dorny/test-reporter` | Test summary in the PR | Visibility |

All GitHub Actions are pinned to a commit SHA (supply-chain hardening). Runner: `windows-latest`
(mandatory because of WinUI 3). The concurrency group cancels older runs per ref. Timeout 15 minutes.

## License

MIT License – see [LICENSE](../LICENSE).
