using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenEdge;

public static class ModService
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public static IReadOnlyList<LoadedMod> LoadEnabledMods()
	{
		return LoadAllMods().Where((LoadedMod mod) => mod.Manifest.Enabled).ToList();
	}

	public static IReadOnlyList<LoadedMod> LoadAllMods()
	{
		EnsureModsDirectory();
		Dictionary<string, int> priorities = LoadModPriorities();
		List<LoadedMod> loadedMods = new List<LoadedMod>();
		foreach (string modDirectory in Directory.GetDirectories(RuntimePaths.ModsDir).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
		{
			LoadedMod loadedMod = TryLoadMod(modDirectory, priorities, out _);
			if (loadedMod != null)
			{
				loadedMods.Add(loadedMod);
			}
		}
		return SortModsByPriority(loadedMods).ToList();
	}

	public static IReadOnlyList<ModSummary> GetModSummaries()
	{
		EnsureModsDirectory();
		Dictionary<string, int> priorities = LoadModPriorities();
		List<ModSummary> summaries = new List<ModSummary>();
		List<LoadedMod> loadedMods = new List<LoadedMod>();
		foreach (string modDirectory in Directory.GetDirectories(RuntimePaths.ModsDir).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
		{
			LoadedMod loadedMod = TryLoadMod(modDirectory, priorities, out string error);
			if (loadedMod == null)
			{
				summaries.Add(CreateErrorSummary(modDirectory, error));
				continue;
			}
			loadedMods.Add(loadedMod);
		}
		foreach (LoadedMod loadedMod in SortModsByPriority(loadedMods))
		{
			summaries.Add(new ModSummary
			{
				RootPath = loadedMod.RootPath,
				Id = loadedMod.Manifest.Id,
				Name = loadedMod.Manifest.Name,
				Author = loadedMod.Manifest.Author,
				Version = loadedMod.Manifest.Version,
				Enabled = loadedMod.Manifest.Enabled,
				Priority = loadedMod.Priority,
				SettingCount = loadedMod.Settings.Count,
				TagCount = loadedMod.Tags.Count,
				ContextCount = loadedMod.Contexts.Count,
				HookCount = loadedMod.Hooks.Count,
				OverrideHookCount = loadedMod.Hooks.Count((ModHookDefinition hook) => string.Equals(hook.Mode, "exclusive", StringComparison.OrdinalIgnoreCase) || string.Equals(hook.Mode, "replace", StringComparison.OrdinalIgnoreCase)),
				OutcomeCount = loadedMod.Outcomes.Count,
				HasLines = IsUsableLineRoot(loadedMod.LinesRootPath),
				Error = ValidateLoadedMod(loadedMod)
			});
		}
		AppendGlobalWarnings(summaries, loadedMods, loadedMods.Where((LoadedMod mod) => mod.Manifest.Enabled).ToList());
		return summaries;
	}

	public static IReadOnlyList<string> GetEnabledLineRoots()
	{
		return LoadEnabledMods().Select((LoadedMod mod) => mod.LinesRootPath).Where(IsUsableLineRoot).ToList();
	}

	public static IReadOnlyList<SettingDefinition> GetEnabledSettingDefinitions()
	{
		return LoadEnabledMods().SelectMany((LoadedMod mod) => mod.Settings).ToList();
	}

	public static IReadOnlyList<ModTagDefinition> GetEnabledTagDefinitions()
	{
		return LoadEnabledMods().SelectMany((LoadedMod mod) => mod.Tags).ToList();
	}

	public static IReadOnlyList<DerivedContextDefinition> GetEnabledContextDefinitions()
	{
		return LoadEnabledMods().SelectMany((LoadedMod mod) => mod.Contexts).ToList();
	}

	public static IReadOnlyList<ModHookDefinition> GetEnabledHookDefinitions()
	{
		return LoadEnabledMods().SelectMany((LoadedMod mod) => mod.Hooks).ToList();
	}

	public static IReadOnlyList<ModOutcomeDefinition> GetEnabledOutcomeDefinitions()
	{
		return LoadEnabledMods().SelectMany((LoadedMod mod) => mod.Outcomes).ToList();
	}

	public static void EnsureModsDirectory()
	{
		Directory.CreateDirectory(RuntimePaths.ModsDir);
	}

	public static void SetModEnabled(string modRootPath, bool enabled)
	{
		Directory.CreateDirectory(modRootPath);
		ModManifest manifest = LoadManifest(modRootPath);
		if (string.IsNullOrWhiteSpace(manifest.Id))
		{
			manifest.Id = Path.GetFileName(modRootPath);
		}
		if (string.IsNullOrWhiteSpace(manifest.Name))
		{
			manifest.Name = manifest.Id;
		}
		manifest.Enabled = enabled;
		SaveManifest(modRootPath, manifest);
	}

	public static void MoveModPriority(string modId, int direction)
	{
		if (string.IsNullOrWhiteSpace(modId) || direction == 0)
		{
			return;
		}
		EnsureModsDirectory();
		Dictionary<string, int> priorities = LoadModPriorities();
		List<LoadedMod> mods = new List<LoadedMod>();
		foreach (string modDirectory in Directory.GetDirectories(RuntimePaths.ModsDir).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
		{
			LoadedMod loadedMod = TryLoadMod(modDirectory, priorities, out _);
			if (loadedMod != null)
			{
				mods.Add(loadedMod);
			}
		}
		mods = SortModsByPriority(mods).ToList();
		int index = mods.FindIndex((LoadedMod mod) => string.Equals(mod.Manifest.Id, modId, StringComparison.OrdinalIgnoreCase));
		if (index < 0)
		{
			return;
		}
		int newIndex = Math.Max(0, Math.Min(mods.Count - 1, index + direction));
		if (newIndex == index)
		{
			return;
		}
		LoadedMod moving = mods[index];
		mods.RemoveAt(index);
		mods.Insert(newIndex, moving);
		SaveModPriorities(mods);
	}

	public static string CreateExampleMod()
	{
		EnsureModsDirectory();
		string modRoot = Path.Combine(RuntimePaths.ModsDir, "example-mod");
		Directory.CreateDirectory(Path.Combine(modRoot, "settings"));
		Directory.CreateDirectory(Path.Combine(modRoot, "tags"));
		Directory.CreateDirectory(Path.Combine(modRoot, "contexts"));
		Directory.CreateDirectory(Path.Combine(modRoot, "hooks"));
		Directory.CreateDirectory(Path.Combine(modRoot, "outcomes"));
		Directory.CreateDirectory(Path.Combine(modRoot, "lines", "Scripts", "Extend"));
		Directory.CreateDirectory(Path.Combine(modRoot, "lines", "Vocab", "Extend"));
		SaveManifest(modRoot, new ModManifest
		{
			Id = "example-mod",
			Name = "Example Mod",
			Author = "OpenEdge",
			Version = "1.0.0",
			Enabled = false
		});
		WriteIfMissing(Path.Combine(modRoot, "settings", "settings.json"), "{\r\n  \"settings\": [\r\n    {\r\n      \"key\": \"latex\",\r\n      \"label\": \"Latex\",\r\n      \"kind\": \"Toggle\",\r\n      \"legacyEnabledFlag\": \"latex\",\r\n      \"legacyDisabledFlag\": \"latexNo\",\r\n      \"queueableAsk\": true,\r\n      \"mediaDiscoveryTags\": [\"Latex\"],\r\n      \"mediaDiscoveryMinimum\": 2\r\n    }\r\n  ]\r\n}\r\n");
		WriteIfMissing(Path.Combine(modRoot, "tags", "tags.json"), "{\r\n  \"tags\": [\r\n    {\r\n      \"key\": \"latex\",\r\n      \"label\": \"Latex\",\r\n      \"group\": \"Fetishes\"\r\n    }\r\n  ]\r\n}\r\n");
		WriteIfMissing(Path.Combine(modRoot, "contexts", "contexts.json"), "{\r\n  \"contexts\": [\r\n    {\r\n      \"key\": \"latexBondage\",\r\n      \"label\": \"Latex Bondage\",\r\n      \"settings\": [\"latex\"],\r\n      \"mediaTags\": [\"Latex\", \"Bondage\"],\r\n      \"minimumMedia\": 2\r\n    }\r\n  ]\r\n}\r\n");
		WriteIfMissing(Path.Combine(modRoot, "hooks", "hooks.json"), "{\r\n  \"hooks\": [\r\n    {\r\n      \"hook\": \"methodPicker\",\r\n      \"script\": \"example\",\r\n      \"mode\": \"additive\",\r\n      \"weight\": 1,\r\n      \"requiresSettings\": [\"latex\"]\r\n    }\r\n  ]\r\n}\r\n");
		WriteIfMissing(Path.Combine(modRoot, "outcomes", "outcomes.json"), "{\r\n  \"outcomes\": [\r\n    {\r\n      \"key\": \"exampleOutcome\",\r\n      \"kind\": \"state\",\r\n      \"label\": \"Example Outcome\",\r\n      \"state\": \"module\"\r\n    }\r\n  ]\r\n}\r\n");
		WriteIfMissing(Path.Combine(modRoot, "lines", "Vocab", "Extend", "tags.txt"), "Latex\r\n");
		WriteIfMissing(Path.Combine(modRoot, "lines", "Scripts", "Extend", "example.txt"), "This is an example mod line.\r\n");
		return modRoot;
	}

	private static LoadedMod TryLoadMod(string modDirectory, Dictionary<string, int> priorities, out string error)
	{
		error = "";
		try
		{
			ModManifest manifest = LoadManifest(modDirectory);
			if (string.IsNullOrWhiteSpace(manifest.Id))
			{
				manifest.Id = Path.GetFileName(modDirectory);
			}
			if (string.IsNullOrWhiteSpace(manifest.Name))
			{
				manifest.Name = manifest.Id;
			}
			return new LoadedMod
			{
				RootPath = modDirectory,
				Manifest = manifest,
				Priority = GetPriority(manifest.Id, priorities),
				Settings = LoadSettings(modDirectory, manifest),
				Tags = LoadTags(modDirectory),
				Contexts = LoadContexts(modDirectory),
				Hooks = LoadHooks(modDirectory, manifest),
				Outcomes = LoadOutcomes(modDirectory, manifest),
				LinesRootPath = Path.Combine(modDirectory, "lines")
			};
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return null;
		}
	}

	private static ModManifest LoadManifest(string modDirectory)
	{
		string manifestPath = Path.Combine(modDirectory, "mod.json");
		if (!File.Exists(manifestPath))
		{
			return new ModManifest
			{
				Id = Path.GetFileName(modDirectory),
				Name = Path.GetFileName(modDirectory),
				Enabled = true
			};
		}
		return ReadJsonFile<ModManifest>(manifestPath) ?? new ModManifest();
	}

	private static List<SettingDefinition> LoadSettings(string modDirectory, ModManifest manifest)
	{
		string settingsPath = Path.Combine(modDirectory, "settings", "settings.json");
		if (!File.Exists(settingsPath))
		{
			return new List<SettingDefinition>();
		}
		ModSettingsFile settingsFile = ReadJsonFile<ModSettingsFile>(settingsPath);
		if (settingsFile?.Settings == null)
		{
			return new List<SettingDefinition>();
		}
		return settingsFile.Settings.Select((ModSettingDefinition setting) => ToSettingDefinition(setting, manifest)).Where((SettingDefinition definition) => !string.IsNullOrWhiteSpace(definition.Key)).ToList();
	}

	private static SettingDefinition ToSettingDefinition(ModSettingDefinition modSetting, ModManifest manifest)
	{
		string sourceName = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name;
		return new SettingDefinition
		{
			Key = CleanKey(modSetting.Key),
			Label = string.IsNullOrWhiteSpace(modSetting.Label) ? CleanKey(modSetting.Key) : modSetting.Label.Trim(),
			Kind = ParseSettingKind(modSetting.Kind),
			LegacyEnabledFlag = CleanKey(modSetting.LegacyEnabledFlag),
			LegacyDisabledFlag = CleanKey(modSetting.LegacyDisabledFlag),
			LegacyValueKey = CleanKey(modSetting.LegacyValueKey),
			QueueableAsk = modSetting.QueueableAsk,
			MediaDiscoveryTags = (modSetting.MediaDiscoveryTags ?? new List<string>()).Where((string tag) => !string.IsNullOrWhiteSpace(tag)).Select((string tag) => tag.Trim()).ToList(),
			MediaDiscoveryMinimum = modSetting.MediaDiscoveryMinimum <= 0 ? 2 : modSetting.MediaDiscoveryMinimum,
			Description = (modSetting.Description ?? "").Trim(),
			ProgressionNote = string.IsNullOrWhiteSpace(modSetting.Warning) ? "Added by an enabled mod." : modSetting.Warning.Trim(),
			Group = (modSetting.Group ?? "").Trim(),
			SourceName = sourceName.Trim()
		};
	}

	private static List<ModTagDefinition> LoadTags(string modDirectory)
	{
		string tagsPath = Path.Combine(modDirectory, "tags", "tags.json");
		if (!File.Exists(tagsPath))
		{
			return new List<ModTagDefinition>();
		}
		ModTagsFile tagsFile = ReadJsonFile<ModTagsFile>(tagsPath);
		if (tagsFile?.Tags == null)
		{
			return new List<ModTagDefinition>();
		}
		return tagsFile.Tags.Where((ModTagDefinition tag) => !string.IsNullOrWhiteSpace(tag.Key) || !string.IsNullOrWhiteSpace(tag.Label)).ToList();
	}

	private static List<DerivedContextDefinition> LoadContexts(string modDirectory)
	{
		string contextsPath = Path.Combine(modDirectory, "contexts", "contexts.json");
		if (!File.Exists(contextsPath))
		{
			return new List<DerivedContextDefinition>();
		}
		ModContextsFile contextsFile = ReadJsonFile<ModContextsFile>(contextsPath);
		if (contextsFile?.Contexts == null)
		{
			return new List<DerivedContextDefinition>();
		}
		return contextsFile.Contexts.Where((DerivedContextDefinition context) => !string.IsNullOrWhiteSpace(context.Key)).ToList();
	}

	private static List<ModHookDefinition> LoadHooks(string modDirectory, ModManifest manifest)
	{
		string hooksPath = Path.Combine(modDirectory, "hooks", "hooks.json");
		if (!File.Exists(hooksPath))
		{
			return new List<ModHookDefinition>();
		}
		ModHooksFile hooksFile = ReadJsonFile<ModHooksFile>(hooksPath);
		if (hooksFile?.Hooks == null)
		{
			return new List<ModHookDefinition>();
		}
		List<ModHookDefinition> hooks = hooksFile.Hooks.Where((ModHookDefinition hook) => hook != null).ToList();
		foreach (ModHookDefinition hook in hooks)
		{
			hook.SourceModId = manifest.Id;
			hook.Hook = CleanKey(hook.Hook);
			hook.Script = CleanKey(hook.Script);
			hook.Mode = CleanKey(hook.Mode);
		}
		return hooks;
	}

	private static List<ModOutcomeDefinition> LoadOutcomes(string modDirectory, ModManifest manifest)
	{
		string outcomesPath = Path.Combine(modDirectory, "outcomes", "outcomes.json");
		if (!File.Exists(outcomesPath))
		{
			return new List<ModOutcomeDefinition>();
		}
		ModOutcomesFile outcomesFile = ReadJsonFile<ModOutcomesFile>(outcomesPath);
		if (outcomesFile?.Outcomes == null)
		{
			return new List<ModOutcomeDefinition>();
		}
		List<ModOutcomeDefinition> outcomes = outcomesFile.Outcomes.Where((ModOutcomeDefinition outcome) => outcome != null).ToList();
		foreach (ModOutcomeDefinition outcome in outcomes)
		{
			outcome.SourceModId = manifest.Id;
			outcome.Key = CleanKey(outcome.Key);
			outcome.Kind = CleanKey(outcome.Kind);
		}
		return outcomes;
	}

	private static SettingKind ParseSettingKind(string kind)
	{
		if (Enum.TryParse(kind, ignoreCase: true, out SettingKind parsed))
		{
			return parsed;
		}
		return SettingKind.Toggle;
	}

	private static string CleanKey(string value)
	{
		return (value ?? "").Trim();
	}

	private static Dictionary<string, int> LoadModPriorities()
	{
		Dictionary<string, int> priorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		if (!File.Exists(RuntimePaths.ModLoadOrderFile))
		{
			return priorities;
		}
		ModLoadOrderFile orderFile = ReadJsonFile<ModLoadOrderFile>(RuntimePaths.ModLoadOrderFile);
		if (orderFile?.Mods == null)
		{
			return priorities;
		}
		foreach (ModPriorityEntry entry in orderFile.Mods)
		{
			if (!string.IsNullOrWhiteSpace(entry.Id))
			{
				priorities[entry.Id.Trim()] = entry.Priority;
			}
		}
		return priorities;
	}

	private static int GetPriority(string modId, Dictionary<string, int> priorities)
	{
		if (!string.IsNullOrWhiteSpace(modId) && priorities.TryGetValue(modId.Trim(), out int priority))
		{
			return priority;
		}
		return 0;
	}

	private static void SaveModPriorities(IReadOnlyList<LoadedMod> mods)
	{
		ModLoadOrderFile orderFile = new ModLoadOrderFile();
		for (int i = 0; i < mods.Count; i++)
		{
			orderFile.Mods.Add(new ModPriorityEntry
			{
				Id = mods[i].Manifest.Id,
				Priority = (mods.Count - i) * 10
			});
		}
		File.WriteAllText(RuntimePaths.ModLoadOrderFile, JsonSerializer.Serialize(orderFile, JsonOptions));
	}

	private static IEnumerable<LoadedMod> SortModsByPriority(IEnumerable<LoadedMod> mods)
	{
		return mods.OrderByDescending((LoadedMod mod) => mod.Priority).ThenBy((LoadedMod mod) => mod.Manifest.Id, StringComparer.OrdinalIgnoreCase).ThenBy((LoadedMod mod) => mod.RootPath, StringComparer.OrdinalIgnoreCase);
	}

	private static void SaveManifest(string modDirectory, ModManifest manifest)
	{
		Directory.CreateDirectory(modDirectory);
		File.WriteAllText(Path.Combine(modDirectory, "mod.json"), JsonSerializer.Serialize(manifest, JsonOptions));
	}

	private static void WriteIfMissing(string path, string content)
	{
		if (!File.Exists(path))
		{
			File.WriteAllText(path, content);
		}
	}

	private static ModSummary CreateErrorSummary(string modDirectory, string error)
	{
		return new ModSummary
		{
			RootPath = modDirectory,
			Id = Path.GetFileName(modDirectory),
			Name = Path.GetFileName(modDirectory),
			Enabled = false,
			Error = error
		};
	}

	private static string ValidateLoadedMod(LoadedMod mod)
	{
		List<string> warnings = new List<string>();
		if (string.IsNullOrWhiteSpace(mod.Manifest.Id))
		{
			warnings.Add("missing id");
		}
		if (mod.Settings.GroupBy((SettingDefinition setting) => setting.Key, StringComparer.OrdinalIgnoreCase).Any((IGrouping<string, SettingDefinition> group) => group.Count() > 1))
		{
			warnings.Add("duplicate setting keys");
		}
		if (mod.Tags.GroupBy((ModTagDefinition tag) => string.IsNullOrWhiteSpace(tag.Key) ? tag.Label : tag.Key, StringComparer.OrdinalIgnoreCase).Any((IGrouping<string, ModTagDefinition> group) => group.Count() > 1))
		{
			warnings.Add("duplicate tag keys");
		}
		if (mod.Contexts.GroupBy((DerivedContextDefinition context) => context.Key, StringComparer.OrdinalIgnoreCase).Any((IGrouping<string, DerivedContextDefinition> group) => group.Count() > 1))
		{
			warnings.Add("duplicate context keys");
		}
		if (mod.Hooks.Any((ModHookDefinition hook) => string.IsNullOrWhiteSpace(hook.Hook)))
		{
			warnings.Add("hook with missing name");
		}
		if (mod.Hooks.Any((ModHookDefinition hook) => string.IsNullOrWhiteSpace(hook.Script)))
		{
			warnings.Add("hook with missing script");
		}
		if (mod.Hooks.Where((ModHookDefinition hook) => !string.IsNullOrWhiteSpace(hook.Hook) && !string.IsNullOrWhiteSpace(hook.Script)).GroupBy((ModHookDefinition hook) => hook.Hook + ":" + hook.Script, StringComparer.OrdinalIgnoreCase).Any((IGrouping<string, ModHookDefinition> group) => group.Count() > 1))
		{
			warnings.Add("duplicate hook registrations");
		}
		if (mod.Hooks.Any((ModHookDefinition hook) => !IsKnownHookMode(hook.Mode)))
		{
			warnings.Add("unknown hook mode");
		}
		if (mod.Outcomes.Any((ModOutcomeDefinition outcome) => string.IsNullOrWhiteSpace(outcome.Kind)))
		{
			warnings.Add("outcome with missing kind");
		}
		if (mod.Outcomes.Any((ModOutcomeDefinition outcome) => string.IsNullOrWhiteSpace(outcome.Key)))
		{
			warnings.Add("outcome with missing key");
		}
		if (mod.Outcomes.Where((ModOutcomeDefinition outcome) => !string.IsNullOrWhiteSpace(outcome.Kind) && !string.IsNullOrWhiteSpace(outcome.Key)).GroupBy((ModOutcomeDefinition outcome) => outcome.Kind + ":" + outcome.Key, StringComparer.OrdinalIgnoreCase).Any((IGrouping<string, ModOutcomeDefinition> group) => group.Count() > 1))
		{
			warnings.Add("duplicate outcome keys");
		}
		return string.Join("; ", warnings);
	}

	private static bool IsKnownHookMode(string mode)
	{
		return string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "additive", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "exclusive", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "fallback", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "replace", StringComparison.OrdinalIgnoreCase);
	}

	private static T ReadJsonFile<T>(string path)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
		}
		catch (JsonException ex)
		{
			string location = "";
			if (ex.LineNumber.HasValue || ex.BytePositionInLine.HasValue)
			{
				location = " at line " + (ex.LineNumber ?? 0) + ", byte " + (ex.BytePositionInLine ?? 0);
			}
			throw new InvalidDataException(path + location + ": invalid JSON - " + ex.Message, ex);
		}
		catch (Exception ex)
		{
			throw new InvalidDataException(path + ": " + ex.Message, ex);
		}
	}

	private static void AppendGlobalWarnings(List<ModSummary> summaries, List<LoadedMod> loadedMods, List<LoadedMod> enabledMods)
	{
		AppendDuplicateWarnings(summaries, enabledMods.SelectMany((LoadedMod mod) => mod.Settings.Select((SettingDefinition setting) => new KeyValuePair<string, string>(setting.Key, mod.RootPath))), "setting");
		AppendDuplicateWarnings(summaries, enabledMods.SelectMany((LoadedMod mod) => mod.Tags.Select((ModTagDefinition tag) => new KeyValuePair<string, string>(string.IsNullOrWhiteSpace(tag.Key) ? tag.Label : tag.Key, mod.RootPath))), "tag");
		AppendDuplicateWarnings(summaries, enabledMods.SelectMany((LoadedMod mod) => mod.Contexts.Select((DerivedContextDefinition context) => new KeyValuePair<string, string>(context.Key, mod.RootPath))), "context");
		AppendDuplicateWarnings(summaries, enabledMods.SelectMany((LoadedMod mod) => mod.Outcomes.Where((ModOutcomeDefinition outcome) => !string.IsNullOrWhiteSpace(outcome.Kind) && !string.IsNullOrWhiteSpace(outcome.Key)).Select((ModOutcomeDefinition outcome) => new KeyValuePair<string, string>(outcome.Kind + ":" + outcome.Key, mod.RootPath))), "outcome");
		AppendMissingHookScriptWarnings(summaries, loadedMods);
		AppendExclusiveHookConflictWarnings(summaries, enabledMods);
	}

	private static void AppendMissingHookScriptWarnings(List<ModSummary> summaries, List<LoadedMod> loadedMods)
	{
		List<string> lineRoots = new List<string> { RuntimePaths.LinesDir };
		lineRoots.AddRange(loadedMods.Select((LoadedMod mod) => mod.LinesRootPath).Where(IsUsableLineRoot));
		foreach (LoadedMod mod in loadedMods)
		{
			foreach (ModHookDefinition hook in mod.Hooks.Where((ModHookDefinition hook) => !string.IsNullOrWhiteSpace(hook.Script)))
			{
				if (!ScriptExistsInLineRoots(hook.Script, lineRoots))
				{
					AppendWarningForRoot(summaries, mod.RootPath, "missing hook script: " + hook.Script);
				}
			}
		}
	}

	private static bool ScriptExistsInLineRoots(string scriptName, IEnumerable<string> lineRoots)
	{
		string cleanScriptName = CleanKey(scriptName);
		if (cleanScriptName.Length == 0)
		{
			return false;
		}
		foreach (string lineRoot in lineRoots.Where((string root) => !string.IsNullOrWhiteSpace(root)))
		{
			try
			{
				if (File.Exists(Path.Combine(lineRoot, "Scripts", "Base", cleanScriptName + ".txt")) || File.Exists(Path.Combine(lineRoot, "Scripts", "Extend", cleanScriptName + ".txt")))
				{
					return true;
				}
			}
			catch
			{
				return false;
			}
		}
		return false;
	}

	private static void AppendExclusiveHookConflictWarnings(List<ModSummary> summaries, List<LoadedMod> enabledMods)
	{
		var exclusiveHooks = enabledMods.SelectMany((LoadedMod mod) => mod.Hooks.Where((ModHookDefinition hook) => !string.IsNullOrWhiteSpace(hook.Hook) && (string.Equals(hook.Mode, "exclusive", StringComparison.OrdinalIgnoreCase) || string.Equals(hook.Mode, "replace", StringComparison.OrdinalIgnoreCase))).Select((ModHookDefinition hook) => new { Mod = mod, Hook = hook }));
		foreach (var conflictGroup in exclusiveHooks.GroupBy((item) => item.Hook.Hook, StringComparer.OrdinalIgnoreCase).Where((group) => group.Select((item) => item.Mod.RootPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
		{
			foreach (var item in conflictGroup)
			{
				AppendWarningForRoot(summaries, item.Mod.RootPath, "exclusive/replace hook conflict resolved by priority: " + conflictGroup.Key);
			}
		}
	}

	private static void AppendWarningForRoot(List<ModSummary> summaries, string rootPath, string warning)
	{
		foreach (ModSummary summary in summaries.Where((ModSummary summary) => string.Equals(summary.RootPath, rootPath, StringComparison.OrdinalIgnoreCase)))
		{
			AppendWarning(summary, warning);
		}
	}

	private static void AppendDuplicateWarnings(List<ModSummary> summaries, IEnumerable<KeyValuePair<string, string>> keys, string kind)
	{
		foreach (IGrouping<string, KeyValuePair<string, string>> duplicateGroup in keys.Where((KeyValuePair<string, string> pair) => !string.IsNullOrWhiteSpace(pair.Key)).GroupBy((KeyValuePair<string, string> pair) => pair.Key, StringComparer.OrdinalIgnoreCase).Where((IGrouping<string, KeyValuePair<string, string>> group) => group.Select((KeyValuePair<string, string> pair) => pair.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
		{
			HashSet<string> affectedRoots = duplicateGroup.Select((KeyValuePair<string, string> pair) => pair.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
			foreach (ModSummary summary in summaries.Where((ModSummary summary) => affectedRoots.Contains(summary.RootPath)))
			{
				AppendWarning(summary, "duplicate enabled " + kind + " key: " + duplicateGroup.Key);
			}
		}
	}

	private static void AppendWarning(ModSummary summary, string warning)
	{
		if (string.IsNullOrWhiteSpace(summary.Error))
		{
			summary.Error = warning;
		}
		else if (!summary.Error.Contains(warning, StringComparison.OrdinalIgnoreCase))
		{
			summary.Error += "; " + warning;
		}
	}

	private static bool IsUsableLineRoot(string path)
	{
		return Directory.Exists(Path.Combine(path, "Scripts")) && Directory.Exists(Path.Combine(path, "Vocab"));
	}
}
