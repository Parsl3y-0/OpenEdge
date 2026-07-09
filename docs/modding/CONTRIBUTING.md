# OpenEdge Mod Contributing Guide

OpenEdge mods live in the runtime `mods` folder and can add simple settings, media tags, derived contexts, line/script content, and vocabulary without changing app code.

Runtime location:

```txt
runtime/local/app/mods/
```

Recommended mod layout:

```txt
mods/example-mod/
  mod.json
  settings/
    settings.json
  tags/
    tags.json
  contexts/
    contexts.json
  lines/
    Scripts/
      Extend/
        exampleScript.txt
      Base/
        optionalReplacementScript.txt
    Vocab/
      Extend/
        exampleVocab.txt
        tags.txt
      Base/
        optionalReplacementVocab.txt
```

## `mod.json`

Every mod should include a manifest:

```json
{
  "id": "example-mod",
  "name": "Example Mod",
  "author": "Your Name",
  "version": "1.0.0",
  "enabled": true
}
```

Fields:

- `id`: stable unique id for the mod folder/content.
- `name`: display name shown in the Mods manager.
- `author`: optional creator name.
- `version`: optional version string.
- `enabled`: whether OpenEdge should load the mod.

The Mods manager can enable/disable mods by editing this file.

## Settings

Simple mod settings go in:

```txt
settings/settings.json
```

Example:

```json
{
  "settings": [
    {
      "key": "latexPreference",
      "label": "Latex Preference",
      "kind": "Toggle",
      "group": "Clothing",
      "description": "Enables latex-focused teasing.",
      "warning": "Affects latex progression scenes.",
      "legacyEnabledFlag": "latexPreference",
      "legacyDisabledFlag": "latexPreferenceNo",
      "queueableAsk": true,
      "mediaDiscoveryTags": ["Latex"],
      "mediaDiscoveryMinimum": 2
    },
    {
      "key": "latexIntensity",
      "label": "Latex Intensity",
      "kind": "Numeric",
      "legacyValueKey": "latexIntensity"
    },
    {
      "key": "favoriteOutfit",
      "label": "Favorite Outfit",
      "kind": "Text",
      "legacyValueKey": "favoriteOutfit"
    }
  ]
}
```

Supported `kind` values:

- `Toggle`
- `Numeric`
- `Text`

Optional Settings UI metadata:

- `group`: groups this setting under a mod-specific expander, e.g. `Mod Name / Clothing`.
- `description`: short help text shown below the setting label.
- `warning`: progression/behavior warning shown in gold. Use only when changing the setting can affect progression or identity state.

Optional media discovery fields:

- `queueableAsk`: allows the setting to be queued as a future session ask.
- `mediaDiscoveryTags`: concrete media tags that imply this setting should be asked about.
- `mediaDiscoveryMinimum`: how many matching media files are needed before the ask is queued.

When a media-discovered ask is queued, OpenEdge first looks for a dedicated script matching the setting key, e.g. `lines/Scripts/Extend/latexPreference.txt`. If none exists, it falls back to the normal ask script pool.

Settings automatically appear in the Settings menu. Toggle settings can be used by scripts with predicates such as:

```txt
ISSETTING:latexPreference
ISNOSETTING:latexPreference
ISSETTINGASKED:latexPreference
SETTINGVALUEATLEAST:latexIntensity,3
```

Use settings for preferences, progression, tone, intensity, and narrative state. Do **not** create media tags for things that are really user preferences or story context.

## Tags

Media tags go in:

```txt
tags/tags.json
```

Example:

```json
{
  "tags": [
    {
      "key": "latex",
      "label": "Latex",
      "group": "Clothing"
    },
    {
      "key": "collar",
      "label": "Collar",
      "group": "Accessories"
    }
  ]
}
```

Tags automatically appear in the tagger and Bulk Operations. Tag groups stay in group order, and tags inside each group sort alphabetically.

### Tag design rules

Keep tags minimal and practical. A media tag should usually be:

- visually identifiable from the image/video
- concrete
- reusable across multiple contexts
- something different users would tag consistently
- not already expressible with existing tags

Good tag candidates:

- body parts
- clothing
- toys/tools
- visible accessories
- visible actions
- participant traits that are visually identifiable
- broad fetish/category markers when they are visually meaningful

Avoid tags for:

- emotional tone
- implied story
- dialogue intent
- hidden motivations
- user preference
- progression state
- concepts that are already implied by another tag
- concepts that can be inferred from a combination of existing tags/settings

For most fetishes or content areas, prefer **one broad media tag** plus settings and derived contexts instead of many narrow tags.

