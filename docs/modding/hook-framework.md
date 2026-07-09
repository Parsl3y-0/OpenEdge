# Mod Hook and Outcome Framework Design

This document describes a genre-neutral revamp of the OpenEdge modding system. The goal is to let mods participate in session flow without requiring app-code changes for every new content type.

Current implementation status: mod priority config and Mods manager up/down priority controls, hook/outcome JSON metadata loading, `RUNSCRIPT:`, dynamic `RUNHOOK:`, conservative `OUTCOME:` commands, hook handled/unhandled tracing, base hook entrypoints for `sessionIntro`, `methodPicker`, `changeState`, `sessionEnd`, `edgeOpportunity`, and `orgasmDecision`, and additive/base pooling for `methodPicker` are implemented. More sophisticated additive/base pooling for other hooks and fallback-after-unhandled behavior are still planned.

## Goals

- Let mods register scripts for well-defined app/session hooks.
- Let mods either add to base behavior or take priority over it when appropriate.
- Let users control mod priority/load order.
- Let mod scripts report structured outcomes, such as an edge, orgasm, denial, punishment, or custom state transition.
- Keep the framework genre-neutral. Mods define the theme; the app defines safe state transitions.
- Preserve existing base behavior when no mods are enabled.
- Make hook decisions diagnosable from logs.

## Non-goals for the first implementation

- Do not build a full general-purpose programming language.
- Do not let mods execute arbitrary code.
- Do not hardcode genre-specific concepts into the framework.
- Do not make destructive state changes implicit. Outcome effects must be explicit.
- Do not replace the existing line/script system immediately.

## Terminology

### Mod

A folder under:

```text
runtime/local/app/mods/<mod-id>/
```

A mod may include settings, tags, contexts, lines, hooks, and outcomes.

### Hook

A named point in app/session flow where mods may register behavior. Hook names are string-based. OpenEdge exposes official base hook entrypoints, and mods may also emit their own custom hooks with `RUNHOOK:`.

Implemented official base hook names:

```text
sessionIntro
methodPicker
changeState
sessionEnd
edgeOpportunity
orgasmDecision
```

Possible future official hook names:

```text
sessionStart
punishment
```

Example custom mod hook names:

```text
myMod.beforeCustomFlow
myMod.customOutcomeDecision
otherMod.afterCustomFlow
```

### Hook handler

A mod-defined registration that says:

```text
At hook X, consider script Y under conditions Z.
```

### Hook mode

Controls how a handler interacts with base behavior and other mods.

Initial modes:

| Mode | Meaning |
|---|---|
| `additive` | Adds an option to the hook pool. Base behavior remains available. |
| `exclusive` | If eligible and selected, this handler owns the hook and base behavior is skipped. |
| `fallback` | Runs only if no higher-priority/base handler handled the hook. |

Possible later mode:

| Mode | Meaning |
|---|---|
| `replace` | Replaces the base handler for that hook whenever eligible. Use carefully. |

### Priority

User-controlled ordering for mods. Higher priority mods are considered first when hooks conflict.

Priority should be stored in runtime user config, not in the mod package itself, so users can resolve conflicts without editing mod files.

Priority file:

```text
runtime/local/app/mod-load-order.json
```

Example:

```json
{
  "mods": [
    { "id": "focused-session-overhaul", "priority": 100 },
    { "id": "small-flavor-pack", "priority": 50 }
  ]
}
```

### Eligibility

A handler is eligible only if its requirements are satisfied.

Possible requirement fields:

```json
{
  "requiresSettings": ["exampleSetting"],
  "forbidsSettings": ["exampleDisabledSetting"],
  "requiresContexts": ["exampleContext"],
  "requiresMediaTags": ["ExampleTag"],
  "minimumMedia": 2,
  "allowedStates": ["module", "anal", "cbt"],
  "allowedWhileChaste": true
}
```

### Outcome

A structured event reported by a script. Outcomes tell the app what happened mechanically.

Examples:

```text
OUTCOME:edge,customEdge
OUTCOME:orgasm,customOrgasm
OUTCOME:denial,customDenial
```

The app uses outcome metadata to update counters/state safely.

## Proposed file layout

