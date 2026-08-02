# Changelog

**Deutsch** · [English](CHANGELOG.en.md) · [Français](CHANGELOG.fr.md)

Alle nennenswerten Änderungen an LookAway werden hier dokumentiert.
Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung an [Semantic Versioning](https://semver.org/lang/de/).

## [Unveröffentlicht]

### Hinzugefügt

- **Freiwilliger Spenden-Einstieg:** Die Über-Seite der Einstellungen zeigt unter Version, Lizenz
  und Dokumentation einen Link, über den sich das Projekt mit einem Kaffee unterstützen lässt.
  Vollkommen freiwillig, ohne Einfluss auf den Funktionsumfang.

- **Setup.exe liegt wieder an jedem Release:** Seit 1.2.5 enthielten die Releases nur noch die
  portable ZIP; der Installer musste selbst kompiliert werden. Er entsteht jetzt im Release-Lauf
  und wird mit hochgeladen. Der Release-Text nennt zu beiden Dateien den SHA-256. Signiert wird
  weiterhin ausschließlich die ZIP — der Updater greift sich die erste Signatur eines Releases,
  eine zweite könnte er dem Paket zuordnen und die Prüfung fehlschlagen lassen.

## [1.2.8] – 2026-07-11

### Behoben

- **Vermerk eines eingespielten Updates blieb stehen:** Nach dem Einspielen behielten die
  Einstellungen Version und Prüfsumme des Updates. Folgenlos, aber bei jedem Start wurde
  erneut nach einem längst abgeräumten Paket gesucht. Der Vermerk wird jetzt verworfen,
  sobald kein einspielbares Paket mehr dazu gehört.

## [1.2.7] – 2026-07-11

### Behoben

- **Automatische Aktualisierung installierte das Paket nicht.** Das Update wurde geprüft und
  heruntergeladen, aber nie eingespielt — die alte Version blieb installiert. Der Hilfsprozess,
  der die Programmdateien austauscht, startet aus dem Zwischenspeicher und leitete daraus zwei
  Dinge falsch ab:
  - **Seinen Datenort:** Weil das Paket eine Portable-Markierung enthält, hielt er sich für eine
    portable Installation und suchte die Einstellungen neben sich. Dort fand er den vermerkten
    Datei-Hash nicht, mit dem er das Paket gegenprüft — und lehnte sein eigenes Update ab. Er
    verwendet jetzt den Datenort der Installation, die er bedient. Zusätzlich wird die
    Portable-Markierung gar nicht mehr mit zwischengespeichert.
  - **Sein Ziel:** Er nahm seinen eigenen Programmordner (also den Zwischenspeicher) als Ziel
    und kopierte damit auf sich selbst. Er verwendet jetzt den übergebenen Installationsordner
    und prüft ihn vorher (vorhandene, beschreibbare Installation außerhalb des
    Zwischenspeichers).

## [1.2.6] – 2026-07-10

### Hinzugefügt

- **Automatischer Pausenstart:** Die Pause startet nach einer einstellbaren Verzögerung
  (Standard 15 Sekunden, in 5-Sekunden-Schritten bis zu 3 Minuten) automatisch, wenn die
  Erinnerung nicht bedient wird. Ein Countdown im Erinnerungsfenster zeigt die verbleibende
  Zeit an. Über die Einstellungen an- oder abschaltbar — deaktiviert bleibt die Erinnerung
  offen, bis eine Aktion gewählt wird.

## [1.2.5] – 2026-07-02

### Geändert

- **Hinweis zur Medienpause:** In den Pause-Aktionen erklärt ein Hinweistext nun, welche
  Player automatisch pausiert werden. Nur Anwendungen, die sich in die Windows-Mediensteuerung
  (SMTC) einklinken, lassen sich steuern (z. B. Spotify, die Musik-App sowie Wiedergabe in
  Chrome, Edge und Firefox). **VLC unterstützt dies nicht** und wird nicht pausiert.

## [1.2.4] – 2026-07-01

### Geändert

- **Overlay-Transparenz entfernt:** Der Alpha-/Deckkraft-Regler beim Overlay-Farbwähler
  entfällt, ebenso der irreführende Hinweis („wie stark der Bildschirm durchscheint").
  Echte Fenster-Transparenz ist in WinUI 3 nicht zuverlässig umsetzbar; das Overlay
  überdeckt den Bildschirm deckend. Eine bisher halbtransparent eingestellte Farbe wird
  automatisch auf ihr optisch gleiches, deckendes Äquivalent migriert — das Aussehen
  ändert sich dadurch nicht.

## [1.2.3] – 2026-07-01

### Behoben

- **Pausen-Overlay erscheint wieder:** Beim Pausenbeginn mit aktivem „alle
  Bildschirme abdunkeln" (Standard) wurde das Overlay nicht angezeigt. Ursache war
  ein `InvalidCastException` beim Aufzählen der Monitorliste aus
  `DisplayArea.FindAll()` (WinRT-Projektion, fehlschlagende `IIterable`-Abfrage in
  CsWinRT) — auf älteren Ständen führte das sogar zum Absturz beim Klick auf
  „Pause starten". Die Monitorliste wird jetzt per Index in ein verwaltetes Array
  übernommen; das Overlay deckt wieder alle Monitore ab. (Die in 1.2.2 ergänzte
  Absicherung fängt einen etwaigen Fehler weiterhin ab, statt die App hängen zu lassen.)

### Geändert

- **Pausen-Inhalt nur auf dem Hauptmonitor:** Titel, Hinweis und Countdown werden
  bei „alle Bildschirme abdunkeln" nur noch auf dem Hauptmonitor angezeigt; weitere
  Monitore werden lediglich abgedunkelt (leeres Overlay). Der ESC-Kurzbefehl beendet
  die Pause weiterhin von jedem Monitor aus.
- **Bessere automatische Textfarbe im Overlay:** Die kontrastreiche Textfarbe richtet
  sich jetzt nach der tatsächlich sichtbaren Overlay-Farbe (halbtransparente Farbe
  über hellem Grund zusammengesetzt, wahrgenommene Helligkeit). Ein halbtransparentes
  Schwarz — das als Grau erscheint — bekommt dadurch dunklen statt hellem, schlecht
  lesbarem Text.

## [1.2.2] – 2026-07-01

### Behoben

- **Automatische Aktualisierung bleibt nicht mehr hängen:** Ein bereits
  heruntergeladenes, signatur- und hash-geprüftes Update-Paket wird bei jedem
  Start **nicht erneut geladen und entpackt**. Bisher überschrieb der Startlauf die
  entpackte `LookAway.exe` jedes Mal neu; eine frisch geschriebene, noch nicht
  signierte Datei wird beim ersten Ausführen vom Virenscanner geprüft, was den
  Helfer-Start kurzzeitig mit „Zugriff verweigert" blockieren kann. Dadurch wurde
  das Paket immer wieder „kalt" und das Update nie eingespielt. Jetzt bleibt das
  einmal bereitgestellte Paket liegen und wird beim nächsten Start eingespielt.
- **Robusterer Pausenbeginn:** Schlägt der Aufbau des Pausen-Overlays oder des
  Erinnerungsfensters fehl, bleibt die App bedienbar: der Zustand wird sauber
  zurückgesetzt, Helligkeit/Medien werden wiederhergestellt und der Timer läuft
  weiter — statt mit gesetztem „Pause läuft"-Zustand hängenzubleiben.

## [1.2.1] – 2026-06-30

### Behoben

- **Timer wird nicht mehr unnötig zurückgesetzt:** Das Speichern unveränderter
  Einstellungen (Sprache, Ton, Overlay-Farbe, Update-Häufigkeit …) startet den
  laufenden Arbeits-Countdown nicht mehr neu. Zudem überdauert der Countdown einen
  Neustart **innerhalb derselben Windows-Sitzung** (z. B. eine Aktualisierung) und
  läuft dort weiter, statt von vorn zu beginnen. Ein Windows-Neustart (neue Sitzung)
  setzt regulär zurück; der Reset nach Standby/Bildschirm-Aus bleibt unverändert.

### Hinzugefügt

- **Ein-Klick-Installation:** „Auf Updates prüfen" bietet bei einem gefundenen Paket
  jetzt direkt einen **„Jetzt installieren"**-Button. Das Update wird geladen,
  signaturgeprüft und beim nächsten Start eingespielt — kein Umweg mehr über die
  GitHub-Release-Seite (die als manueller Ausweg sichtbar bleibt).

### Geändert

- Interner Qualitäts-Feinschliff: vollständiges Kommentar-/Prinzipien-Audit aller
  Schichten, toter Code entfernt, durchgängig korrekte Umlaute auch in Projekt-Kommentaren.

## [1.2.0] – 2026-06-30

### Hinzugefügt

- **Echtheit von Updates (Release-Signatur):** Update-Pakete werden vor dem
  Entpacken/Einspielen gegen eine losgelöste **ECDSA-P-256/SHA-256-Signatur**
  geprüft (`*.sig`-Asset gegen eingebetteten öffentlichen Schlüssel, fail-closed).
  Ein übernommener Release-Kanal kann ohne den offline gehaltenen privaten Schlüssel
  keine gültige Signatur erzeugen. Werkzeuge `tools/new-signing-key.ps1` und
  `tools/sign-release.ps1`; die CI signiert mit dem Secret `LOOKAWAY_SIGNING_KEY`.

### Geändert

- **Schichtreinheit:** Single-Instance-Sperre hinter das Core-Interface
  `ISingleInstanceLock` gelegt und samt der Windows-Adapter
  (`WindowsScreenDimmer`, `WindowsMediaController`) in die Data-Schicht verschoben;
  `LookAway.Application` ist dadurch plattformneutral (`net10.0`).
- Beide JSON-Repositories nutzen einen gemeinsamen `JsonFileStore`; beschädigte
  `settings.json`/`history.json` werden vor dem Ersetzen als `*.corrupt` gesichert.
- Hotkey-Anzeige wird über `ILocalizationService` lokalisiert (Strg/Ctrl, Umschalt/
  Shift/Maj); `SettingsViewModel` nach Belang (Hotkeys/Updates) aufgeteilt.

### Behoben

- Durchgängig korrekte Umlaute (ä/ö/ü/ß) in Kommentaren, Texten und Test-Namen.

## [1.1.1] – 2026-06-30

### Behoben

- In-App-Lizenzanzeige (Über-Bereich) zeigt nun korrekt **MIT** statt „Proprietär".
- **Sicherheit:** Ein ausstehendes Update wird vor dem Einspielen über Version
  **und SHA-256 der Programmdatei** verifiziert — ein einfach untergeschobener
  Ordner unter `%LOCALAPPDATA%\…\updates\` wird nicht mehr ausgeführt. Der
  Zip-Bomben-Schutz begrenzt jetzt die **tatsächlich geschriebenen** Bytes statt
  der im ZIP angegebenen Größe.
- Keine doppelten Pausen-Erinnerungen mehr, wenn Timer und Benutzeraktion
  gleichzeitig auslösen (die Anzeige läuft thread-sicher auf dem UI-Thread).
- Robustheit: Der Timer-Loop nutzt ein lokal festgehaltenes Abbruch-Token,
  die Overlay-Sichtbarkeit ist `volatile`, abgebrochene Teil-Downloads werden
  aufgeräumt, kollidierende Log-Event-IDs wurden neu vergeben.

## [1.1.0] – 2026-06-30

### Hinzugefügt

- Pausen-Screen auf **mehreren Monitoren**: Auf Wunsch wird während der Pause
  jeder angeschlossene Bildschirm mit einem eigenen Overlay abgedeckt (Option
  „Alle Bildschirme abdunkeln", Standard: an). Funktioniert unabhängig von
  DDC/CI — also auch auf Notebooks.
- **Frei wählbare Farbe des Pausen-Screens** inklusive Transparenz (Deckkraft-/
  Alpha-Regler) über einen Farbwähler in den Einstellungen.
- **Automatische Aktualisierung**: Ist eine neue Version verfügbar, kann LookAway
  sie selbst installieren — das neue Portable-Paket wird heruntergeladen, die
  Programmdateien werden nach dem Beenden ausgetauscht und die App startet neu.
  Neue Einstellung **„Automatisch aktualisieren"**: lädt die neueste Version im
  Hintergrund und installiert sie beim nächsten Start ohne Zutun. Ohne diese
  Option genügt ein Klick auf den Tray-Eintrag „Update".

### Geändert

- Modernisierte Einstellungen: Das Menü oben (Registerkarten) wurde durch ein
  ein-/ausklappbares **Seitenmenü** (NavigationView mit Hamburger-Button) ersetzt.
- Neues helles **Mint/Teal-Theme** (augenfreundlich) für die gesamte Oberfläche.
- Nach **Standby oder Inaktivität** (z. B. Telefonat) startet der Arbeits-Timer
  frisch, wenn die Abwesenheit mindestens so lang wie eine Pause war — die Augen
  haben dann ohnehin bereits geruht. Kurze Unterbrechungen laufen wie bisher mit
  der Restzeit weiter; eine manuelle Pause bleibt unverändert.
- Der Tray-Eintrag „Update" öffnet nicht mehr nur die Release-Seite, sondern
  lädt die neue Version herunter und installiert sie automatisch.

## [1.0.2] – 2026-06-29

### Behoben

- Startabsturz im unpackaged-Betrieb behoben: Das Tray-Icon wurde als PNG an
  H.NotifyIcon übergeben und warf eine Ausnahme; jetzt als DIB-ICO. Zudem
  fehlten beim Publish der Ressourcen-Index (PRI) und lose Asset-Dateien
  (XamlParseException / DirectoryNotFoundException) — die App startet nun
  zuverlässig. Portable-ZIP und MSIX sind damit erstmals voll lauffähig.

### Hinzugefügt

- Setup.exe-Installer (Inno Setup): frei wählbarer Speicherort, Installation
  für den aktuellen oder alle Benutzer, Startmenü-/optionale Desktop-Verknüpfung,
  optionaler Autostart und Uninstaller. Self-contained — keine vorinstallierte
  .NET-/Windows-App-SDK-Runtime nötig.

### Geändert

- Verteilbare Builds sind self-contained (Windows App SDK), CI-Pipeline gehärtet
  (grüner Lauf, SHA-gepinnte Actions, node24) und die Git-Historie bereinigt.

## [1.0.1] – 2026-06-29

### Hinzugefügt

- Abgedunkelter Vollbild-Pausen-Screen: verdeckt während der Pause den Bildschirm, zeigt den
  Countdown und das Übungs-Ziel und lässt sich mit **ESC** vorzeitig beenden
- EXE-Anwendungsicon (Explorer, Taskleiste, Alt+Tab) aus dem LookAway-Logo

### Geändert

- Kachel- und Store-Logos (MSIX) aus dem LookAway-Logo neu erzeugt

## [1.0.0] – 2026-06-28

Erste vollständige Version.

### Hinzugefügt

- Tray-Anwendung mit Single-Instance-Sperre und Status-Icon samt Live-Tooltip
- Timer-Engine mit sieben Pausenmodellen und Sleep-resilientem Zustand
- Pause-Erinnerung als dezentes Overlay-Fenster (Pause starten / verschieben / überspringen)
- Auto-Pause bei Inaktivität und Nicht-stören-Modus bei Vollbild-Apps
- Einstellungsfenster (Allgemein, Pausenmodell, eigene Intervalle, Sound, Pause-Aktionen,
  Hotkeys, Statistik, Update, Über)
- First-Run-Wizard für die Erstkonfiguration
- Dreisprachigkeit (Deutsch, Englisch, Französisch) mit Laufzeit-Sprachwechsel
- Zentrales Theme (Farbpalette, Typografie, Button-Styles)
- Optionaler Erinnerungston mit Auswahl, Lautstärke und Vorhören
- Statistiken (heute, Woche, Jahr) mit CSV-Export
- Globale Hotkeys für Pause, Snooze und Nicht stören
- Update-Prüfung über die GitHub-Releases-API
- Pause-Aktionen: Bildschirm dimmen und Medienwiedergabe pausieren
- Autostart mit Windows über den benutzerspezifischen Run-Eintrag
- Distribution als portable ZIP und MSIX-Paket

[Unveröffentlicht]: https://github.com/ReneSchustek/LookAway/compare/v1.1.1...HEAD
[1.1.1]: https://github.com/ReneSchustek/LookAway/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/ReneSchustek/LookAway/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/ReneSchustek/LookAway/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/ReneSchustek/LookAway/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/ReneSchustek/LookAway/releases/tag/v1.0.0