Example: if a content area already has one visible broad tag, avoid adding extra tags for implied roles, tone, or narrative framing. Use settings or derived context rules instead.

## Lines and vocabulary

Line content goes under:

```txt
lines/Scripts/
lines/Vocab/
```

Use `Extend` to add to existing content:

```txt
lines/Scripts/Extend/exampleScript.txt
lines/Vocab/Extend/exampleVocab.txt
```

Use `Base` only when intentionally replacing a base file:

```txt
lines/Scripts/Base/exampleScript.txt
lines/Vocab/Base/exampleVocab.txt
```

Most mods should prefer `Extend`.

Example script line file:

```txt
ISSETTING:latexPreference
That outfit theme seems to suit you.

ISNOSETTING:latexPreference
Maybe that style is not for you yet.
```

Example vocab file:

```txt
sleek
shiny
restrictive
polished
```

`lines/Vocab/Extend/tags.txt` can also provide legacy custom tags, but prefer `tags/tags.json` for new mods because it supports groups.

## Writing in the OpenEdge tone

Mods should try to sound like they belong beside the built-in scripts. OpenEdge's line style is direct, conversational, teasing, and second-person. It usually sounds like a dominant person talking live to the user, not like polished prose or narration.

### Core voice principles

OpenEdge lines usually:

- speak directly to the user as `you`
- use short, punchy lines
- use lower-case sentence starts often
- sound conversational rather than literary
- mix teasing, control, and casual observation
- use concrete body/control language instead of abstract emotional analysis
- frequently reference the user's role with placeholders like `@subTitle`, `@missTitle`, `@cock`, `@balls`, `@boy`
- make commands feel immediate
- ask leading questions
- treat user reactions as obvious/predictable
- use occasional profanity or blunt phrasing where appropriate

OpenEdge lines usually avoid:

- long paragraphs
- therapy-like language
- formal exposition
- overly polished romance-novel narration
- explaining the entire psychology of a fetish at once
- repeating the same abstract words too often, such as "dynamic", "context", "identity", "consent", "journey"
- sounding like documentation inside a script

### Cadence

Prefer one idea per line:

```txt
you're back again
I wonder what dragged you here this time
was it me?
or was it that needy @cock of yours?
```

Avoid dense paragraph-style writing:

```txt
You have returned once again because your complicated relationship with desire and control has led you to seek out this experience as a means of exploring your submissive identity.
```

A good block often alternates between:

1. observation
2. teasing interpretation
3. direct command or question
4. consequence

Example:

```txt
look at you
trying to act calm
but your @cock always gives you away
so stop pretending and listen
```

### Use placeholders

Use existing placeholders so lines adapt to user settings and match core scripts.

Common placeholders include:

```txt
@missTitle
@subTitle
@cock
@balls
@boy
```

Examples:

```txt
that's a good @subTitle
keep your hands away from your @cock
are you listening to your @missTitle?
```

Prefer placeholders over hardcoded titles when possible.

### Keep it conversational

OpenEdge often sounds casual and immediate:

Good:

```txt
well that's interesting
you got quiet very quickly
I think I found the right nerve
```

Less fitting:

```txt
Your silence indicates that this particular theme has resonated with you on a deep psychological level.
```

Good:

```txt
don't look so surprised
this is exactly what I expected from you
```

Less fitting:

```txt
Your reaction is understandable given the established pattern of your preferences.
```

### Be specific, not abstract

Use concrete details:

```txt
that @cock is already twitching
keep your hands on your thighs
look at the screen and don't look away
```

Avoid vague analysis:

```txt
this experience is activating several layers of your submissive response
```

### Teasing without over-explaining

Core OpenEdge scripts often imply why something works without explaining every detail.

Good:

```txt
I barely said anything and you're already reacting
that's almost cute
```

Less fitting:

```txt
The reason this works is because you associate verbal acknowledgement with permission to experience arousal.
```

### Questions and answers

Questions should sound leading and in-character.

Good:

```txt
ASK:you like when I say it out loud don't you?
[yes @missTitle]
that's what I thought
[no @missTitle]
then why did you get so quiet?
```

Less fitting:

```txt
ASK:would you like to enable this optional content preference?
[yes]
Preference enabled.
[no]
Preference disabled.
```

Even when a question exists to set a mod setting, keep the wording in-character.

Example:

```txt
ASK:should I tease you like this more often?
[yes @missTitle]
I knew you would
FLAG:examplePreference
[no @missTitle]
fine, I'll leave that part alone for now
FLAG:examplePreferenceNo
```

### Commands and action lines

Commands should be clear and immediate:

```txt
STOPSTROKING:
STROKESLOW:
STROKENORMAL:
STROKEFAST:
EDGE:
```

