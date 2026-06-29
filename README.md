<div align="center">

<img src="src/LookAway.App/Assets/LookAwayLogo.png" alt="LookAway" width="120" />

# LookAway

**Bildschirmpausen, intelligent erinnert.**

[![CI](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ReneSchustek/LookAway?sort=semver)](https://github.com/ReneSchustek/LookAway/releases/latest)

**Deutsch** · [English](README.en.md) · [Français](README.fr.md)

</div>

LookAway ist eine schlanke Windows-Tray-Anwendung, die dezent an Bildschirmpausen erinnert.
Sie laeuft unauffaellig im Hintergrund, bietet mehrere wissenschaftlich fundierte Pausenmodelle und
ist vollstaendig pro Windows-Benutzer konfigurierbar – fuer alle, die viel am Bildschirm arbeiten und
ihre Augen, Haltung und Konzentration schonen moechten.

## Aktuelles Release

**[v1.0.2](https://github.com/ReneSchustek/LookAway/releases/latest)** ist die aktuelle Version.
Sie bringt einen **Setup.exe-Installer** mit frei waehlbarem Speicherort, einen zuverlaessigen Start
im portablen und im MSIX-Betrieb, den **abgedunkelten Pausen-Screen** (mit ESC beendbar) und das neue
App-Icon. Drei Installationsvarianten stehen bereit – siehe [Installation](#installation).

## Funktionen

- **7 Pausenmodelle** – Pomodoro, Modifiziertes Pomodoro, Ultradianer Rhythmus, Physical Counter,
  Kurze Pausen, Aufgabenbasiert und gesetzliche Empfehlung
- **Auto-Pause und Nicht stoeren** – pausiert bei Inaktivitaet und unterdrueckt Erinnerungen waehrend
  Vollbild-Apps (Praesentationen, Filme, Spiele)
- **Dreisprachig** – Deutsch, Englisch, Franzoesisch, live umschaltbar
- **Statistiken und CSV-Export** – Pausen pro Tag, Woche und Jahr, exportierbar
- **Globale Hotkeys** – Pause starten, ueberspringen, Nicht stoeren – von ueberall
- **Optionaler Ton** – dezenter Hinweis bei der Erinnerung (aus drei Toenen waehlbar)
- **Abgedunkelter Pausen-Screen** – ein ruhiger Vollbild-Overlay verdeckt den Bildschirm waehrend der
  Pause, zeigt den Countdown und das Uebungs-Ziel und laesst sich jederzeit mit **ESC** vorzeitig beenden
- **Pause-Aktionen** – Bildschirm dimmen (DDC/CI) und Medienwiedergabe waehrend der Pause pausieren
- **Autostart** – startet auf Wunsch automatisch mit Windows

## Voraussetzungen

- Windows 10 (Version 1809) oder neuer / Windows 11

## Installation

Alle Artefakte liegen auf der [Releases-Seite](https://github.com/ReneSchustek/LookAway/releases/latest)
(aktuell **v1.0.2**).

### Variante A: Setup.exe (komfortabel)

1. `LookAway-Setup-v1.0.2.exe` herunterladen und ausfuehren.
2. Im Assistenten den **Speicherort frei waehlen** und „nur fuer mich" oder „fuer alle Benutzer"
   bestimmen. LookAway landet im Startmenue (optional Desktop-Verknuepfung/Autostart) und startet ins Tray.

> Hinweis: Die Setup.exe ist nicht mit einem CA-Zertifikat signiert – Windows SmartScreen kann warnen
> („Weitere Informationen" → „Trotzdem ausfuehren").

### Variante B: Portable (ohne Installation)

1. `LookAway-Portable-v1.0.2.zip` herunterladen und in einen beliebigen Ordner entpacken.
2. `LookAway.exe` starten. Im Portable-Modus liegen alle Daten neben der EXE – ideal fuer den USB-Stick.

### Variante C: MSIX

Das MSIX ist mit einem **selbstsignierten** Zertifikat signiert. Damit Windows die Installation
zulaesst, muss das mitgelieferte Zertifikat einmalig als vertrauenswuerdig importiert werden:

1. `LookAway-v1.0.2.cer` und `LookAway-v1.0.2-x64.msix` herunterladen.
2. In einer **Administrator**-PowerShell das Zertifikat importieren:
   ```powershell
   Import-Certificate -FilePath .\LookAway-v1.0.2.cer -CertStoreLocation Cert:\LocalMachine\Root
   ```
3. Doppelklick auf die `.msix` und der Installationsanweisung folgen. LookAway erscheint danach im
   Startmenue und startet ins Tray.

## Erste Schritte

Beim ersten Start fuehrt ein kurzer Assistent in drei Schritten durch die Einrichtung: Sprache,
Pausenmodell und Autostart. Danach lebt LookAway im Tray – ein Klick auf das Symbol oeffnet das Menue,
ein Doppelklick die Einstellungen.

## Konfiguration

Alle Optionen finden sich im Einstellungsfenster (Tray-Menue → „Einstellungen…"): Sprache, Autostart,
Pausenmodell, eigene Intervalle, Sound, Pause-Aktionen, Hotkeys, Statistik und Update-Pruefung.

## Pausenmodelle

| Modell | Arbeit | Pause | Empfohlen fuer |
|---|---|---|---|
| Kurze Pausen | 60 min | 5 min | Lange, ruhige Arbeitsphasen |
| Klassisches Pomodoro | 25 min | 5 min | Fokussiertes Arbeiten in Etappen |
| Modifiziertes Pomodoro | 50 min | 10 min | Laengere Konzentrationsbloecke |
| Ultradianer Rhythmus | 90 min | 20 min | Tiefe Arbeit nach dem natuerlichen Rhythmus |
| Physical Counter | 40 min | 2 min | Haltung und Mikropausen |
| Aufgabenbasiert | bis 120 min | 10 min | Arbeiten bis zum Meilenstein |
| Gesetzliche Empfehlung | 120 min | 15 min | Bildschirmarbeit nach Vorgabe |

## Hotkeys (Standard)

| Aktion | Tastenkombination |
|---|---|
| Pause starten | `Strg + Alt + P` |
| Ueberspringen / Snooze | `Strg + Alt + S` |
| Nicht stoeren umschalten | `Strg + Alt + D` |

Die Hotkeys lassen sich in den Einstellungen aktivieren oder auf die Standardwerte zuruecksetzen.

## Datenschutz

LookAway ist datensparsam und arbeitet vollstaendig lokal:

- **Was gespeichert wird:** Einstellungen, Pausen-Historie und Logdateien – ausschliesslich auf dem
  eigenen Geraet unter `%APPDATA%\LookAway` (bzw. neben der EXE im Portable-Modus).
- **Was nicht passiert:** Es werden keine Nutzungsdaten, keine Telemetrie und keine persoenlichen
  Daten an Server gesendet.
- **Einzige Netzwerkverbindung:** Die optionale Update-Pruefung fragt die oeffentliche
  GitHub-Releases-API ab, ob eine neuere Version vorliegt. Sie laesst sich in den Einstellungen
  abschalten.

## Screenshots

Die aktuellen Screenshots (Tray-Icon, Pause-Erinnerung, Einstellungen, Statistik) liegen unter
[`docs/screenshots/`](docs/screenshots/).

## Changelog

Die Versionshistorie steht in [`CHANGELOG.md`](CHANGELOG.md).

## Fuer Entwickler

Architektur, Build, Tests und die internen Details sind in [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md)
beschrieben.

## Lizenz

Proprietaer – alle Rechte vorbehalten.
