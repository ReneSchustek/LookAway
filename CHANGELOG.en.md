# Changelog

[Deutsch](CHANGELOG.md) · **English** · [Français](CHANGELOG.fr.md)

All notable changes to LookAway are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versioning follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **Light and dark appearance.** Under "General" you can choose whether windows appear light or
  dark; "Match Windows" follows the system setting. The choice takes effect immediately in the
  open window.

- **Log inside the app.** A new section shows what the application recorded most recently — with
  a search across the message text and filters for level and period. Until now these entries only
  existed as a file in the data folder, which you had to find first.

- **Search and filters for the break models.** The models are shown as cards with a search box and
  filter chips. Each card says what to do during that break and how many breaks have come out of it.

### Changed

- **Consistent appearance.** Windows, cards, buttons, check boxes and sliders now follow one shared
  set of colours. Previously some controls picked up the accent colour configured in Windows — a
  different one on every machine.

- **Links are underlined** and therefore recognisable as links even when colours are hard to tell
  apart.

- **New application icon** in the side menu, the window and the program icon.

- Empty lists now say whether there is nothing at all or whether the input simply matches nothing.
  In the second case a button resets search and filters.

## [1.2.9] – 2026-08-02

### Added

- **Optional way to support the project:** the About page in the settings now shows a link below
  version, licence and documentation for buying the developer a coffee. Entirely optional and
  without any effect on the feature set.

- **Setup.exe is attached to every release again:** since 1.2.5 the releases carried only the portable
  ZIP, so the installer had to be compiled by hand. It is now built during the release run and uploaded
  along with the ZIP. The release text lists the SHA-256 of both files. Only the ZIP is signed — the
  updater picks the first signature of a release, and a second one could be mistaken for the package's
  and fail the check.

## [1.2.8] – 2026-07-11

### Fixed

- **Record of an applied update was left behind:** After an update was applied, the settings kept
  its version and checksum. Harmless, but every start went looking for a package that had long
  been cleaned up. The record is now discarded as soon as no applicable package belongs to it.

## [1.2.7] – 2026-07-11

### Fixed

- **Automatic update did not install the package.** The update was verified and downloaded but
  never applied — the old version stayed installed. The helper process that swaps the program
  files starts from the staging folder and derived two things wrongly from that:
  - **Its data location:** Because the package contains a portable marker, it considered itself
    a portable installation and looked for its settings next to itself. There it could not find
    the recorded file hash it validates the package against — and rejected its own update. It
    now uses the data location of the installation it serves. In addition, the portable marker
    is no longer staged at all.
  - **Its target:** It used its own program folder (the staging folder) as the target, copying
    onto itself. It now uses the installation folder passed to it and validates it beforehand
    (an existing, writable installation outside the staging area).

## [1.2.6] – 2026-07-10

### Added

- **Automatic break start:** The break now starts automatically after a configurable delay
  (default 15 seconds, in 5-second steps up to 3 minutes) if the reminder is left untouched.
  A countdown in the reminder window shows the remaining time. It can be enabled or disabled
  in the settings — when disabled, the reminder stays open until you choose an action.

## [1.2.5] – 2026-07-02

### Changed

- **Media pause hint:** The break actions now include a hint explaining which players are
  paused automatically. Only applications that integrate with the Windows media controls
  (SMTC) can be controlled (e.g. Spotify, the Music app, and playback in Chrome, Edge and
  Firefox). **VLC does not support this** and will not be paused.

## [1.2.4] – 2026-07-01

### Changed

- **Overlay transparency removed:** The alpha/opacity slider on the overlay colour
  picker is gone, along with the misleading hint ("how much the screen shows through").
  True window transparency is not reliably achievable in WinUI 3; the overlay covers
  the screen opaquely. A previously semi-transparent colour is automatically migrated
  to its visually equivalent opaque colour — the appearance does not change.

## [1.2.3] – 2026-07-01

### Fixed

