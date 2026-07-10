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

## Roadmap status

The originally deferred roadmap has since been worked off; only the two genuinely
environment-dependent items remain.

### Done

1. **Application coordinator (✔).** The break workflow (timer-event → reminder →
   overlay → history) moved out of `App.xaml.cs` into a testable
   `Application/Coordination/BreakCoordinator` behind presenter abstractions
   (`IReminderPresenter`/`IBreakOverlayPresenter`/`ITrayController`), covered by
   `BreakCoordinatorTests`.
3. **Layer purity (✔).** `ISingleInstanceLock` (Core) introduced; `SingleInstanceLock`
   moved to Data — the only thing forcing the Windows TFM, so `LookAway.Application`
   now targets plain `net10.0`. `WindowsScreenDimmer` and `WindowsMediaController`
   moved from App to Data (all Windows adapters live in Data; Data targets the
   Windows SDK TFM for the WinRT/SMTC projections).
4. **`SettingsViewModel` split + repo dedup (✔).** The hotkey and update concerns
   were extracted into `SettingsViewModel.Hotkeys.cs` / `SettingsViewModel.Updates.cs`
   partials; the two JSON repositories now share a `JsonFileStore` (atomic write,
   tolerant read, write-serialisation, corrupt-backup). Full child-view-model
   composition was deliberately not pursued: it would rebind dozens of XAML
   properties (runtime-only risk the test suite can't catch) for marginal gain.
5. **Localised hotkey labels (✔).** `HotkeyDefinition.Format(...)` + `KeyLabel`
   render modifiers via `HotkeyTextKeys.Format`/`ILocalizationService`
   (Strg/Ctrl, Umschalt/Shift/Maj); modifier keys added to de/en/fr. `ToString`
   stays as a culture-neutral fallback.
6. **Corrupt-file backup (✔).** `settings.json`/`history.json` are copied to a
   `*.corrupt` sidecar before defaults replace them on a parse failure
   (`JsonFileStore.TryWriteCorruptBackupAsync`), with tests in both repositories.
7. **Polish (partly ✔).** Removed the unused `ISettingsPresenter` interface;
   README version references already link to `/releases/latest`. The reminder
   auto-defaults to the protective `StartBreak` after its 30 s timeout and already
   offers a Snooze action, so the WCAG 2.2.1 concern is met without an extra
   control.
2. **Release signing (✔).** Update authenticity is now enforced by a detached
   **ECDSA P-256 / SHA-256** signature: the maintainer signs each portable ZIP with
   an **offline** private key (`tools/sign-release.ps1`); the app ships only the
   embedded public key (`ReleaseSignatureVerifier.DefaultPublicKeySpkiBase64`) and
   verifies the downloaded package against the downloaded `*.sig` **before** anything
   is extracted or applied (fail-closed). A compromised release channel cannot forge
   a valid signature without the offline key. No certificate authority is required;
   `tools/new-signing-key.ps1` generates/rotates the key pair, and CI signs
   automatically when the `LOOKAWAY_SIGNING_KEY` secret is present.

### Remaining (environment-dependent)

7b. **Screenshots.** Real product screenshots for the README require running the
   packaged WinUI app on a desktop session; tracked for a manual capture pass.
