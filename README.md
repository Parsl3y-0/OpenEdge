# OpenEdge

OpenEdge is a recovered and actively maintained WPF desktop application targeting **.NET 8 for Windows**. The repository is organized so the editable source, canonical assets, third-party DLLs, and local runtime data are kept separate.

> **Note**
> OpenEdge is a Windows-only application. The runtime folder can contain personal settings, tasks, media paths, and tag data; treat it as private user data.

## Current status

- Source project builds cleanly on the current baseline.
- Build output is written to `runtime/local/app/`.
- Runtime paths are centralized through `RuntimePaths.cs`.
- Media sources are configurable at runtime through `media-sources.json`.
- Media tags are stored canonically in `media-tag-index.json`.
- Legacy EverEdge/OpenEdge text files are supported where needed for import and compatibility.

## Requirements

- Windows
- .NET 8 SDK
- PowerShell, for the smoke-check script

The project uses repo-owned DLL dependencies from `vendor/dotnet/`; do not replace them with NuGet packages unless intentionally changing the dependency model.

## Quick start

From the repository root:

```powershell
dotnet build src/OpenEdge/OpenEdge.csproj
```

After a successful build, run:

```powershell
runtime/local/app/OpenEdge.exe
```

To verify the canonical baseline:

```powershell
powershell -ExecutionPolicy Bypass -File docs/recovery/smoke-check.ps1
```

The smoke check builds the project, verifies the output layout, launches the rebuilt app briefly, and confirms startup writes `runtime/local/app/flags/temp/open.txt`.

## Repository layout

| Path | Purpose |
|---|---|
| `src/OpenEdge/` | Editable WPF source project. |
| `Data/resources/` | Canonical image/resource assets copied into build output. |
| `Data/audio/` | Canonical audio assets copied into build output. |
| `Data/lines/` | Canonical script/vocabulary line data copied into build output. |
| `vendor/dotnet/` | Repo-owned binary DLL dependencies. |
| `runtime/local/app/` | Local build output and private runtime/user data. Gitignored. |
| `docs/recovery/` | Recovery notes, smoke checks, compatibility docs, and stabilization history. |

## Runtime data and upgrade safety

`runtime/local/` is intentionally gitignored. A normal source update and rebuild should not reset user data as long as this folder is preserved.

Important runtime files include:

| Runtime path | Purpose |
|---|---|
| `options.txt` | UI/user options such as volume, speed, fullscreen, and related preferences. |
| `tasks.txt` | Saved homework/task state. |
| `tagGroups.txt` | Image tagger quick-tag groups. |
| `flags/*.txt` | Legacy setting/progression flags still used by parts of the app. |
| `compatibility-state.json` | Newer compatibility registry for persistent flag/state values. |
| `media-sources.json` | Configured media source folders. |
| `media-tag-index.json` | Canonical media identity and tag store. |
| `tags.txt` | Optional legacy media tag input. Read for compatibility, but not created or written by OpenEdge. |

When upgrading manually, preserve `runtime/local/` or export user data first from:

```text
Settings -> Migration & Diagnostics -> Export User Data
```

## EverEdge migration

Older EverEdge installs stored user data in text files under `Data/` and flags under `Data/flags/`. OpenEdge includes a migration action at:

```text
Settings -> Migration & Diagnostics -> Import Data from EverEdge
```

You can select either the EverEdge install folder or the EverEdge `Data` folder. The importer copies supported settings/tasks/flags into the current runtime layout and imports legacy media tags into the canonical `media-tag-index.json` store instead of recreating `tags.txt`.

## Media sources and tags

Media source configuration lives in `runtime/local/app/media-sources.json` and can be edited from the app through:

```text
Settings -> Media Sources
```

By default, OpenEdge creates legacy local sources for:

- `runtime/local/app/images/`
- `runtime/local/app/videos/`

Additional folders can be added without copying media into the app directory. Tags are stored by media identity in `media-tag-index.json`, allowing tags to survive some file moves or renames inside configured sources.

## Diagnostics

For support/debugging, use:

```text
Settings -> Migration & Diagnostics -> Export Diagnostics
```

This creates a diagnostics bundle under `runtime/local/app/debug/`. It may include local file paths and other private runtime state, so review or redact it before sharing.

## Release packaging

Create a release zip from a clean, allowlisted payload with:

```powershell
powershell -ExecutionPolicy Bypass -File docs/release/package-release.ps1 -Version 0.1.0
```

The script publishes the app, stages only release-safe files, blocks user-state files, and leaves only the final zip in the versioned release folder:

```text
artifacts/releases/OpenEdge-<version>-win-x86/OpenEdge-<version>-win-x86.zip
```

Release zips intentionally exclude user data such as `options.txt`, `tasks.txt`, `flags/`, `media-sources.json`, and `media-tag-index.json`.

## Development workflow

Recommended baseline loop:

1. Make a small, focused change.
2. Build:

   ```powershell
   dotnet build src/OpenEdge/OpenEdge.csproj
   ```

3. Run the smoke check:

   ```powershell
   powershell -ExecutionPolicy Bypass -File docs/recovery/smoke-check.ps1
   ```

4. Manually verify any UI flow touched by the change.

Avoid committing files under `runtime/local/`; that directory contains local output and user data.

## Documentation

- Recovery overview: `docs/recovery/README.md`
- Stabilization backlog: `docs/recovery/phase-1-baseline.md`
- Compatibility contract: `docs/recovery/compatibility-contract.md`
- Tag identity notes: `docs/recovery/tag-identity.md`
- Modding notes: `docs/modding/CONTRIBUTING.md`

## License

OpenEdge is distributed under the GNU General Public License v3. See `LICENSE.md`.
