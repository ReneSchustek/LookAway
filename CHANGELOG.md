# Changelog

Alle nennenswerten Aenderungen an LookAway werden hier dokumentiert.
Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung an [Semantic Versioning](https://semver.org/lang/de/).

## [Unveroeffentlicht]

## [1.0.2] – 2026-06-29

### Behoben

- Startabsturz im unpackaged-Betrieb behoben: Das Tray-Icon wurde als PNG an
  H.NotifyIcon uebergeben und warf eine Ausnahme; jetzt als DIB-ICO. Zudem
  fehlten beim Publish der Ressourcen-Index (PRI) und lose Asset-Dateien
  (XamlParseException / DirectoryNotFoundException) — die App startet nun
  zuverlaessig. Portable-ZIP und MSIX sind damit erstmals voll lauffaehig.

### Hinzugefuegt

- Setup.exe-Installer (Inno Setup): frei waehlbarer Speicherort, Installation
  fuer den aktuellen oder alle Benutzer, Startmenue-/optionale Desktop-Verknuepfung,
  optionaler Autostart und Uninstaller. Self-contained — keine vorinstallierte
  .NET-/Windows-App-SDK-Runtime noetig.

### Geaendert

- Verteilbare Builds sind self-contained (Windows App SDK), CI-Pipeline gehaertet
  (gruener Lauf, SHA-gepinnte Actions, node24) und die Git-Historie bereinigt.

## [1.0.1] – 2026-06-29

### Hinzugefuegt

- Abgedunkelter Vollbild-Pausen-Screen: verdeckt waehrend der Pause den Bildschirm, zeigt den
  Countdown und das Uebungs-Ziel und laesst sich mit **ESC** vorzeitig beenden
- EXE-Anwendungsicon (Explorer, Taskleiste, Alt+Tab) aus dem LookAway-Logo

### Geaendert

- Kachel- und Store-Logos (MSIX) aus dem LookAway-Logo neu erzeugt

## [1.0.0] – 2026-06-28

Erste vollstaendige Version.

### Hinzugefuegt

- Tray-Anwendung mit Single-Instance-Sperre und Status-Icon samt Live-Tooltip
- Timer-Engine mit sieben Pausenmodellen und Sleep-resilientem Zustand
- Pause-Erinnerung als dezentes Overlay-Fenster (Pause starten / verschieben / ueberspringen)
- Auto-Pause bei Inaktivitaet und Nicht-stoeren-Modus bei Vollbild-Apps
- Einstellungsfenster (Allgemein, Pausenmodell, eigene Intervalle, Sound, Pause-Aktionen,
  Hotkeys, Statistik, Update, Ueber)
- First-Run-Wizard fuer die Erstkonfiguration
- Dreisprachigkeit (Deutsch, Englisch, Franzoesisch) mit Laufzeit-Sprachwechsel
- Zentrales Theme (Farbpalette, Typografie, Button-Styles)
- Optionaler Erinnerungston mit Auswahl, Lautstaerke und Vorhoeren
- Statistiken (heute, Woche, Jahr) mit CSV-Export
- Globale Hotkeys fuer Pause, Snooze und Nicht stoeren
- Update-Pruefung ueber die GitHub-Releases-API
- Pause-Aktionen: Bildschirm dimmen und Medienwiedergabe pausieren
- Autostart mit Windows ueber den benutzerspezifischen Run-Eintrag
- Distribution als portable ZIP und MSIX-Paket

[Unveroeffentlicht]: https://github.com/ReneSchustek/LookAway/compare/v1.0.2...HEAD
[1.0.2]: https://github.com/ReneSchustek/LookAway/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/ReneSchustek/LookAway/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/ReneSchustek/LookAway/releases/tag/v1.0.0
