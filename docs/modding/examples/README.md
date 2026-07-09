# OpenEdge Modding Examples

These are disabled-by-default fixture mods for testing the hook/outcome framework. They are not loaded from this folder.

To test one:

1. Copy the example folder into `runtime/local/app/mods/`.
2. Open OpenEdge.
3. Open the Mods manager.
4. Enable the copied mod.
5. Use Reload App or restart OpenEdge.
6. Check `runtime/local/app/debug/session-trace.log` for `mod-hooks` and `mod-outcome` entries.

## Fixtures

- `additive-methodpicker` — adds an `additive` `methodPicker` hook that joins the base module pool.
- `exclusive-sessionintro` — replaces the base `sessionIntro` hook.
- `custom-hook-fallback` — demonstrates `RUNHOOK:hookName,fallbackScript`.
- `custom-outcome-edge` — demonstrates `OUTCOME:edge,exampleEdge` with declared outcome metadata.
- `invalid-warning` — intentionally contains invalid hook/outcome metadata for Mods manager warning tests.
- `exclusive-conflict-a` and `exclusive-conflict-b` — enable both to test exclusive/replace conflict warnings and priority ordering.

Keep these examples genre-neutral and small. They should demonstrate framework mechanics, not content assumptions.