- **Break overlay shows again:** With "darken all screens" enabled (the default),
  the overlay was not displayed when a break started. The cause was an
  `InvalidCastException` while enumerating the monitor list from
  `DisplayArea.FindAll()` (a WinRT projection whose `IIterable` query fails in
  CsWinRT) — on older builds this even crashed the app when clicking "Start break".
  The monitor list is now copied into a managed array by index; the overlay covers
  all monitors again. (The safeguard added in 1.2.2 still catches any failure
  instead of leaving the app stuck.)

### Changed

- **Break content only on the primary monitor:** With "darken all screens", the
  title, hint, and countdown now appear only on the primary monitor; additional
  monitors are merely darkened (empty overlay). The ESC shortcut still ends the
  break from any monitor.
- **Better automatic overlay text color:** The high-contrast text color now follows
  the actually visible overlay color (the semi-transparent color composited over a
  light background, judged by perceived brightness). A semi-transparent black — which
  appears grey — therefore gets dark text instead of poorly readable light text.

## [1.2.2] – 2026-07-01

### Fixed

- **Automatic update no longer gets stuck:** An update package that was already
  downloaded and verified (signature and hash) is **no longer re-downloaded and
  re-extracted on every launch**. Previously each startup overwrote the extracted
  `LookAway.exe`; a freshly written, unsigned file is scanned by the virus scanner
  on first execution, which can briefly block the helper launch with "Access
  denied". That kept the package perpetually "cold" and the update never got
  applied. The staged package now stays in place and is applied on the next start.
- **More robust break start:** If building the break overlay or the reminder window
  fails, the app stays usable: state is reset cleanly, brightness/media are
  restored, and the timer keeps running — instead of getting stuck in a "break in
  progress" state.

## [1.2.1] – 2026-06-30

### Fixed

- **The timer is no longer reset unnecessarily:** saving unrelated settings
  (language, sound, overlay colour, update frequency …) no longer restarts the
  running work countdown. The countdown also survives a restart **within the same
  Windows session** (e.g. an update) and continues where it left off instead of
  starting over. A Windows restart (new session) resets it as expected; the reset
  after standby/screen-off is unchanged.

### Added

- **One-click install:** when "Check for updates" finds a package, it now offers an
  **"Install now"** button directly. The update is downloaded, signature-verified
  and applied on next start — no detour via the GitHub release page (which remains
  visible as a manual fallback).

### Changed

- Internal quality polish: full comment/principles audit across all layers, dead code
  removed, consistent correct umlauts including project-file comments.

## [1.2.0] – 2026-06-30

### Added

- **Update authenticity (release signing):** update packages are verified against a
  detached **ECDSA P-256 / SHA-256** signature before extraction or apply (`*.sig`
  asset against the embedded public key, fail-closed). A compromised release channel
  cannot forge a valid signature without the offline private key. Tools
  `tools/new-signing-key.ps1` and `tools/sign-release.ps1`; CI signs via the
  `LOOKAWAY_SIGNING_KEY` secret.

### Changed

- **Layer purity:** the single-instance lock now sits behind the Core interface
  `ISingleInstanceLock` and, together with the Windows adapters
  (`WindowsScreenDimmer`, `WindowsMediaController`), moved into the Data layer;
  `LookAway.Application` is now platform-neutral (`net10.0`).
- Both JSON repositories share a common `JsonFileStore`; corrupt
  `settings.json`/`history.json` are backed up as `*.corrupt` before being replaced.
- Hotkey labels are localised via `ILocalizationService` (Strg/Ctrl, Umschalt/
  Shift/Maj); `SettingsViewModel` split by concern (hotkeys/updates).

### Fixed

- Consistent correct umlauts (ä/ö/ü/ß) across comments, text and test names.

## [1.1.1] – 2026-06-30

### Fixed