Then follow with direct instruction:

```txt
STOPSTROKING:
hands off
look at the screen
I want your full attention for this
```

### Tone range

OpenEdge can be harsh, affectionate, teasing, bored, amused, or controlling. Pick a clear tone for a mod and keep it consistent.

Examples:

Affectionate teasing:

```txt
you're such an easy little thing
but I like that about you
it means I don't have to work very hard
```

Matter-of-fact dominance:

```txt
this is simple
you obey
I decide what happens next
```

Mocking:

```txt
was that supposed to be self control?
that's almost adorable
```

Avoid mixing too many tones in one block. A line can be warm and cruel, but it should not suddenly switch from nurturing to formal explanation.

### Use settings and tags in-character

Do not expose implementation details to the user.

Less fitting:

```txt
Your latexPreference setting is enabled, so this line is now available.
```

Better:

```txt
I know you like this look
there's no point pretending you don't
```

Less fitting:

```txt
This media has the Latex and Bondage tags.
```

Better:

```txt
look at that
all wrapped up and helpless
I can see why it gets your attention
```

### Good block shape

A strong mod block often looks like this:

```txt
ISSETTING:examplePreference SETTAGGEDMEDIA:Latex,Bondage
{
STOPSTROKING:
look at the screen
that's the kind of thing that gets under your skin isn't it?
all that shine
all that restraint
and you still try to act like you're in control
cute
}
```

Why it works:

- condition is outside the spoken text
- line starts with an action command
- short direct sentences
- concrete visual reference
- direct teasing
- no explanation of implementation details

### Common rewrites

Too polished:

```txt
You seem to be experiencing a complicated mixture of arousal and vulnerability.
```

More OpenEdge-like:

```txt
you look nervous
that usually means I'm close to something useful
```

Too abstract:

```txt
This fantasy represents your desire to surrender agency.
```

More OpenEdge-like:

```txt
you really do like when I take the decision away from you
```

Too gentle/documentary:

```txt
It is okay for you to enjoy this preference.
```

More OpenEdge-like:

```txt
you can stop acting ashamed
we both know you like this
```

Too verbose:

```txt
I want you to focus on the visual content currently being displayed and consider how it relates to your established interests.
```

More OpenEdge-like:

```txt
look at the screen
that's exactly your kind of trouble isn't it?
```

### Final style checklist

Before adding a script block, ask:

1. Does this sound like someone speaking live to the user?
2. Are most lines short?
3. Did I use `you`, direct commands, or leading questions?
4. Did I avoid explaining implementation details?
5. Did I use placeholders where appropriate?
6. Is the fetish/theme concrete instead of abstract?
7. Does the tone match the mod's intended personality?
8. Could this sit beside a core OpenEdge script without sounding like documentation?

If not, rewrite it shorter and more direct.

## How scripts, settings, asks, and media tags interact

Script files are made of conditional blocks. The text before `{ ... }` decides whether the block is eligible to run.

Example:

```txt
ISSETTING:latexPreference
{
That outfit theme seems to suit you.
}
```

This block can run only if the `latexPreference` setting is enabled.

### Settings predicates

Settings represent user preferences, progression, tone, intensity, identity, or narrative state. They are separate from media tags.

Common setting predicates:

```txt
ISSETTING:latexPreference
ISNOSETTING:latexPreference
ISSETTINGASKED:latexPreference
ISNOSETTINGASKED:latexPreference
ISNOSETTINGDECLINED:latexPreference
SETTINGVALUEATLEAST:latexIntensity,3
SETTINGTEXTSET:favoriteOutfit
SETTINGTEXTEMPTY:favoriteOutfit
```

Examples:

```txt
ISSETTING:latexPreference ISSETTING:latexSoftTone
{
You can enjoy this gently, without needing it to feel harsh.
}
```

```txt
ISSETTING:latexPreference SETTINGVALUEATLEAST:latexIntensity,3
{
You seem ready for a stronger version of this theme.
}
```

### Media tag predicates

Media tags represent what is visible in tagged images/videos. They are edited in the tagger and stored canonically in:

```txt
runtime/local/app/media-tag-index.json
```

`runtime/local/app/tags.txt` is optional legacy input only. OpenEdge reads it for compatibility when present, but does not create or write it.

Scripts can check whether enough matching media exists:

```txt
TAG:Latex
IMGTAG:Latex
VIDTAG:Latex
```

Combinations are comma-separated:

```txt
TAG:Latex,Bondage
```

Exclusions use `!`:

```txt
TAG:Latex,Bondage,!Caption
```

