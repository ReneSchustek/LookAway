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

**[v1.0.2](https://github.com/ReneSchustek/LookAway/releases/latest)** is the current version.
It adds a **Setup.exe installer** with a freely selectable install location, reliable startup in both
portable and MSIX mode, the **dimmed break screen** (dismissible with ESC) and the new app icon.
Three installation options are available — see [Installation](#installation).

## Features

- **7 break models** – Pomodoro, Modified Pomodoro, Ultradian Rhythm, Physical Counter,
  Short Breaks, Task-based and the legal recommendation
- **Auto-pause and Do Not Disturb** – pauses on inactivity and suppresses reminders during
  full-screen apps (presentations, movies, games)
- **Trilingual** – German, English, French, switchable at runtime
- **Statistics and CSV export** – breaks per day, week and year, exportable
- **Global hotkeys** – start break, skip, toggle Do Not Disturb – from anywhere
- **Optional sound** – a discreet cue on the reminder (choose from three tones)
- **Dimmed break screen** – a calm full-screen overlay covers the screen during the break,
  shows the countdown and the exercise goal, and can be ended early at any time with **ESC**
- **Break actions** – dim the screen (DDC/CI) and pause media playback during the break
- **Autostart** – optionally starts automatically with Windows

## Requirements

- Windows 10 (version 1809) or newer / Windows 11

## Installation

All artifacts are on the [releases page](https://github.com/ReneSchustek/LookAway/releases/latest)
(currently **v1.0.2**).

### Option A: Setup.exe (convenient)

1. Download and run `LookAway-Setup-v1.0.2.exe`.
2. In the wizard, **choose the install location freely** and decide between "just for me" or
   "for all users". LookAway is added to the Start menu (optional desktop shortcut/autostart) and
   starts into the tray.

> Note: the Setup.exe is not signed with a CA certificate – Windows SmartScreen may warn
> ("More info" → "Run anyway").

### Option B: Portable (no installation)

1. Download `LookAway-Portable-v1.0.2.zip` and extract it into any folder.
2. Run `LookAway.exe`. In portable mode all data lives next to the EXE – ideal for a USB stick.

### Option C: MSIX

The MSIX is signed with a **self-signed** certificate. For Windows to allow the installation, the
bundled certificate has to be trusted once:

1. Download `LookAway-v1.0.2.cer` and `LookAway-v1.0.2-x64.msix`.
2. In an **administrator** PowerShell, import the certificate:
   ```powershell
   Import-Certificate -FilePath .\LookAway-v1.0.2.cer -CertStoreLocation Cert:\LocalMachine\Root
   ```
3. Double-click the `.msix` and follow the installation prompt. LookAway then appears in the Start
   menu and starts into the tray.

## Getting started

On first launch a short three-step wizard guides you through the setup: language, break model and
autostart. After that LookAway lives in the tray – a single click on the icon opens the menu, a
double click opens the settings.

## Configuration

All options live in the settings window (tray menu → "Settings…"): language, autostart, break model,
custom intervals, sound, break actions, hotkeys, statistics and update check.

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

The current screenshots (tray icon, break reminder, settings, statistics) are under
[`docs/screenshots/`](docs/screenshots/).

## Changelog

The version history is in [`CHANGELOG.md`](CHANGELOG.md).

## For developers

Architecture, build, tests and the internal details are described in
[`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).

## License

Proprietary – all rights reserved.