- In-app license display (About) now correctly shows **MIT** instead of "Proprietary".
- **Security:** a pending update is verified by version **and the SHA-256 of its
  executable** before being applied — a folder merely planted under
  `%LOCALAPPDATA%\…\updates\` is no longer executed. The zip-bomb guard now limits
  the **actually written** bytes instead of the size declared in the ZIP.
- No more duplicate break reminders when the timer and a user action fire at the
  same time (display now runs thread-safely on the UI thread).
- Robustness: the timer loop captures its cancellation token locally, overlay
  visibility is `volatile`, aborted partial downloads are cleaned up, and
  colliding log event IDs were reassigned.

## [1.1.0] – 2026-06-30

### Added

- Break screen across **multiple monitors**: optionally every connected display
  is covered with its own overlay during a break (option "Darken all screens",
  default: on). Works independently of DDC/CI — so on laptops too.
- **Freely selectable break screen colour** including transparency (opacity/alpha
  slider) via a colour picker in the settings.
- **Automatic updates**: when a new version is available, LookAway can install it
  itself — the new portable package is downloaded, the program files are swapped
  after the app closes and it restarts. New **"Update automatically"** setting:
  downloads the latest version in the background and installs it on the next start
  with no interaction. Without it, a single click on the tray "Update" entry suffices.

### Changed

- Modernised settings: the top tab strip was replaced with a collapsible
  **side menu** (NavigationView with a hamburger button).
- New light **mint/teal theme** (eye-friendly) across the whole interface.
- After **sleep or inactivity** (e.g. a phone call) the work timer restarts fresh
  if you were away at least as long as a break — your eyes have already rested by
  then. Short interruptions keep counting down the remaining time as before; a
  manual pause is unaffected.
- The tray "Update" entry no longer just opens the release page — it downloads the
  new version and installs it automatically.

## [1.0.2] – 2026-06-29

### Fixed

- Fixed a startup crash in unpackaged mode: the tray icon was passed to H.NotifyIcon
  as a PNG and threw an exception; it is now a DIB ICO. In addition, the publish was
  missing the resource index (PRI) and loose asset files (XamlParseException /
  DirectoryNotFoundException) — the app now starts reliably. The portable ZIP and the
  MSIX are therefore fully runnable for the first time.

### Added

- Setup.exe installer (Inno Setup): freely selectable install location, installation
  for the current or all users, Start menu / optional desktop shortcut, optional
  autostart and an uninstaller. Self-contained — no pre-installed .NET / Windows App
  SDK runtime required.

### Changed

- Distributable builds are self-contained (Windows App SDK), the CI pipeline is
  hardened (green run, SHA-pinned actions, node24) and the Git history was cleaned up.

## [1.0.1] – 2026-06-29

### Added

- Dimmed full-screen break screen: covers the screen during the break, shows the
  countdown and the exercise goal and can be ended early with **ESC**
- EXE application icon (Explorer, taskbar, Alt+Tab) from the LookAway logo

### Changed

- Tile and Store logos (MSIX) regenerated from the LookAway logo

## [1.0.0] – 2026-06-28

First complete version.

### Added

- Tray application with single-instance lock and a status icon plus live tooltip
- Timer engine with seven break models and sleep-resilient state
- Break reminder as a discreet overlay window (start break / snooze / skip)
- Auto-pause on inactivity and Do-Not-Disturb mode for full-screen apps
- Settings window (General, Break model, Custom intervals, Sound, Break actions,
  Hotkeys, Statistics, Update, About)
- First-run wizard for the initial configuration
- Trilingual (German, English, French) with runtime language switching
- Central theme (color palette, typography, button styles)
- Optional reminder sound with selection, volume and preview
- Statistics (today, week, year) with CSV export
- Global hotkeys for break, snooze and Do Not Disturb
- Update check via the GitHub releases API
- Break actions: dim the screen and pause media playback
- Autostart with Windows via the per-user Run entry
- Distribution as a portable ZIP and an MSIX package

[Unreleased]: https://github.com/ReneSchustek/LookAway/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/ReneSchustek/LookAway/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/ReneSchustek/LookAway/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/ReneSchustek/LookAway/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/ReneSchustek/LookAway/releases/tag/v1.0.0