```text
mods/example-mod/
  mod.json
  settings/
    settings.json
  tags/
    tags.json
  contexts/
    contexts.json
  hooks/
    hooks.json
  outcomes/
    outcomes.json
  lines/
    Scripts/
      Extend/
        customEdge.txt
        customOrgasm.txt
    Vocab/
      Extend/
        customWords.txt
```

## Hook schema

Supported metadata file `hooks/hooks.json`:

```json
{
  "hooks": [
    {
      "hook": "orgasmDecision",
      "script": "customOrgasmDecision",
      "mode": "exclusive",
      "weight": 100,
      "requiresSettings": ["customOutcomeEnabled"],
      "requiresContexts": ["customOutcomeReady"],
      "allowedWhileChaste": true
    },
    {
      "hook": "edgeOpportunity",
      "script": "customEdge",
      "mode": "additive",
      "weight": 20,
      "requiresSettings": ["customOutcomeEnabled"]
    }
  ]
}
```

Fields:

| Field | Required | Notes |
|---|---:|---|
| `hook` | yes | Name of the app hook. |
| `script` | yes | Script file name, without `.txt`. |
| `mode` | no | Defaults to `additive`. |
| `weight` | no | Selection weight among eligible handlers. Defaults to `1`. |
| `requiresSettings` | no | All listed settings must be enabled. |
| `forbidsSettings` | no | All listed settings must be disabled. |
| `requiresContexts` | no | All listed contexts must be active. |
| `requiresMediaTags` | no | Matching media must exist. |
| `minimumMedia` | no | Defaults to `2` when `requiresMediaTags` is used. |
| `allowedStates` | no | If present, current state must match one entry. |
| `allowedWhileChaste` | no | Metadata for handlers that should still be valid under restrictive states/settings. |

## Outcome schema

Supported metadata file `outcomes/outcomes.json`:

```json
{
  "outcomes": [
    {
      "key": "customEdge",
      "kind": "edge",
      "label": "Custom Edge",
      "countsAsEdge": true,
      "usesNormalStroking": false,
      "state": "module"
    },
    {
      "key": "customOrgasm",
      "kind": "orgasm",
      "label": "Custom Orgasm",
      "countsAsFullOrgasm": true,
      "resetsDeniedCounter": true,
      "resetsEdgeCounters": true,
      "usesNormalStroking": false,
      "allowedWhileChaste": true,
      "state": "module"
    }
  ]
}
```

Fields:

| Field | Required | Notes |
|---|---:|---|
| `key` | yes | Unique outcome key within enabled mods. |
| `kind` | yes | Suggested initial values: `edge`, `orgasm`, `ruin`, `denial`, `state`. |
| `label` | no | Human-readable diagnostics/UI label. |
| `countsAsEdge` | no | Increments edge counters when true. |
| `countsAsFullOrgasm` | no | Resets full-orgasm timer/counter when true. |
| `countsAsRuinedOrgasm` | no | Records ruined orgasm when true. |
| `resetsDeniedCounter` | no | Clears denial counter when true. |
| `incrementsDeniedCounter` | no | Increments denial counter when true. |
| `resetsEdgeCounters` | no | Clears edge/time-on-edge counters when true. |
| `usesNormalStroking` | no | If true, may use existing stroking/cum state. Defaults false for custom outcomes. |
| `allowedWhileChaste` | no | Whether this outcome is allowed when restrictive settings are enabled. |
| `state` | no | App state to set after the outcome, if any. |

## Script commands

### `RUNSCRIPT:<name>`

Implemented. Runs a named script from the combined base/mod line roots without requiring a new C# script class.

Example:

```text
RUNSCRIPT:customEdge
```

Design notes:

- The script should return to the previous script when complete.
- It should support base scripts and enabled mod scripts.
- The command should log the resolved source path.

### `RUNHOOK:<hookName>` / `RUNHOOK:<hookName>,<fallbackScript>`

Implemented. Emits a named hook from a script. The hook name can be an official OpenEdge hook or a custom mod-defined hook. Other enabled mods can register handlers for the same hook and priority/mode rules decide which handler runs.

If a fallback script is provided, OpenEdge runs that script when no eligible hook handler is selected.

Example:

```txt
RUNHOOK:myMod.beforeCustomEdge
RUNHOOK:myMod.customEdgeBody,defaultCustomEdgeBody
RUNHOOK:myMod.afterCustomEdge
```

