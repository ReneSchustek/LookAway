<div align="center">

<img src="src/LookAway.App/Assets/LookAwayLogo.png" alt="LookAway" width="120" />

# LookAway

**Screen breaks, intelligently reminded.**

[![CI](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ReneSchustek/LookAway?sort=semver)](https://github.com/ReneSchustek/LookAway/releases/latest)

[Deutsch](README.md) · **English** · [Français](README.fr.md)

</div>

LookAway is a lightweight Windows tray application that discreetly reminds you to take screen breaks.
It runs quietly in the background, offers several scientifically grounded break models and is fully
configurable per Windows user — for everyone who works a lot at the screen and wants to protect their
eyes, posture and focus.

## Latest release

**[v1.1.1](https://github.com/ReneSchustek/LookAway/releases/latest)** is the current version.
Highlights of the 1.1 line:

- **Break screen across multiple monitors** – optionally every display is dimmed.
- **Freely selectable break colour** including transparency.
- **Modern side menu** in the settings and a light, eye-friendly mint/teal design.
- **Automatic updates** – LookAway downloads and installs new versions itself (verified via version
  and file hash).

## Features

- **7 break models** – Pomodoro, Modified Pomodoro, Ultradian Rhythm, Physical Counter,
  Short Breaks, Task-based and the legal recommendation
- **Auto-pause and Do Not Disturb** – pauses on inactivity and suppresses reminders during
  full-screen apps (presentations, movies, games)
- **Timer reset after being away** – after sleep or longer inactivity (e.g. a phone call) the work
  timer restarts fresh, since your eyes have already rested
- **Dimmed break screen** – a calm full-screen overlay covers the screen during the break, shows the
  countdown and the exercise goal, and can be ended at any time with **ESC**; optionally on **all
  monitors** and in a **freely selectable colour**
- **Break actions** – dim the screen (DDC/CI) and pause media playback during the break
- **Automatic updates** – optional check via the GitHub releases API; on request LookAway downloads
  and installs new versions itself (see [Updates](#updates))
- **Trilingual** – German, English, French, switchable at runtime
- **Statistics and CSV export** – breaks per day, week and year, exportable
- **Global hotkeys** – start break, skip, toggle Do Not Disturb – from anywhere
- **Optional sound** – a discreet cue on the reminder (choose from three tones)
- **Autostart** – optionally starts automatically with Windows

## Requirements

- Windows 10 (version 1809) or newer / Windows 11

## Installation

The ready-to-run application is published as a **portable ZIP** on the
[releases page](https://github.com/ReneSchustek/LookAway/releases/latest).

### Portable (recommended)

1. Download `LookAway-Portable-<version>.zip` from the releases page and extract it into any folder.
2. Run `LookAway.exe`. In portable mode all data lives next to the EXE – ideal for a USB stick.

> Note: the builds are not signed with a CA certificate – Windows SmartScreen may warn on first run
> ("More info" → "Run anyway").

### Setup.exe or MSIX (optional, build it yourself)

A convenient **Setup.exe installer** (Inno Setup, freely selectable location, Start-menu entry,
optional autostart) and an **MSIX package** can be built locally – the automatic release deliberately
contains only the portable ZIP:

```powershell
# Setup.exe (requires Inno Setup):
tools\publish-setup.ps1 -Version <version>

# MSIX package:
msbuild src\LookAway.App\LookAway.App.csproj -p:Configuration=Release -p:Platform=x64 `
  -p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true
```

## Getting started

On first launch a short three-step wizard guides you through the setup: language, break model and
autostart. After that LookAway lives in the tray – a single click on the icon opens the menu, a
double click opens the settings.

## Configuration

All options live in the settings window (tray menu → "Settings…") in the collapsible side menu:
General, break model, custom intervals, sound, break (incl. screen dimming and colour picker),
hotkeys, statistics and About (with update options).

## Updates

LookAway can keep itself up to date:

- When the update check is enabled and a new version is available, the tray shows an "Update" entry.
- A click downloads the new portable ZIP, swaps the program files after closing and restarts.
- With the **"Update automatically"** option this happens in the background and is applied on the next
  start, with no interaction.
- Before applying, the version and SHA-256 of the downloaded file are verified; the download runs over
  HTTPS from GitHub only. The automatic swap works for portable and per-user installations (for
  "all users" LookAway opens the release page instead).

## Break models

| Model | Work | Break | Recommended for |
|---|---|---|---|
| Short Breaks | 60 min | 5 min | Long, calm work phases |
| Classic Pomodoro | 25 min | 5 min | Focused work in stages |
| Modified Pomodoro | 50 min | 10 min | Longer concentration blocks |
| Ultradian Rhythm | 90 min | 20 min | Deep work following the natural rhythm |
| Physical Counter | 40 min | 2 min | Posture and micro-breaks |
| Task-based | up to 120 min | 10 min | Working until a milestone |
| Legal recommendation | 120 min | 15 min | Screen work per regulation |

## Hotkeys (default)

| Action | Shortcut |
|---|---|
| Start break | `Ctrl + Alt + P` |
| Skip / Snooze | `Ctrl + Alt + S` |
| Toggle Do Not Disturb | `Ctrl + Alt + D` |

The hotkeys can be enabled in the settings or reset to their default values.

## Privacy

LookAway is data-frugal and works entirely locally:

- **What is stored:** settings, break history and log files – exclusively on your own device under
  `%APPDATA%\LookAway` (or next to the EXE in portable mode).
- **What does not happen:** no usage data, no telemetry and no personal data are sent to any server.
- **Only network connection:** the optional update check queries the public GitHub releases API for a
  newer version. It can be disabled in the settings.

## Screenshots

Screenshots of the interface (tray icon, break reminder, settings, statistics) are collected under
[`docs/screenshots/`](docs/screenshots/).

## Changelog

The version history is in [`CHANGELOG.md`](CHANGELOG.md).

## For developers

Architecture, build, tests and the internal details are described in
[`docs/DEVELOPMENT.en.md`](docs/DEVELOPMENT.en.md).

## License

MIT License – see [LICENSE](LICENSE).