Use tag predicates when the script needs matching media to exist before the line is eligible.

### Showing lines with certain media

Use `SETTAGGEDMEDIA:` when a block should both require matching media and temporarily bias the displayed media toward those tags.

```txt
SETTAGGEDMEDIA:Latex
{
Let me put something suitable on screen while I talk about this.
}
```

Combined tags work here too:

```txt
ISSETTING:latexPreference SETTAGGEDMEDIA:Latex,Bondage
{
There. Something visually appropriate, and a setting that says this theme is welcome.
}
```

This means:

- `latexPreference` must be enabled.
- enough media must be tagged with both `Latex` and `Bondage`.
- while this block plays, the session media is temporarily pulled toward that matching media.

Use `TAG:` when you only need to know media exists. Use `SETTAGGEDMEDIA:` when the line should actively guide what media appears.

### Asking questions and writing settings

Scripts can ask questions with `ASK:` and answer branches:

```txt
ASK:do you like this theme?
[yes]
FLAG:latexPreference
[no]
FLAG:latexPreferenceNo
```

For a mod setting like this:

```json
{
  "key": "latexPreference",
  "label": "Latex Preference",
  "kind": "Toggle",
  "legacyEnabledFlag": "latexPreference",
  "legacyDisabledFlag": "latexPreferenceNo"
}
```

writing the legacy flags can promote into canonical setting state:

```txt
FLAG:latexPreference
FLAG:latexPreferenceNo
```

That means future script checks can use:

```txt
ISSETTING:latexPreference
ISNOSETTING:latexPreference
ISSETTINGASKED:latexPreference
```

For numeric/text settings, use legacy value keys and supported setting-value predicates where possible.

### Progression variables

Some older structured flows use variables such as:

```txt
V=0:someProgressVar
{
...
SETVAR:someProgressVar,1
}
```

This means the block is tied to a progression value. Mods can still use existing script variables, but prefer simple mod settings for new preference/tone switches unless you specifically need staged progression.

### Practical examples

Setting-only line:

```txt
ISSETTING:latexPreference ISSETTING:latexSoftTone
{
You can enjoy this softly, without needing it to feel severe.
}
```

Media-gated line:

```txt
TAG:Latex
{
This line only appears if there is enough latex-tagged media.
}
```

Media-directed line:

```txt
SETTAGGEDMEDIA:Latex
{
This line also nudges the displayed media toward latex-tagged media.
}
```

Settings plus media:

```txt
ISSETTING:latexPreference SETTAGGEDMEDIA:Latex,Bondage
{
This requires the preference and matching visual media.
}
```

Settings plus existing related settings:

```txt
ISSETTING:latexPreference ISSETTING:chastity SETTAGGEDMEDIA:Latex,Chastity
{
This uses a mod setting, an existing built-in setting, and existing visual tags together.
}
```

For repeated combinations, prefer derived contexts instead of creating extra tags for implied concepts.

## Derived contexts / virtual meanings

Some ideas should not become media tags. Instead, they can be inferred from existing tags and settings.

Example: a mod may want a “latex restraint context” without asking users to tag a separate `latexRestraint` tag. That context can be represented as:

```txt
Tags: Latex + Bondage
Settings: latexPreference enabled
```

Context files live at:

```txt
mods/mod-id/contexts/contexts.json
```

Current context shape:

```json
{
  "contexts": [
    {
      "key": "latexRestraint",
      "label": "Latex Restraint Context",
      "settings": ["latexPreference"],
      "mediaTags": ["Latex", "Bondage"],
      "minimumMedia": 2
    }
  ]
}
```

Current fields:

- `key`: script-facing context key.
- `label`: human-readable description.
- `settings`: all listed settings must be enabled.
- `mediaTags`: all listed media tags must be present on matching media.
- `minimumMedia`: minimum matching media count; defaults to 2.

Script predicates:

```txt
ISCONTEXT:latexRestraint
{
This line appears only if the context is active.
}

SETCONTEXTMEDIA:latexRestraint
{
This line appears only if the context is active, and temporarily selects media matching the context tags.
}
```

Contexts do not appear as tag buttons. They are virtual meanings used by scripts/media selection logic.

This keeps tagging lightweight while still allowing expressive scripts.

## Mod hooks and scripted outcomes

OpenEdge also supports early hook/outcome metadata for more advanced mods. Full design notes live in `docs/modding/hook-framework.md`.

Hook registrations live at:

```txt
mods/mod-id/hooks/hooks.json
```

Outcome definitions live at:

```txt
mods/mod-id/outcomes/outcomes.json
```

Implemented official hook entrypoints:

