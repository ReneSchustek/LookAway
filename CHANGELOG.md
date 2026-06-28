# Changelog

Alle nennenswerten Aenderungen an LookAway werden hier dokumentiert.
Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung an [Semantic Versioning](https://semver.org/lang/de/).

## [Unveroeffentlicht]

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

[Unveroeffentlicht]: https://github.com/ReneSchustek/LookAway/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/ReneSchustek/LookAway/releases/tag/v1.0.0
