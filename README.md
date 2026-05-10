# LookAway

Eine schlanke Windows-Tray-Anwendung, die dezent an Bildschirmpausen erinnert. Mehrere wissenschaftlich fundierte Pausenmodelle, dreisprachig (Deutsch, Englisch, Franzoesisch), und vollstaendig konfigurierbar pro Windows-Benutzer.

## Voraussetzungen

- Windows 10 1809 oder neuer / Windows 11
- .NET 10 SDK (fuer den Build)
- Windows App SDK (fuer WinUI 3)

## Solution-Struktur

```
LookAway/
├── src/
│   ├── LookAway.App/             WinUI 3 App, Views, ViewModels, Services
│   ├── LookAway.Application/     Use Cases, DTOs, Application Services
│   ├── LookAway.Core/            Entities, Value Objects, Interfaces, Enums
│   └── LookAway.Data/            Repositories, Persistenz, externe Zugriffe
├── tests/
│   ├── LookAway.Tests.Unit/         xUnit, isolierte Domain-Tests
│   └── LookAway.Tests.Integration/  xUnit, echtes Dateisystem / Registry
├── Directory.Build.props         Globale Build-Einstellungen
├── .editorconfig                 Code-Style-Konventionen
└── LookAway.slnx                 Solution-Datei
```

## Build

```bash
dotnet restore
dotnet build --configuration Release
```

## Tests

```bash
dotnet test
```

## Entwicklung

```bash
dotnet run --project src/LookAway.App
```

## Architektur

Das Projekt folgt einer klassischen Schichtenarchitektur mit strikter Abhaengigkeitsrichtung von aussen nach innen:

```
App  →  Application  →  Core  ←  Data
```

- **Core** ist unabhaengig und kennt keine aeusseren Schichten
- **Application** orchestriert Use Cases ueber Core-Interfaces
- **Data** implementiert Persistenz-Adapter fuer Core-Interfaces
- **App** bindet alles ueber Dependency Injection zusammen

## Konfiguration

Die Benutzerkonfiguration liegt pro Windows-Benutzer unter:

```
%APPDATA%\LookAway\settings.json
```

- Format: JSON (`System.Text.Json`, camelCase, Enums als Strings)
- Schreibvorgaenge sind atomar (Temp-Datei + Rename) – kein halbgeschriebenes File bei Crashes
- Beschaedigte oder fehlende Datei: Defaults werden geladen, der naechste Speichervorgang ueberschreibt sie
- Mehrere Lese-/Schreibzugriffe sind parallel sicher (Reader oeffnen mit `FileShare.Delete`, Writes ueber Semaphor serialisiert)

Die Persistenz wird ueber `ISettingsRepository` (Core) abstrahiert; `JsonSettingsRepository` (Data) ist die Standardimplementierung und wird in `App.xaml.cs` als Singleton registriert.

## Lizenz

Proprietaer – alle Rechte vorbehalten.

<!-- TRIAGE-WORKFLOW: auto-managed by triage-deploy.ps1 -->
## Triage und Reviews

- **Watcher starten:** `.\triage-watch.ps1` (bzw. `.\triage-watch-php.ps1` / `.\triage-watch-shopware.ps1`) im Projekt-Root
- **Status-Dateien:** `.ai/triage-status.json`, `.ai/triage-escalation.md`, `.ai/reviews/*.md`, `.ai/erp/*.md`

Volle Doku: `F:\Entwicklung\_Anleitungen\allgemein\triage-workflow.md`
<!-- /TRIAGE-WORKFLOW -->