```txt
sessionIntro
methodPicker
changeState
sessionEnd
edgeOpportunity
orgasmDecision
```

Mods can also emit custom hook names from scripts with `RUNHOOK:`. Hook names are string-based, so another mod can register a handler for a hook emitted by your mod.

Example hook file:

```json
{
  "hooks": [
    {
      "hook": "methodPicker",
      "script": "customModule",
      "mode": "exclusive",
      "weight": 1,
      "requiresSettings": ["customMode"]
    }
  ]
}
```

Currently useful modes:

- `exclusive`: eligible handler runs before base behavior for official hooks.
- `replace`: treated like `exclusive` for currently implemented hooks.
- `additive`: joins the weighted base pool for `methodPicker`; for script-emitted hooks it can be selected by the generic hook resolver.
- `fallback`: metadata is accepted, but richer fallback behavior is still planned.

Advanced script commands:

```txt
RUNSCRIPT:scriptName
RUNHOOK:hookName[,fallbackScript]
HOOKHANDLED[:reason]
OUTCOME:kind,key
```

`RUNSCRIPT:` runs a named script from base or enabled mod line roots without requiring a new app-code script class.

`RUNHOOK:` emits a hook that enabled mods can handle. You can optionally provide a fallback script to run when no eligible hook handler is selected. Example:

```txt
RUNHOOK:myMod.beforeCustomFlow
RUNHOOK:myMod.customFlowBody,defaultCustomFlowBody
RUNHOOK:myMod.afterCustomFlow
```

`HOOKHANDLED:` marks the active hook as handled without applying an outcome. Use it when a hook script successfully handled control flow or flavor text.

`OUTCOME:` applies a declared outcome and also marks the active hook as handled.

Example outcome file:

```json
{
  "outcomes": [
    {
      "key": "customEdge",
      "kind": "edge",
      "label": "Custom Edge",
      "countsAsEdge": true,
      "usesNormalStroking": false,
      "allowedWhileChaste": true,
      "state": "module"
    }
  ]
}
```

Example script line:

```txt
OUTCOME:edge,customEdge
```

Outcome effects are conservative and explicit. If an outcome is not marked `allowedWhileChaste`, it will be skipped while `wearingChastity` is enabled.

This system is still being expanded. For now, prefer small test mods and check `runtime/local/app/debug/session-trace.log` for `mod-hooks` and `mod-outcome` entries when debugging.

The Mods manager warns about common hook/outcome issues, including missing hook names/scripts, unknown hook modes, duplicate outcome keys, missing hook script files, and enabled `exclusive`/`replace` hook conflicts that will be resolved by priority.

## Practical mod design checklist

Before adding a tag, ask:

1. Can the user see this directly in the media?
2. Would different people tag it consistently?
3. Is it concrete rather than narrative?
4. Is it not already implied by an existing tag?
5. Can it not be inferred from existing tags/settings?

If the answer is no, use a setting, vocab, script predicate, or derived context instead.

Before adding a setting, ask:

1. Is this about user preference, consent, progression, intensity, or tone?
2. Would scripts need to check it directly?
3. Should it appear in the Settings menu?
4. Should it support queued asks?

If yes, a simple mod setting is probably appropriate.

## Example fixtures

Framework fixture mods are not shipped in the main source tree or release zip. Keep local test fixtures under `runtime/local/app/mods/` while developing. If reusable examples are needed, publish them separately as a modding tools/example pack.

## Testing a mod

1. Put the mod folder under `runtime/local/app/mods/`.
2. Open OpenEdge.
3. Open the Mods manager from the main menu.
4. Confirm the mod is detected.
5. Enable it if needed.
6. Restart OpenEdge after enabling/disabling mods so startup-loaded settings refresh everywhere.
7. Check:
   - Settings menu for new settings.
   - Tagger/Bulk Operations for new tags.
   - Script behavior for new lines/vocab.

## Current limitations

- Mods are loaded at startup/page construction; restart after enable/disable for best results.
- Dynamic simple settings are supported; complex structured settings still require app code.
- Dynamic tags are supported.
- Mod line roots are supported.
- Derived context JSON is supported for `ISCONTEXT:` and `SETCONTEXTMEDIA:` script predicates.
- First-class hooks/outcomes are partially implemented; `sessionIntro`, `methodPicker`, `changeState`, `sessionEnd`, `edgeOpportunity`, `orgasmDecision`, `RUNSCRIPT:`, `RUNHOOK:`, and `OUTCOME:` are available. Additive/base pooling currently applies to `methodPicker`; richer pooling and fallback behavior are still planned.
- Invalid JSON reporting and hook/outcome warnings are still first-pass; keep files valid and simple.
