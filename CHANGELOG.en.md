# Changelog

[Deutsch](CHANGELOG.md) · **English** · [Français](CHANGELOG.fr.md)

All notable changes to LookAway are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versioning follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

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
