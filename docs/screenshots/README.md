# Screenshots

Aufnahmen der Oberfläche für die READMEs und die Projektseite. PNG, 800 px Breite,
deutschsprachige Oberfläche, helles Erscheinungsbild.

Vorhanden:

- `break-screen.png` – der Pausen-Screen mit Ziel und Countdown
- `reminder.png` – das Erinnerungsfenster mit den drei Aktionen
- `break-models.png` – Pausenmodelle als Kacheln mit Suche und Filter
- `statistics.png` – Statistik mit Tages-, Wochen- und Jahresansicht
- `settings.png` – Einstellungen, Bereich „Allgemein" mit der Erscheinungsbild-Wahl
- `hotkeys.png` – Einstellungen, Bereich „Hotkeys" mit der Neubelegung

## Wie sie entstehen

`tools/make-screenshots.ps1` nimmt sie auf. Zwei Eigenheiten bestimmen den Ablauf:

Das Programm läuft als **Einzelinstanz** — ein zweiter Start übergibt an die laufende
und beendet sich sofort. Genau das öffnet aber das Einstellungsfenster, und darüber
kommt das Skript hinein, statt das Symbol im Infobereich zu bedienen.

Aufgenommen wird über `PrintWindow` aus dem Fensterpuffer, **nicht** als
Bildschirmausschnitt: Sonst landet alles mit im Bild, was zufällig davor liegt.

**Der Rahmen muss weg.** `GetWindowRect` liefert die Fenstergrenzen einschließlich des
unsichtbaren Schattenrahmens, den Windows um moderne Fenster legt — `PrintWindow` füllt
ihn mit Schwarz, und das Bild bekommt unten einen 7 bis 13 px breiten schwarzen Streifen.
Das Skript schneidet ihn über `DwmGetWindowAttribute` mit `DWMWA_EXTENDED_FRAME_BOUNDS`
ab, also über die tatsächlich sichtbaren Grenzen, plus einen Pixel Sicherheitsabstand
gegen den Mischpixel beim Skalieren. Ein Beschnitt nach „dunklen Randzeilen" wäre der
falsche Weg: Er würde beim ganzflächig dunklen Pausen-Screen das halbe Bild wegnehmen.

## Was in den Bildern steht

Die Aufnahmen entstehen gegen eine **portable Instanz mit vorbereiteter Belegung**,
nicht gegen eine Installation mit echten Daten — dort stünden persönliche Pausenzeiten
im Bild. Die Belegung bildet einen normalen Verlauf ab: rund fünf bis sieben Pausen an
einem Arbeitstag, gelegentlich eine übersprungene, verteilt über die Modelle, die
jemand über die Monate ausprobiert hat. Keine Rekordwerte — ein Bild, das Bestleistung
zeigt, wirbt gegen das Programm, weil niemand sie erreicht.

`statistics.png` zeigt eine **vollständige Arbeitswoche** (Montag bis Freitag gefüllt,
Wochenende leer). Das ist eine Belegung, kein mitgeschriebener Verlauf: Aufgenommen
wurde an einem Dienstag, die übrigen Tage sind vorbelegt. Die Kachel „Heute" zeigt
dagegen den Tag der Aufnahme. Wer die Bilder erneuert, entscheidet neu — eine Woche mit
zwei Balken ist ehrlicher, eine volle Woche zeigt die Ansicht so, wie sie im Betrieb
aussieht.

## Pausen-Screen

Das Pausenfenster füllt den Bildschirm und trägt damit dessen Seitenverhältnis — auf
einem breiten Monitor ein flacher Streifen, in dem der Text bei 800 px Breite nicht mehr
lesbar wäre. `break-screen.png` ist deshalb ein **mittiger Ausschnitt** im Format der
übrigen Aufnahmen. Der Inhalt sitzt ohnehin in der Mitte; die abgeschnittene Fläche ist
einfarbig.

## Nicht vorhanden

- Das Symbol im Infobereich samt Kurzinfo. Es lässt sich nicht ohne Bedienung am
  Bildschirm auslösen und wird nachgereicht, sobald es sich aufnehmen lässt — und nicht
  nachgestellt.
- Die Protokoll-Ansicht. Aufnehmbar wäre sie, aber ihr Inhalt bestünde beim Aufnehmen
  überwiegend aus Meldungen über den Zweitstart, mit dem das Skript das Fenster öffnet.
  Das sagt über das Programm nichts aus, und ein passenderes Protokoll wäre gestellt.
