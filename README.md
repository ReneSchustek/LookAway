<div align="center">

<img src="src/LookAway.App/Assets/LookAwayLogo.png" alt="LookAway" width="120" />

# LookAway

**Bildschirmpausen, intelligent erinnert.**

[![CI](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ReneSchustek/LookAway?sort=semver)](https://github.com/ReneSchustek/LookAway/releases/latest)

**Deutsch** · [English](README.en.md) · [Français](README.fr.md)

</div>

LookAway ist eine schlanke Windows-Tray-Anwendung, die dezent an Bildschirmpausen erinnert.
Sie läuft unauffällig im Hintergrund, bietet mehrere wissenschaftlich fundierte Pausenmodelle und
ist vollständig pro Windows-Benutzer konfigurierbar – für alle, die viel am Bildschirm arbeiten und
ihre Augen, Haltung und Konzentration schonen möchten.

## Aktuelles Release

**[v1.1.1](https://github.com/ReneSchustek/LookAway/releases/latest)** ist die aktuelle Version. Highlights
der 1.1-Reihe:

- **Pausen-Screen auf mehreren Monitoren** – auf Wunsch wird jeder Bildschirm abgedunkelt.
- **Frei wählbare Pausen-Farbe** inklusive Transparenz.
- **Modernes Seitenmenü** in den Einstellungen und ein helles, augenfreundliches Mint/Teal-Design.
- **Automatische Aktualisierung** – LookAway lädt neue Versionen selbst herunter und installiert sie
  (verifiziert über Version und Datei-Hash).

## Funktionen

- **7 Pausenmodelle** – Pomodoro, Modifiziertes Pomodoro, Ultradianer Rhythmus, Physical Counter,
  Kurze Pausen, Aufgabenbasiert und gesetzliche Empfehlung
- **Auto-Pause und Nicht stören** – pausiert bei Inaktivität und unterdrückt Erinnerungen während
  Vollbild-Apps (Präsentationen, Filme, Spiele)
- **Timer-Reset nach Abwesenheit** – nach Standby oder längerer Inaktivität (z. B. Telefonat) startet
  die Arbeitszeit frisch, denn die Augen haben ohnehin pausiert
- **Abgedunkelter Pausen-Screen** – ein ruhiges Vollbild-Overlay verdeckt den Bildschirm während der
  Pause, zeigt den Countdown und das Übungs-Ziel und lässt sich jederzeit mit **ESC** beenden;
  optional auf **allen Monitoren** und in **frei wählbarer Farbe**
- **Pause-Aktionen** – Bildschirm dimmen (DDC/CI) und Medienwiedergabe während der Pause pausieren
- **Automatische Updates** – optionale Prüfung über die GitHub-Releases-API; auf Wunsch lädt und
  installiert LookAway neue Versionen selbst (siehe [Updates](#updates))
- **Dreisprachig** – Deutsch, Englisch, Französisch, live umschaltbar
- **Statistiken und CSV-Export** – Pausen pro Tag, Woche und Jahr, exportierbar
- **Globale Hotkeys** – Pause starten, überspringen, Nicht stören – von überall
- **Optionaler Ton** – dezenter Hinweis bei der Erinnerung (aus drei Tönen wählbar)
- **Autostart** – startet auf Wunsch automatisch mit Windows

## Voraussetzungen

- Windows 10 (Version 1809) oder neuer / Windows 11

## Installation

Die fertige Anwendung liegt als **portables ZIP** auf der
[Releases-Seite](https://github.com/ReneSchustek/LookAway/releases/latest).

### Portable (empfohlen)

1. `LookAway-Portable-<Version>.zip` von der Releases-Seite herunterladen und in einen beliebigen
   Ordner entpacken.
2. `LookAway.exe` starten. Im Portable-Modus liegen alle Daten neben der EXE – ideal für den USB-Stick.

> Hinweis: Die Builds sind nicht mit einem CA-Zertifikat signiert – Windows SmartScreen kann beim ersten
> Start warnen („Weitere Informationen" → „Trotzdem ausführen").

### Setup.exe oder MSIX (optional, selbst gebaut)

Ein komfortabler **Setup.exe-Installer** (Inno Setup, frei wählbarer Speicherort, Startmenü-Eintrag,
optionaler Autostart) sowie ein **MSIX-Paket** lassen sich lokal erzeugen – die automatische
Veröffentlichung umfasst bewusst nur die portable ZIP:

```powershell
# Setup.exe (benötigt Inno Setup):
tools\publish-setup.ps1 -Version <Version>

# MSIX-Paket:
msbuild src\LookAway.App\LookAway.App.csproj -p:Configuration=Release -p:Platform=x64 `
  -p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true
```

## Erste Schritte

Beim ersten Start führt ein kurzer Assistent in drei Schritten durch die Einrichtung: Sprache,
Pausenmodell und Autostart. Danach lebt LookAway im Tray – ein Klick auf das Symbol öffnet das Menü,
ein Doppelklick die Einstellungen.

## Konfiguration

Alle Optionen finden sich im Einstellungsfenster (Tray-Menü → „Einstellungen…") im aufklappbaren
Seitenmenü: Allgemein, Pausenmodell, eigene Intervalle, Sound, Pause (inkl. Bildschirm-Abdunkelung und
Farbwähler), Hotkeys, Statistik und Über (mit Update-Optionen).

## Updates

LookAway kann sich selbst aktuell halten:

- Ist die Update-Prüfung aktiv und eine neue Version verfügbar, erscheint im Tray der Eintrag „Update".
- Ein Klick lädt die neue Portable-ZIP, ersetzt nach dem Beenden die Programmdateien und startet neu.
- Mit der Option **„Automatisch aktualisieren"** geschieht das im Hintergrund und beim nächsten Start
  ganz ohne Zutun.
- Vor dem Einspielen werden Version und SHA-256 der heruntergeladenen Datei geprüft; der Download läuft
  ausschließlich über HTTPS von GitHub. Der automatische Tausch funktioniert für portable und
  Per-Benutzer-Installationen (bei „für alle Benutzer" öffnet LookAway stattdessen die Release-Seite).

## Pausenmodelle

| Modell | Arbeit | Pause | Empfohlen für |
|---|---|---|---|
| Kurze Pausen | 60 min | 5 min | Lange, ruhige Arbeitsphasen |
| Klassisches Pomodoro | 25 min | 5 min | Fokussiertes Arbeiten in Etappen |
| Modifiziertes Pomodoro | 50 min | 10 min | Längere Konzentrationsblöcke |
| Ultradianer Rhythmus | 90 min | 20 min | Tiefe Arbeit nach dem natürlichen Rhythmus |
| Physical Counter | 40 min | 2 min | Haltung und Mikropausen |
| Aufgabenbasiert | bis 120 min | 10 min | Arbeiten bis zum Meilenstein |
| Gesetzliche Empfehlung | 120 min | 15 min | Bildschirmarbeit nach Vorgabe |

## Hotkeys (Standard)

| Aktion | Tastenkombination |
|---|---|
| Pause starten | `Strg + Alt + P` |
| Überspringen / Snooze | `Strg + Alt + S` |
| Nicht stören umschalten | `Strg + Alt + D` |

Die Hotkeys lassen sich in den Einstellungen aktivieren oder auf die Standardwerte zurücksetzen.

## Datenschutz

LookAway ist datensparsam und arbeitet vollständig lokal:

- **Was gespeichert wird:** Einstellungen, Pausen-Historie und Logdateien – ausschließlich auf dem
  eigenen Gerät unter `%APPDATA%\LookAway` (bzw. neben der EXE im Portable-Modus).
- **Was nicht passiert:** Es werden keine Nutzungsdaten, keine Telemetrie und keine persönlichen
  Daten an Server gesendet.
- **Einzige Netzwerkverbindung:** Die optionale Update-Prüfung fragt die öffentliche
  GitHub-Releases-API ab, ob eine neuere Version vorliegt. Sie lässt sich in den Einstellungen
  abschalten.

## Screenshots

Screenshots der Oberfläche (Tray-Icon, Pause-Erinnerung, Einstellungen, Statistik) werden unter
[`docs/screenshots/`](docs/screenshots/) gesammelt.

## Changelog

Die Versionshistorie steht in [`CHANGELOG.md`](CHANGELOG.md).

## Für Entwickler

Architektur, Build, Tests und die internen Details sind in [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md)
beschrieben.

## Lizenz

MIT-Lizenz – siehe [LICENSE](LICENSE).
