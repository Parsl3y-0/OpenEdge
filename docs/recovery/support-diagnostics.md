# OpenEdge support diagnostics

Use these tools when checking compatibility state, script migration, or media tag recovery.

## Baseline verification

```powershell
powershell -ExecutionPolicy Bypass -File docs/recovery/verify-baseline.ps1
```

This runs build, SettingsHarness, and the smoke check.

## Legacy script/state audit

```powershell
powershell -ExecutionPolicy Bypass -File docs/recovery/audit-legacy-state.ps1 -OutputPath docs/recovery/legacy-state-audit.md
```

This reports remaining `FLAG:`, `DELFLAG:`, `SETVAR:`, `ADDVAR:`, `ISFLAG:`, `ISNOFLAG:`, and direct app-code legacy state calls.

## Full diagnostics export

```powershell
powershell -ExecutionPolicy Bypass -File docs/recovery/export-diagnostics.ps1
```

This writes `docs/recovery/openedge-diagnostics.md` with compatibility state counts, media tag counts, retained session-trace archive counts, and the script migration audit.

## In-app diagnostics

Open `Settings -> Migration & Diagnostics -> Export Diagnostics` to export a runtime diagnostics bundle under `runtime/local/app/debug/`. The bundle includes the current `session-trace.log`, retained `session-trace-*.log` archives, recent debug reports/logs, compatibility state, media source/tag files, and flags.

## Media tag recovery notes

- `media-tag-index.json` is primary.
- `tags.txt` is optional legacy input and is not created or written by OpenEdge.
- If media was moved or renamed, reload Media Sources first so identity matching can rebind tags.
- Duplicate files with the same fingerprint are intentionally not guessed; ambiguous claims are logged instead of stealing tags.
