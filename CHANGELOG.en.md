# Changelog

[Deutsch](CHANGELOG.md) · **English** · [Français](CHANGELOG.fr.md)

All notable changes to LookAway are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versioning follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Break screen across **multiple monitors**: optionally every connected display
  is covered with its own overlay during a break (option "Darken all screens",
  default: on). Works independently of DDC/CI — so on laptops too.
- **Freely selectable break screen colour** including transparency (opacity/alpha
  slider) via a colour picker in the settings.

### Changed

- Modernised settings: the top tab strip was replaced with a collapsible
  **side menu** (NavigationView with a hamburger button).
- New light **mint/teal theme** (eye-friendly) across the whole interface.

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

[Unreleased]: https://github.com/ReneSchustek/LookAway/compare/v1.0.2...HEAD
[1.0.2]: https://github.com/ReneSchustek/LookAway/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/ReneSchustek/LookAway/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/ReneSchustek/LookAway/releases/tag/v1.0.0
