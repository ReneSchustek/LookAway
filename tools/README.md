# tools/

Portable Werkzeuge die NICHT als NuGet-Pakete kommen.

## gitleaks

Secret-Scanner (nutzt Shannon-Entropie zur Erkennung hochentroper Strings).
Binary ist via `*.exe` gitignored — vor dem ersten Lauf installieren:

```powershell
powershell tools/install-gitleaks.ps1
```

Lokaler Aufruf:

```powershell
tools/gitleaks.exe detect --source . --no-banner
tools/gitleaks.exe detect --source . --log-opts="--all" --no-banner   # inkl. History
```

Die CI braucht das Binary nicht: Der Security-Scan läuft über das
pattern-basierte `tools/review.ps1 -Mode security` (Schritt „Security-Scan" in
`.github/workflows/ci.yml`). gitleaks bleibt ein optionales lokales Werkzeug.
