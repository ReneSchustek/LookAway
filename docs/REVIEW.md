# LookAway — Deep Review (v1.1.0)

A multi-dimensional review of the codebase ahead of public release, covering
architecture, concurrency/correctness, security, UI/accessibility/i18n, tests and
release engineering. This document records the findings, what was fixed for
v1.1.0, and the deliberately deferred roadmap.

## Overall verdict

- **Architecture:** strong. Clean layering holds (Core has no outward refs;
  Application/Data → Core; App → all). Rich, self-validating domain
  (`BreakInterval`, `Settings`, `UpdateInfo`, value objects). Consistent,
  source-generated logging; modern, correct P/Invoke.
- **Internationalisation:** complete — 115 keys aligned across de/en/fr, every
  static, dynamic and enum-derived key resolves, no orphans.
- **Tests:** high quality and deterministic (time and power events faked). 354
  tests after this pass.
- The real issues clustered in the newest code (the auto-updater and the
  overlay/engine interplay) plus accessibility polish and release hygiene.

## Fixed in v1.1.0

### Correctness
- **Overlay/engine desync & stuck dim screen.** The break clock started at the
  reminder while the overlay countdown started on the user's click, so breaks
  were shortened and — if the reminder was ignored longer than the break — the
  dim/media state could remain stuck. The overlay is now authoritative for break
  end: `OnBreakOverlayEnded` always restores pause actions and starts a fresh work
  phase; `BreakCompletedEvent` defers to it while the overlay is open.
- **Unobserved exception** on the startup update check (offline start was logged
  as a crash) — now caught.
- **Shutdown dispose race** — background loops also swallow `ObjectDisposedException`.
- `TimerService._disposed` made `volatile`; concurrent update downloads use unique
  temp paths.

### Security (auto-updater)
- **HTTPS + GitHub-host pinning** on the package URL; downgrade redirects rejected.
- **Safe ZIP extraction**: entry-count and total-size limits (zip-bomb guard) and
  an explicit zip-slip check; download size cap.
- **Atomic apply with backup/rollback**: overwritten files are backed up and
  restored on failure, so a partial update can't brick the installation; the app
  is relaunched in either case.
- Staging now extracts to a unique work dir and is moved into place atomically.

### Accessibility / UI
- Overlay text colour now adapts to the chosen background luminance (readable on
  any user colour); overlay title is an assertive live region.
- `ColorPicker` has an accessible name; decorative logo hidden from AT; tertiary
  text colour darkened to clear WCAG AA.
- Second-instance launch now opens Settings (not the empty main window).

### Quality / hygiene
- Latent save crash fixed: an invalid overlay colour now disables Save
  (`CanPersist` validates it) instead of throwing.
- Hex-colour logic unified in one tested `Core/HexColor` helper (was duplicated 3×).
- Update-arg parsing moved to `Application/UpdateApplyArgs` (now unit-tested).
- Version bumped to 1.1.0; CHANGELOG finalised (de/en/fr); package versions pinned
  (reproducible builds); CI cache key corrected; `.gitignore` header fixed;
  internal AI/tooling notes and machine paths removed from docs; `LICENSE` added;
  `publish-setup.ps1` default output is now repo-relative `dist/`.
- 33 new tests (HexColor, UpdateApplyArgs, ResumeAfterAway, updater failure paths,
  GitHub asset selection incl. host rejection).

## Fixed in v1.1.1 (post-release re-review)

A second gapless review after v1.1.0 confirmed all v1.1.0 fixes and i18n alignment,
and surfaced these — now fixed:

- **In-app license** (About) said "Proprietary" — corrected to **MIT** (de/en/fr).
- **Security (was the top open item):** the startup auto-apply no longer trusts an
  arbitrary folder under `%LOCALAPPDATA%\…\updates\`. The app records the staged
  version + **SHA-256** it downloaded and only applies a staged folder whose hash
  matches (`FindVerifiedPendingUpdateDirectory`). The zip-bomb guard now counts
  **bytes actually written** instead of the ZIP's declared sizes.
- **Duplicate break reminders** when timer + user action fired together — display
  now marshals to the UI thread (single-threaded open-guard).
- Robustness: timer loop captures its cancellation token locally; overlay
  visibility flag is `volatile`; aborted partial downloads are cleaned up;
  colliding log event IDs reassigned.
- Tests: +18 (HexColor edge, zip-slip, hash-verify, HTTPS reject, githubusercontent
  host, SettingsViewModel invalid-colour + new-property round-trip). **361 total.**

## Deferred roadmap (documented, not yet done)

These are larger or environment-dependent and are intentionally tracked rather
than rushed:

1. **Extract an application coordinator (highest-value refactor).** The break
   workflow (timer-event → reminder → overlay → history) lives in `App.xaml.cs`,
   the one untestable assembly. Move it to a testable `Application` coordinator so
   the core behaviour gains the testable seam every other layer already has.
2. **Code signing for true update authenticity.** Current controls (HTTPS + host
   pinning) defend against MITM/corruption but not a compromised release. Full
   authenticity requires Authenticode/Ed25519 signing with an **offline** key and
   verification before apply. Requires a signing certificate.
3. **Layer-purity tidy-ups.** Move `SingleInstanceLock` behind a Core interface
   into Data (lets `Application` drop the `-windows` TFM); move `WindowsScreenDimmer`
   / `WindowsMediaController` from App to Data for adapter consistency.
4. **`SettingsViewModel` is large** — split per concern (hotkeys, updates) like the
   composed `Statistics` view model; **dedupe the two JSON repositories** behind a
   small `JsonFileStore`.
5. **Localised hotkey labels.** `HotkeyDefinition.ToString()` emits German modifier
   names ("Strg"); render via `ILocalizationService` so EN/FR are correct.
6. **Corrupt-file backup.** Back up `settings.json`/`history.json` before
   overwriting on parse failure instead of silently replacing.
7. **Polish:** remove unused presenter interfaces / dead members; refresh README
   version references (link to `/releases/latest`); add real screenshots; consider
   a "snooze/extend" affordance on the auto-dismissing reminder (WCAG 2.2.1).