A higher-priority mod can hook into `myMod.customEdgeBody` with `exclusive` or `replace` mode to override that part of the flow.

### `OUTCOME:<kind>,<key>`

Implemented in conservative form. Reports that a structured outcome happened and applies explicit outcome metadata effects.

Examples:

```text
OUTCOME:edge,customEdge
OUTCOME:orgasm,customOrgasm
OUTCOME:denial,customDenial
```

Design notes:

- Applying an outcome marks the current hook as handled.
- Unknown outcome keys should log a warning and fail safely.
- Outcome effects should be conservative and explicit.
- The trace log should include mod id, hook, outcome kind, outcome key, and applied effects.

## Hook resolution algorithm

For a hook invocation:

1. Collect base handler, if any.
2. Collect enabled mod handlers for the hook.
3. Filter handlers by eligibility.
4. Sort mod handlers by user priority, then handler weight/order.
5. Resolve by mode:
   - Eligible `exclusive` handlers are considered before base behavior.
   - `additive` handlers join the base pool.
   - `fallback` handlers are considered only if nothing handled the hook.
6. Run the selected handler script.
7. If the script reports an outcome, mark the hook handled.
8. If the script does not handle the hook, continue/fallback according to mode.
9. If no mod handles it, use base OpenEdge behavior.

## Initial hook integration status

Implemented base hook entrypoints:

1. `sessionIntro`
   - Runs eligible `exclusive`/`replace` handlers before the base intro.
   - Marks the session intro temp flag when a mod hook takes over, so a custom intro does not repeat forever.
2. `methodPicker`
   - Runs eligible `exclusive`/`replace` handlers before base method picking.
   - Eligible `additive` handlers are pooled against base method picking with weight.
3. `changeState`
   - Runs eligible `exclusive`/`replace` handlers before the base ChangeState script.
4. `sessionEnd`
   - Runs eligible `exclusive`/`replace` handlers before base ending selection.
5. `edgeOpportunity`
   - Runs eligible `exclusive`/`replace` handlers before normal edge/edgehold mechanics.
6. `orgasmDecision`
   - Runs eligible `exclusive`/`replace` handlers before base orgasm decision logic.

Still planned:

- additive/base weighted pooling for official hooks beyond `methodPicker`
- fallback-after-unhandled base behavior where practical
- additional official base hook entrypoints

## Base behavior contract

When no mods are installed or enabled:

- Hook resolution must select the same base behavior as before.
- Existing scripts must still run.
- Existing counters and state must update as before.
- Smoke check should pass with no extra setup.

## Diagnostics

Trace logging should include:

```text
mod-hooks - loaded hook count per mod
mod-hooks - hook=<name> eligible=<count> selected=<mod/script/mode>
mod-hooks - skipped hook reason=<reason>
mod-outcome - kind=<kind> key=<key> mod=<id> effects=<...>
mod-hooks - fallback to base hook=<name>
```

Diagnostics export should include safe summaries of:

- enabled mods
- mod priority order
- hook counts
- outcome counts
- mod validation errors

## Mods manager UI requirements

Mods manager now shows:

- enabled state
- priority order with move up/down controls
- settings/tags/context/hook/outcome counts
- validation warnings
- whether a mod contains exclusive/replace hooks

Priority changes currently require Reload App/restart before startup-loaded mod data refreshes everywhere.

## Documentation requirements

Update `docs/modding/CONTRIBUTING.md` with:

- hook schema
- outcome schema
- priority/load order explanation
- `RUNSCRIPT:` examples
- `OUTCOME:` examples
- additive mod example
- exclusive/focused mod example
- compatibility and safety guidelines

## Testing checklist

- No mods installed: base behavior unchanged.
- Disabled mod: hooks/outcomes ignored.
- Invalid hook JSON: mod warning, no crash.
- Invalid outcome JSON: mod warning, no crash.
- Additive hook: base behavior remains available.
- Exclusive hook: eligible mod can prevent base behavior.
- Fallback hook: runs only when nothing else handles the hook.
- Conflicting mods: priority order is deterministic.
- Outcome command: applies only declared effects.
- Release packaging: runtime mods and priority config remain excluded from release zips.
