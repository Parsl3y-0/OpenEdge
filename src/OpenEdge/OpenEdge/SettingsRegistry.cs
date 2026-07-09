using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenEdge;

public enum SettingKind
{
	Toggle,
	Numeric,
	Text
}

public sealed class SettingDefinition
{
	public string Key { get; init; } = "";

	public string Label { get; init; } = "";

	public SettingKind Kind { get; init; }

	public string StructuredParentKey { get; init; } = "";

	public string LegacyEnabledFlag { get; init; } = "";

	public string LegacyDisabledFlag { get; init; } = "";

	public string LegacyValueKey { get; init; } = "";

	public string Description { get; init; } = "";

	public string ProgressionNote { get; init; } = "";

	public string Group { get; init; } = "";

	public IReadOnlyList<string> RelatedLegacyKeys { get; init; } = Array.Empty<string>();

	public bool QueueableAsk { get; init; }

	public IReadOnlyList<string> MediaDiscoveryTags { get; init; } = Array.Empty<string>();

	public int MediaDiscoveryMinimum { get; init; } = 2;

	public string SourceName { get; init; } = "";
}

public sealed class CuckSettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public int Stage { get; set; }

	public bool FridayPassed { get; set; }
}

public sealed class PetPlaySettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public bool AdvancedEnabled { get; set; }

	public bool AdvancedAnswered { get; set; }

	public bool CollarEnabled { get; set; }

	public bool TreatsEnabled { get; set; }

	public string Persona { get; set; } = "None";

	public string PetName { get; set; } = "";

	public string SubName { get; set; } = "";
}

public sealed class ChastitySettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public bool CageOwned { get; set; }

	public bool WearingCage { get; set; }

	public string CageType { get; set; } = "";

	public bool VibratorOwned { get; set; }

	public bool LostKey { get; set; }

	public bool ToldAboutNecklace { get; set; }

	public int DurationDays { get; set; }

	public string StartDateText { get; set; } = "";
}

public sealed class NoVideoSettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public int DurationDays { get; set; }
}

public sealed class AnalSettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public bool FirstSessionCompleted { get; set; }

	public string Experience { get; set; } = "Unknown";

	public string Preference { get; set; } = "Unknown";

	public bool TrainingEnabled { get; set; }

	public bool TrainingDeclined { get; set; }

	public bool WaterLubeEnabled { get; set; }

	public bool DildoEnabled { get; set; }

	public bool PlugEnabled { get; set; }
}

public sealed class LobSettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public bool RuntimeEnabled { get; set; }

	public int EarlyHour { get; set; }

	public int LateHour { get; set; }
}

public sealed class CensorshipSettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public int Intensity { get; set; }
}

public sealed class BreathPlaySettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public int BreathTimeSeconds { get; set; }
}

public sealed class GaySettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public bool HumiliationEnabled { get; set; }

	public bool HumiliationAnswered { get; set; }
}

public sealed class OutsideSessionSettingState
{
	public bool Enabled { get; set; }

	public bool Answered { get; set; }

	public int NoPornRemaining { get; set; }

	public int ConstantCeiRemaining { get; set; }

	public int PlugHourRemaining { get; set; }

	public int WatchPornRemaining { get; set; }

	public int HypnoFilesRemaining { get; set; }

	public bool HasActiveCommandments => NoPornRemaining > 0 || ConstantCeiRemaining > 0 || PlugHourRemaining > 0 || WatchPornRemaining > 0 || HypnoFilesRemaining > 0;
}

public sealed class SettingsRegistry
{
	private readonly CompatibilityStateService compatibilityStateService;

	private readonly Dictionary<string, SettingDefinition> definitionsByKey;

	private readonly Dictionary<string, SettingDefinition> definitionsByLegacyEnabledFlag;

	private readonly Dictionary<string, SettingDefinition> definitionsByLegacyDisabledFlag;

	private readonly IReadOnlyList<SettingDefinition> definitions;

	private readonly HashSet<string> queueableAskSettingKeys;

	private static readonly IReadOnlySet<string> QueueableAskSettingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"safeWord", "hands", "virgin", "gay", "feet", "taskScreen", "findom", "cei", "humiliation", "cuck", "string", "anal", "palming", "edgeIntro", "edgeHold", "cockControl", "asmr", "clothesPins", "hypno", "breathPlay", "outsideSession", "LOB", "canRemove", "petPlay", "petPlayAdvanced", "censorship"
	};

	private static readonly IReadOnlyList<SettingDefinition> Definitions = new List<SettingDefinition>
	{
		new SettingDefinition { Key = "humiliation", Label = "Humiliation", Kind = SettingKind.Toggle, LegacyEnabledFlag = "humiliation", LegacyDisabledFlag = "humiliationNo" },
		new SettingDefinition { Key = "sph", Label = "SPH", Kind = SettingKind.Toggle, LegacyEnabledFlag = "sph", LegacyDisabledFlag = "sphNo" },
		new SettingDefinition { Key = "virgin", Label = "Virgin", Kind = SettingKind.Toggle, LegacyEnabledFlag = "virgin", LegacyDisabledFlag = "virginNo" },
		new SettingDefinition { Key = "gay", Label = "Gay", Kind = SettingKind.Toggle, LegacyEnabledFlag = "gay", LegacyDisabledFlag = "gayNo", ProgressionNote = "Linked follow-up humiliation preference.", RelatedLegacyKeys = new string[1] { "gayHumiliation" } },
		new SettingDefinition { Key = "gayHumiliation", Label = "Gay Humiliation", Kind = SettingKind.Toggle, StructuredParentKey = "gay", LegacyEnabledFlag = "gayHumiliation", LegacyDisabledFlag = "gayHumiliationNo" },
		new SettingDefinition { Key = "feet", Label = "Feet", Kind = SettingKind.Toggle, LegacyEnabledFlag = "feet", LegacyDisabledFlag = "feetNo" },
		new SettingDefinition { Key = "sissy", Label = "Sissy", Kind = SettingKind.Toggle, LegacyEnabledFlag = "sissy", LegacyDisabledFlag = "sissyNo" },
		new SettingDefinition { Key = "petPlay", Label = "Pet Play", Kind = SettingKind.Toggle, LegacyEnabledFlag = "petPlay", LegacyDisabledFlag = "petPlayNo", ProgressionNote = "Has advanced stage, collar/treat readiness, persona selection, and pet identity text.", RelatedLegacyKeys = new string[8] { "petPlayAdvanced", "petPlayAdvancedNo", "collar", "treats", "pup", "cat", "petName", "subName" } },
		new SettingDefinition { Key = "chastity", Label = "Chastity", Kind = SettingKind.Toggle, LegacyEnabledFlag = "chastity", LegacyDisabledFlag = "chastityNo", ProgressionNote = "Tracks chastity preference, cage ownership/state, equipment, and related progression flags.", RelatedLegacyKeys = new string[8] { "cage", "wearingChastity", "cageType", "vibrator", "lostKey", "toldAboutNecklace", "chastityTime", "chastityDate" } },
		new SettingDefinition { Key = "cei", Label = "CEI", Kind = SettingKind.Toggle, LegacyEnabledFlag = "cei", LegacyDisabledFlag = "ceiNo" },
		new SettingDefinition { Key = "cuck", Label = "Cuck", Kind = SettingKind.Toggle, LegacyEnabledFlag = "cuck", LegacyDisabledFlag = "cuckNo", ProgressionNote = "Uses staged narrative progress beyond on/off.", RelatedLegacyKeys = new string[2] { "cuckNum", "fridayPassed" } },
		new SettingDefinition { Key = "findom", Label = "Findom", Kind = SettingKind.Toggle, LegacyEnabledFlag = "findom", LegacyDisabledFlag = "findomNo" },
		new SettingDefinition { Key = "censorship", Label = "Censorship", Kind = SettingKind.Toggle, LegacyEnabledFlag = "censorship", LegacyDisabledFlag = "censorshipNo", ProgressionNote = "Intensity is modified separately.", RelatedLegacyKeys = new string[1] { "censorIncrease" } },
		new SettingDefinition { Key = "asmr", Label = "ASMR", Kind = SettingKind.Toggle, LegacyEnabledFlag = "asmr", LegacyDisabledFlag = "asmrNo" },
		new SettingDefinition { Key = "hypno", Label = "Hypno", Kind = SettingKind.Toggle, LegacyEnabledFlag = "hypno", LegacyDisabledFlag = "hypnoNo" },
		new SettingDefinition { Key = "breathPlay", Label = "Breath Play", Kind = SettingKind.Toggle, LegacyEnabledFlag = "breathPlay", LegacyDisabledFlag = "breathPlayNo", ProgressionNote = "Has measured tolerance value.", RelatedLegacyKeys = new string[1] { "breathTime" } },
		new SettingDefinition { Key = "anal", Label = "Anal", Kind = SettingKind.Toggle, LegacyEnabledFlag = "anal", LegacyDisabledFlag = "analNo", ProgressionNote = "Tracks first session, training state, comfort preference, and related equipment readiness.", RelatedLegacyKeys = new string[9] { "analFirst", "analExperienced", "analBeginner", "analLike", "analNeutral", "analDislike", "analTraining", "analTrainingNo", "waterLube" } },
		new SettingDefinition { Key = "cockControl", Label = "Cock Control", Kind = SettingKind.Toggle, LegacyEnabledFlag = "cockControl", LegacyDisabledFlag = "cockControlNo" },
		new SettingDefinition { Key = "outsideSession", Label = "Outside Session", Kind = SettingKind.Toggle, LegacyEnabledFlag = "outsideSession", LegacyDisabledFlag = "outsideSessionNo", ProgressionNote = "Unlocks separate commandment/countdown rules.", RelatedLegacyKeys = new string[5] { "noPorn", "constantCei", "plugHour", "watchPorn", "hypnoFiles" } },
		new SettingDefinition { Key = "noVideo", Label = "No Video", Kind = SettingKind.Toggle, LegacyEnabledFlag = "noVideo", ProgressionNote = "Blocks videos and gifs until the no-video timer expires.", RelatedLegacyKeys = new string[1] { "noVideoValue" } },
		new SettingDefinition { Key = "taskScreen", Label = "Tasks", Kind = SettingKind.Toggle, LegacyEnabledFlag = "taskScreen", LegacyDisabledFlag = "taskScreenNo" },
		new SettingDefinition { Key = "devSessionTools", Label = "Dev Session Tools", Kind = SettingKind.Toggle, LegacyEnabledFlag = "devSessionTools", LegacyDisabledFlag = "devSessionToolsNo", ProgressionNote = "Shows an in-session developer panel for manually testing scripts, hooks, and session flow." },
		new SettingDefinition { Key = "LOB", Label = "LOB", Kind = SettingKind.Toggle, LegacyEnabledFlag = "LOB", LegacyDisabledFlag = "LOBNo", ProgressionNote = "Has activation schedule and on/off runtime companion state.", RelatedLegacyKeys = new string[3] { "LOBOn", "earlyLOB", "lateLOB" } },
		new SettingDefinition { Key = "canRemove", Label = "Can Remove", Kind = SettingKind.Toggle, LegacyEnabledFlag = "canRemove", LegacyDisabledFlag = "canRemoveNo" },
		new SettingDefinition { Key = "string", Label = "String", Kind = SettingKind.Toggle, LegacyEnabledFlag = "string", LegacyDisabledFlag = "stringNo" },
		new SettingDefinition { Key = "clothesPins", Label = "Clothes Pins", Kind = SettingKind.Toggle, LegacyEnabledFlag = "clothesPins", LegacyDisabledFlag = "clothesPinsNo" },
		new SettingDefinition { Key = "cage", Label = "Cage", Kind = SettingKind.Toggle, StructuredParentKey = "chastity", LegacyEnabledFlag = "cage", LegacyDisabledFlag = "cageNo" },
		new SettingDefinition { Key = "wearingChastity", Label = "Wearing Chastity", Kind = SettingKind.Toggle, StructuredParentKey = "chastity", LegacyEnabledFlag = "wearingChastity" },
		new SettingDefinition { Key = "vibrator", Label = "Vibrator", Kind = SettingKind.Toggle, StructuredParentKey = "chastity", LegacyEnabledFlag = "vibrator", LegacyDisabledFlag = "vibratorNo" },
		new SettingDefinition { Key = "lostKey", Label = "Lost Key", Kind = SettingKind.Toggle, StructuredParentKey = "chastity", LegacyEnabledFlag = "lostKey" },
		new SettingDefinition { Key = "toldAboutNecklace", Label = "Told About Necklace", Kind = SettingKind.Toggle, StructuredParentKey = "chastity", LegacyEnabledFlag = "toldAboutNecklace" },
		new SettingDefinition { Key = "plug", Label = "Plug", Kind = SettingKind.Toggle, StructuredParentKey = "anal", LegacyEnabledFlag = "plug", LegacyDisabledFlag = "plugNo" },
		new SettingDefinition { Key = "dildo", Label = "Dildo", Kind = SettingKind.Toggle, StructuredParentKey = "anal", LegacyEnabledFlag = "dildo", LegacyDisabledFlag = "dildoNo" },
		new SettingDefinition { Key = "waterLube", Label = "Water Lube", Kind = SettingKind.Toggle, StructuredParentKey = "anal", LegacyEnabledFlag = "waterLube", LegacyDisabledFlag = "waterLubeNo" },
		new SettingDefinition { Key = "collar", Label = "Collar", Kind = SettingKind.Toggle, StructuredParentKey = "petPlay", LegacyEnabledFlag = "collar", LegacyDisabledFlag = "collarNo" },
		new SettingDefinition { Key = "treats", Label = "Treats", Kind = SettingKind.Toggle, StructuredParentKey = "petPlay", LegacyEnabledFlag = "treats", LegacyDisabledFlag = "treatsNo" },
		new SettingDefinition { Key = "pup", Label = "Pup Persona", Kind = SettingKind.Toggle, StructuredParentKey = "petPlay", LegacyEnabledFlag = "pup", LegacyDisabledFlag = "pupNo" },
		new SettingDefinition { Key = "cat", Label = "Cat Persona", Kind = SettingKind.Toggle, StructuredParentKey = "petPlay", LegacyEnabledFlag = "cat", LegacyDisabledFlag = "catNo" },
		new SettingDefinition { Key = "goon", Label = "Goon", Kind = SettingKind.Toggle, LegacyEnabledFlag = "goon", LegacyDisabledFlag = "goonNo" },
		new SettingDefinition { Key = "strobe", Label = "Strobe", Kind = SettingKind.Toggle, LegacyEnabledFlag = "strobe", LegacyDisabledFlag = "strobeNo" },
		new SettingDefinition { Key = "gag", Label = "Gag", Kind = SettingKind.Toggle, LegacyEnabledFlag = "gag", LegacyDisabledFlag = "gagNo" },
		new SettingDefinition { Key = "hadBlowJob", Label = "Had Blowjob", Kind = SettingKind.Toggle, LegacyEnabledFlag = "hadBlowJob", LegacyDisabledFlag = "hadBlowJobNo", RelatedLegacyKeys = new string[1] { "hadBlowjob" } },
		new SettingDefinition { Key = "factualFirst", Label = "Factual First", Kind = SettingKind.Toggle, LegacyEnabledFlag = "factualFirst" },
		new SettingDefinition { Key = "explanationFactualFirst", Label = "Explanation Factual First", Kind = SettingKind.Toggle, LegacyEnabledFlag = "explanationFactualFirst" },
		new SettingDefinition { Key = "askedForName", Label = "Asked For Name", Kind = SettingKind.Toggle, LegacyEnabledFlag = "askedForName" },
		new SettingDefinition { Key = "safeWord", Label = "Safe Word", Kind = SettingKind.Toggle, LegacyEnabledFlag = "safeWord", LegacyDisabledFlag = "safeWordNo" },
		new SettingDefinition { Key = "edgeIntro", Label = "Edge Intro", Kind = SettingKind.Toggle, LegacyEnabledFlag = "edgeIntro", LegacyDisabledFlag = "edgeIntroNo" },
		new SettingDefinition { Key = "edgeHold", Label = "Edge Holds", Kind = SettingKind.Toggle, LegacyEnabledFlag = "edgeHold", LegacyDisabledFlag = "edgeHoldNo", ProgressionNote = "Allows scripts to use EDGEHOLD instructions. Disabled by default until explicitly accepted.", RelatedLegacyKeys = new string[3] { "edgeHoldRecord", "edgeHoldGame", "edgeHoldGameTime" } },
		new SettingDefinition { Key = "hands", Label = "Hands", Kind = SettingKind.Toggle, LegacyEnabledFlag = "hands", LegacyDisabledFlag = "handsNo" },
		new SettingDefinition { Key = "palming", Label = "Palming", Kind = SettingKind.Toggle, LegacyEnabledFlag = "palming", LegacyDisabledFlag = "palmingNo" },
		new SettingDefinition { Key = "removed", Label = "Removed", Kind = SettingKind.Toggle, LegacyEnabledFlag = "removed", LegacyDisabledFlag = "removedNo" },
		new SettingDefinition { Key = "chastityTime", Label = "Chastity Time", Kind = SettingKind.Numeric, StructuredParentKey = "chastity", LegacyValueKey = "chastityTime" },
		new SettingDefinition { Key = "breathTime", Label = "Breath Time", Kind = SettingKind.Numeric, StructuredParentKey = "breathPlay", LegacyValueKey = "breathTime" },
		new SettingDefinition { Key = "taskTime", Label = "Task Time", Kind = SettingKind.Numeric, LegacyValueKey = "taskTime" },
		new SettingDefinition { Key = "earlyLOB", Label = "Early LOB", Kind = SettingKind.Numeric, StructuredParentKey = "LOB", LegacyValueKey = "earlyLOB" },
		new SettingDefinition { Key = "lateLOB", Label = "Late LOB", Kind = SettingKind.Numeric, StructuredParentKey = "LOB", LegacyValueKey = "lateLOB" },
		new SettingDefinition { Key = "censorIncrease", Label = "Censor Increase", Kind = SettingKind.Numeric, StructuredParentKey = "censorship", LegacyValueKey = "censorIncrease" },
		new SettingDefinition { Key = "sessionLengthMod", Label = "Session Length Mod", Kind = SettingKind.Numeric, LegacyValueKey = "sessionLengthMod" },
		new SettingDefinition { Key = "videoMod", Label = "Video Mod", Kind = SettingKind.Numeric, LegacyValueKey = "videoMod" },
		new SettingDefinition { Key = "scoreMod", Label = "Score Mod", Kind = SettingKind.Numeric, LegacyValueKey = "scoreMod" },
		new SettingDefinition { Key = "pronoun", Label = "Pronouns", Kind = SettingKind.Numeric, LegacyValueKey = "pronoun", ProgressionNote = "0 = he/him, 1 = she/her, 2 = they/them." },
		new SettingDefinition { Key = "noPorn", Label = "No Porn", Kind = SettingKind.Numeric, StructuredParentKey = "outsideSession", LegacyValueKey = "noPorn" },
		new SettingDefinition { Key = "constantCei", Label = "Constant CEI", Kind = SettingKind.Numeric, StructuredParentKey = "outsideSession", LegacyValueKey = "constantCei" },
		new SettingDefinition { Key = "plugHour", Label = "Plug Hour", Kind = SettingKind.Numeric, StructuredParentKey = "outsideSession", LegacyValueKey = "plugHour" },
		new SettingDefinition { Key = "watchPorn", Label = "Watch Porn", Kind = SettingKind.Numeric, StructuredParentKey = "outsideSession", LegacyValueKey = "watchPorn" },
		new SettingDefinition { Key = "hypnoFiles", Label = "Hypno Files", Kind = SettingKind.Numeric, StructuredParentKey = "outsideSession", LegacyValueKey = "hypnoFiles" },
		new SettingDefinition { Key = "noVideoValue", Label = "No Video Value", Kind = SettingKind.Numeric, StructuredParentKey = "noVideo", LegacyValueKey = "noVideoValue" },
		new SettingDefinition { Key = "domTitle", Label = "Dom Title", Kind = SettingKind.Text, LegacyEnabledFlag = "domTitle", LegacyValueKey = "domTitle" },
		new SettingDefinition { Key = "cageType", Label = "Cage Type", Kind = SettingKind.Text, StructuredParentKey = "chastity", LegacyValueKey = "cageType" },
		new SettingDefinition { Key = "guessName", Label = "Guess Name", Kind = SettingKind.Text, LegacyValueKey = "guessName" },
		new SettingDefinition { Key = "chastityDate", Label = "Chastity Date", Kind = SettingKind.Text, StructuredParentKey = "chastity", LegacyValueKey = "chastityDate" },
		new SettingDefinition { Key = "petName", Label = "Pet Name", Kind = SettingKind.Text, StructuredParentKey = "petPlay", LegacyValueKey = "petName" },
		new SettingDefinition { Key = "subName", Label = "Sub Name", Kind = SettingKind.Text, StructuredParentKey = "petPlay", LegacyValueKey = "subName" }
	};

	public SettingsRegistry(CompatibilityStateService compatibilityStateService)
		: this(compatibilityStateService, Array.Empty<SettingDefinition>())
	{
	}

	public SettingsRegistry(CompatibilityStateService compatibilityStateService, IEnumerable<SettingDefinition> additionalDefinitions)
	{
		this.compatibilityStateService = compatibilityStateService;
		definitions = MergeDefinitions(additionalDefinitions);
		definitionsByKey = definitions.ToDictionary((SettingDefinition item) => item.Key, StringComparer.OrdinalIgnoreCase);
		definitionsByLegacyEnabledFlag = definitions.Where((SettingDefinition item) => !string.IsNullOrWhiteSpace(item.LegacyEnabledFlag)).GroupBy((SettingDefinition item) => item.LegacyEnabledFlag, StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, SettingDefinition> group) => group.Key, (IGrouping<string, SettingDefinition> group) => group.First(), StringComparer.OrdinalIgnoreCase);
		definitionsByLegacyDisabledFlag = definitions.Where((SettingDefinition item) => !string.IsNullOrWhiteSpace(item.LegacyDisabledFlag)).GroupBy((SettingDefinition item) => item.LegacyDisabledFlag, StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, SettingDefinition> group) => group.Key, (IGrouping<string, SettingDefinition> group) => group.First(), StringComparer.OrdinalIgnoreCase);
		queueableAskSettingKeys = new HashSet<string>(QueueableAskSettingKeys, StringComparer.OrdinalIgnoreCase);
		foreach (SettingDefinition definition in definitions.Where((SettingDefinition item) => item.QueueableAsk))
		{
			queueableAskSettingKeys.Add(definition.Key);
		}
		if (definitionsByKey.TryGetValue("hadBlowJob", out SettingDefinition value))
		{
			definitionsByLegacyEnabledFlag["hadBlowjob"] = value;
		}
	}

	private static IReadOnlyList<SettingDefinition> MergeDefinitions(IEnumerable<SettingDefinition> additionalDefinitions)
	{
		List<SettingDefinition> merged = new List<SettingDefinition>(Definitions);
		HashSet<string> usedKeys = new HashSet<string>(Definitions.Select((SettingDefinition item) => item.Key), StringComparer.OrdinalIgnoreCase);
		foreach (SettingDefinition definition in additionalDefinitions ?? Array.Empty<SettingDefinition>())
		{
			if (string.IsNullOrWhiteSpace(definition.Key) || usedKeys.Contains(definition.Key))
			{
				continue;
			}
			usedKeys.Add(definition.Key);
			merged.Add(definition);
		}
		return merged;
	}

	public IReadOnlyList<SettingDefinition> GetDefinitions()
	{
		return definitions;
	}

	public SettingDefinition GetDefinition(string key)
	{
		definitionsByKey.TryGetValue(key, out SettingDefinition value);
		return value;
	}

	public bool IsStructuredChildDefinition(string key)
	{
		SettingDefinition definition = GetDefinition(key);
		return definition != null && !string.IsNullOrWhiteSpace(definition.StructuredParentKey);
	}

	public bool SupportsQueuedAsk(string key)
	{
		return !string.IsNullOrWhiteSpace(key) && queueableAskSettingKeys.Contains(key);
	}

	public bool IsAskQueued(string key)
	{
		return GetQueuedAskKeys().Any((string queuedKey) => string.Equals(queuedKey, key, StringComparison.OrdinalIgnoreCase));
	}

	public IReadOnlyList<string> GetQueuedAskKeys()
	{
		string persistentValue = compatibilityStateService.GetPersistentValue(GetAskQueueKey());
		if (string.IsNullOrWhiteSpace(persistentValue))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>();
		foreach (string item in persistentValue.Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			string text = item.Trim();
			if (!SupportsQueuedAsk(text) || list.Any((string existingKey) => string.Equals(existingKey, text, StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}
			list.Add(text);
		}
		return list;
	}

	public void QueueAsk(string key)
	{
		if (!SupportsQueuedAsk(key) || IsAskAnswered(key))
		{
			return;
		}
		List<string> list = GetQueuedAskKeys().Where((string existingKey) => !string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase)).ToList();
		list.Add(key);
		SaveQueuedAskKeys(list);
	}

	public void DequeueAsk(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return;
		}
		SaveQueuedAskKeys(GetQueuedAskKeys().Where((string existingKey) => !string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase)).ToList());
	}

	public bool IsEnabled(string key)
	{
		SettingDefinition definition = RequireDefinition(key);
		string text = compatibilityStateService.GetPersistentValue(GetCanonicalEnabledKey(key));
		if (!string.IsNullOrWhiteSpace(text) && bool.TryParse(text, out bool result))
		{
			return result;
		}
		if (GetLegacyEnabledFlags(definition).Any((string flag) => compatibilityStateService.PersistentEntryExists(flag)))
		{
			return true;
		}
		if (GetLegacyDisabledFlags(definition).Any((string flag) => compatibilityStateService.PersistentEntryExists(flag)))
		{
			return false;
		}
		return false;
	}

	public bool IsAnswered(string key)
	{
		SettingDefinition definition = RequireDefinition(key);
		return IsDefinitionAnswered(definition);
	}

	public int GetNumericValue(string key, int defaultValue = 0)
	{
		string rawValue = GetRawValue(key);
		if (!string.IsNullOrWhiteSpace(rawValue) && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
		{
			return result;
		}
		return defaultValue;
	}

	public string GetRawValue(string key)
	{
		SettingDefinition definition = RequireDefinition(key);
		string text = compatibilityStateService.GetPersistentValue(GetCanonicalValueKey(key));
		if (text != null)
		{
			return text;
		}
		if (!string.IsNullOrWhiteSpace(definition.LegacyValueKey))
		{
			return compatibilityStateService.GetPersistentValue(definition.LegacyValueKey);
		}
		return null;
	}

	public string GetRelatedStateSummary(string key)
	{
		SettingDefinition definition = RequireDefinition(key);
		List<string> list = new List<string>();
		foreach (string relatedLegacyKey in definition.RelatedLegacyKeys)
		{
			string persistentValue = compatibilityStateService.GetPersistentValue(relatedLegacyKey);
			if (persistentValue == null)
			{
				continue;
			}
			if (string.IsNullOrWhiteSpace(persistentValue))
			{
				list.Add(relatedLegacyKey + "=<empty>");
			}
			else if (DateTime.TryParse(persistentValue, out _))
			{
				list.Add(relatedLegacyKey + "=set");
			}
			else
			{
				list.Add(relatedLegacyKey + "=" + persistentValue);
			}
		}
		return string.Join(", ", list);
	}

	public CuckSettingState GetCuckState()
	{
		return new CuckSettingState
		{
			Enabled = IsEnabled("cuck"),
			Answered = IsAnswered("cuck"),
			Stage = GetLegacyNumericValue("cuckNum"),
			FridayPassed = GetCuckFridayPassedAt().HasValue
		};
	}

	public DateTime? GetCuckFridayPassedAt()
	{
		string persistentValue = compatibilityStateService.GetPersistentValue("fridayPassed");
		if (!string.IsNullOrWhiteSpace(persistentValue) && DateTime.TryParse(persistentValue, out DateTime result))
		{
			return result;
		}
		return null;
	}

	public bool HasActiveCuckFridayCooldown(DateTime? now = null)
	{
		DateTime? cuckFridayPassedAt = GetCuckFridayPassedAt();
		if (!cuckFridayPassedAt.HasValue)
		{
			return false;
		}
		DateTime value = now ?? DateTime.Now;
		DateTime dateTime = cuckFridayPassedAt.Value.AddDays(1.0);
		while (dateTime.DayOfWeek != DayOfWeek.Saturday)
		{
			dateTime = dateTime.AddDays(1.0);
		}
		return value <= dateTime;
	}

	public bool ExpireStaleCuckFridayCooldown(DateTime? now = null)
	{
		if (HasActiveCuckFridayCooldown(now))
		{
			return false;
		}
		if (!GetCuckFridayPassedAt().HasValue)
		{
			return false;
		}
		compatibilityStateService.DeletePersistentValue("fridayPassed");
		return true;
	}

	public void SaveCuckState(CuckSettingState state)
	{
		SetEnabled("cuck", state.Enabled);
		compatibilityStateService.SetPersistentValue("cuckNum", state.Stage.ToString(CultureInfo.InvariantCulture));
		if (state.FridayPassed)
		{
			compatibilityStateService.SetPersistentValue("fridayPassed", DateTime.Now.ToString());
		}
		else
		{
			compatibilityStateService.DeletePersistentValue("fridayPassed");
		}
	}

	public PetPlaySettingState GetPetPlayState()
	{
		string text = "None";
		if (compatibilityStateService.PersistentEntryExists("pup"))
		{
			text = "Pup";
		}
		else if (compatibilityStateService.PersistentEntryExists("cat"))
		{
			text = "Cat";
		}
		return new PetPlaySettingState
		{
			Enabled = IsEnabled("petPlay"),
			Answered = IsAnswered("petPlay"),
			AdvancedEnabled = compatibilityStateService.PersistentEntryExists("petPlayAdvanced"),
			AdvancedAnswered = compatibilityStateService.PersistentEntryExists("petPlayAdvanced") || compatibilityStateService.PersistentEntryExists("petPlayAdvancedNo"),
			CollarEnabled = IsEnabled("collar"),
			TreatsEnabled = IsEnabled("treats"),
			Persona = text,
			PetName = GetRawValue("petName") ?? "",
			SubName = GetRawValue("subName") ?? ""
		};
	}

	public void SavePetPlayState(PetPlaySettingState state)
	{
		SetEnabled("petPlay", state.Enabled);
		if (state.AdvancedEnabled)
		{
			compatibilityStateService.SetPersistentValue("petPlayAdvanced", DateTime.Now.ToString());
			compatibilityStateService.DeletePersistentValue("petPlayAdvancedNo");
		}
		else if (state.AdvancedAnswered)
		{
			compatibilityStateService.DeletePersistentValue("petPlayAdvanced");
			compatibilityStateService.SetPersistentValue("petPlayAdvancedNo", DateTime.Now.ToString());
		}
		SetEnabled("collar", state.CollarEnabled);
		SetEnabled("treats", state.TreatsEnabled);
		ClearSetting("pup");
		ClearSetting("cat");
		if (string.Equals(state.Persona, "Pup", StringComparison.OrdinalIgnoreCase))
		{
			SetEnabled("pup", true);
		}
		else if (string.Equals(state.Persona, "Cat", StringComparison.OrdinalIgnoreCase))
		{
			SetEnabled("cat", true);
		}
		SetRawValue("petName", state.PetName);
		SetRawValue("subName", state.SubName);
	}

	public ChastitySettingState GetChastityState()
	{
		return new ChastitySettingState
		{
			Enabled = IsEnabled("chastity"),
			Answered = IsAnswered("chastity")
				|| IsAnswered("cage")
				|| IsAnswered("wearingChastity")
				|| IsAnswered("vibrator")
				|| IsAnswered("lostKey")
				|| IsAnswered("toldAboutNecklace")
				|| compatibilityStateService.PersistentEntryExists("cageType")
				|| compatibilityStateService.PersistentEntryExists("chastityTime")
				|| compatibilityStateService.PersistentEntryExists("chastityDate"),
			CageOwned = IsEnabled("cage"),
			WearingCage = IsEnabled("wearingChastity"),
			CageType = GetRawValue("cageType") ?? "",
			VibratorOwned = IsEnabled("vibrator"),
			LostKey = IsEnabled("lostKey"),
			ToldAboutNecklace = IsEnabled("toldAboutNecklace"),
			DurationDays = GetNumericValue("chastityTime"),
			StartDateText = GetRawValue("chastityDate") ?? ""
		};
	}

	public void SaveChastityState(ChastitySettingState state)
	{
		SetEnabled("chastity", state.Enabled);
		SetEnabled("cage", state.CageOwned);
		SetEnabled("wearingChastity", state.WearingCage);
		SetRawValue("cageType", state.CageType);
		SetEnabled("vibrator", state.VibratorOwned);
		SetEnabled("lostKey", state.LostKey);
		SetEnabled("toldAboutNecklace", state.ToldAboutNecklace);
		SetNumericValue("chastityTime", state.DurationDays);
		SetRawValue("chastityDate", state.StartDateText);
	}

	public NoVideoSettingState GetNoVideoState()
	{
		return new NoVideoSettingState
		{
			Enabled = IsEnabled("noVideo"),
			Answered = IsAnswered("noVideo") || compatibilityStateService.PersistentEntryExists("noVideoValue"),
			DurationDays = GetNumericValue("noVideoValue")
		};
	}

	public void SaveNoVideoState(NoVideoSettingState state)
	{
		SetEnabled("noVideo", state.Enabled);
		SetNumericValue("noVideoValue", state.DurationDays);
	}

	public AnalSettingState GetAnalState()
	{
		string text = "Unknown";
		if (compatibilityStateService.PersistentEntryExists("analExperienced"))
		{
			text = "Experienced";
		}
		else if (compatibilityStateService.PersistentEntryExists("analBeginner"))
		{
			text = "Beginner";
		}
		string text2 = "Unknown";
		if (compatibilityStateService.PersistentEntryExists("analLike"))
		{
			text2 = "Like";
		}
		else if (compatibilityStateService.PersistentEntryExists("analNeutral"))
		{
			text2 = "Neutral";
		}
		else if (compatibilityStateService.PersistentEntryExists("analDislike"))
		{
			text2 = "Dislike";
		}
		return new AnalSettingState
		{
			Enabled = IsEnabled("anal"),
			Answered = IsAnswered("anal"),
			FirstSessionCompleted = compatibilityStateService.PersistentEntryExists("analFirst"),
			Experience = text,
			Preference = text2,
			TrainingEnabled = compatibilityStateService.PersistentEntryExists("analTraining"),
			TrainingDeclined = compatibilityStateService.PersistentEntryExists("analTrainingNo"),
			WaterLubeEnabled = IsEnabled("waterLube"),
			DildoEnabled = IsEnabled("dildo"),
			PlugEnabled = IsEnabled("plug")
		};
	}

	public void SaveAnalState(AnalSettingState state)
	{
		SetEnabled("anal", state.Enabled);
		if (state.FirstSessionCompleted)
		{
			compatibilityStateService.SetPersistentValue("analFirst", DateTime.Now.ToString());
		}
		else
		{
			compatibilityStateService.DeletePersistentValue("analFirst");
		}
		compatibilityStateService.DeletePersistentValue("analExperienced");
		compatibilityStateService.DeletePersistentValue("analBeginner");
		if (string.Equals(state.Experience, "Experienced", StringComparison.OrdinalIgnoreCase))
		{
			compatibilityStateService.SetPersistentValue("analExperienced", DateTime.Now.ToString());
		}
		else if (string.Equals(state.Experience, "Beginner", StringComparison.OrdinalIgnoreCase))
		{
			compatibilityStateService.SetPersistentValue("analBeginner", DateTime.Now.ToString());
		}
		compatibilityStateService.DeletePersistentValue("analLike");
		compatibilityStateService.DeletePersistentValue("analNeutral");
		compatibilityStateService.DeletePersistentValue("analDislike");
		if (string.Equals(state.Preference, "Like", StringComparison.OrdinalIgnoreCase))
		{
			compatibilityStateService.SetPersistentValue("analLike", DateTime.Now.ToString());
		}
		else if (string.Equals(state.Preference, "Neutral", StringComparison.OrdinalIgnoreCase))
		{
			compatibilityStateService.SetPersistentValue("analNeutral", DateTime.Now.ToString());
		}
		else if (string.Equals(state.Preference, "Dislike", StringComparison.OrdinalIgnoreCase))
		{
			compatibilityStateService.SetPersistentValue("analDislike", DateTime.Now.ToString());
		}
		if (state.TrainingEnabled)
		{
			compatibilityStateService.SetPersistentValue("analTraining", DateTime.Now.ToString());
			compatibilityStateService.DeletePersistentValue("analTrainingNo");
		}
		else if (state.TrainingDeclined)
		{
			compatibilityStateService.DeletePersistentValue("analTraining");
			compatibilityStateService.SetPersistentValue("analTrainingNo", DateTime.Now.ToString());
		}
		else
		{
			compatibilityStateService.DeletePersistentValue("analTraining");
			compatibilityStateService.DeletePersistentValue("analTrainingNo");
		}
		SetEnabled("waterLube", state.WaterLubeEnabled);
		SetEnabled("dildo", state.DildoEnabled);
		SetEnabled("plug", state.PlugEnabled);
	}

	public bool HasCompletedFirstAnalSession()
	{
		return GetAnalState().FirstSessionCompleted;
	}

	public bool HasActiveAnalTraining()
	{
		return GetAnalState().TrainingEnabled;
	}

	public bool HasDeclinedAnalTraining()
	{
		return GetAnalState().TrainingDeclined;
	}

	public bool IsAnalStage(string stage)
	{
		return string.Equals(GetAnalState().Experience, stage, StringComparison.OrdinalIgnoreCase);
	}

	public bool IsAnalPreference(string preference)
	{
		return string.Equals(GetAnalState().Preference, preference, StringComparison.OrdinalIgnoreCase);
	}

	public LobSettingState GetLobState()
	{
		return new LobSettingState
		{
			Enabled = IsEnabled("LOB"),
			Answered = IsAnswered("LOB"),
			RuntimeEnabled = compatibilityStateService.PersistentEntryExists("LOBOn"),
			EarlyHour = GetLegacyNumericValue("earlyLOB"),
			LateHour = GetLegacyNumericValue("lateLOB")
		};
	}

	public OutsideSessionSettingState GetOutsideSessionState()
	{
		return new OutsideSessionSettingState
		{
			Enabled = IsEnabled("outsideSession"),
			Answered = IsAnswered("outsideSession"),
			NoPornRemaining = GetNumericValue("noPorn"),
			ConstantCeiRemaining = GetNumericValue("constantCei"),
			PlugHourRemaining = GetNumericValue("plugHour"),
			WatchPornRemaining = GetNumericValue("watchPorn"),
			HypnoFilesRemaining = GetNumericValue("hypnoFiles")
		};
	}

	public void SaveOutsideSessionState(OutsideSessionSettingState state)
	{
		SetEnabled("outsideSession", state.Enabled);
		SaveOutsideSessionCounter("noPorn", state.NoPornRemaining);
		SaveOutsideSessionCounter("constantCei", state.ConstantCeiRemaining);
		SaveOutsideSessionCounter("plugHour", state.PlugHourRemaining);
		SaveOutsideSessionCounter("watchPorn", state.WatchPornRemaining);
		SaveOutsideSessionCounter("hypnoFiles", state.HypnoFilesRemaining);
	}

	public void SaveOutsideSessionDraftState(OutsideSessionSettingState state)
	{
		SaveOutsideSessionDraftCounter("noPorn", state.NoPornRemaining);
		SaveOutsideSessionDraftCounter("constantCei", state.ConstantCeiRemaining);
		SaveOutsideSessionDraftCounter("plugHour", state.PlugHourRemaining);
		SaveOutsideSessionDraftCounter("watchPorn", state.WatchPornRemaining);
		SaveOutsideSessionDraftCounter("hypnoFiles", state.HypnoFilesRemaining);
	}

	public bool IsOutsideSessionRuleActive(string key)
	{
		return GetNumericValue(key) > 0;
	}

	public void SaveLobState(LobSettingState state)
	{
		SetEnabled("LOB", state.Enabled);
		if (state.RuntimeEnabled)
		{
			compatibilityStateService.SetPersistentValue("LOBOn", DateTime.Now.ToString());
		}
		else
		{
			compatibilityStateService.DeletePersistentValue("LOBOn");
		}
		SetNumericValue("earlyLOB", state.EarlyHour);
		SetNumericValue("lateLOB", state.LateHour);
	}

	public CensorshipSettingState GetCensorshipState()
	{
		return new CensorshipSettingState
		{
			Enabled = IsEnabled("censorship"),
			Answered = IsAnswered("censorship"),
			Intensity = GetNumericValue("censorIncrease")
		};
	}

	public void SaveCensorshipState(CensorshipSettingState state)
	{
		SetEnabled("censorship", state.Enabled);
		SetNumericValue("censorIncrease", state.Intensity);
	}

	public BreathPlaySettingState GetBreathPlayState()
	{
		return new BreathPlaySettingState
		{
			Enabled = IsEnabled("breathPlay"),
			Answered = IsAnswered("breathPlay"),
			BreathTimeSeconds = GetNumericValue("breathTime", 60)
		};
	}

	public void SaveBreathPlayState(BreathPlaySettingState state)
	{
		SetEnabled("breathPlay", state.Enabled);
		SetNumericValue("breathTime", state.BreathTimeSeconds);
	}

	public GaySettingState GetGayState()
	{
		return new GaySettingState
		{
			Enabled = IsEnabled("gay"),
			Answered = IsAnswered("gay"),
			HumiliationEnabled = IsEnabled("gayHumiliation"),
			HumiliationAnswered = IsAnswered("gayHumiliation")
		};
	}

	public void SaveGayState(GaySettingState state)
	{
		SetEnabled("gay", state.Enabled);
		if (state.HumiliationAnswered || state.HumiliationEnabled)
		{
			SetEnabled("gayHumiliation", state.HumiliationEnabled);
		}
		else
		{
			ClearSetting("gayHumiliation");
		}
	}

	public bool IsPetPlayAdvancedEnabled()
	{
		return GetPetPlayState().AdvancedEnabled;
	}

	public bool IsPetPlayAdvancedDeclined()
	{
		PetPlaySettingState petPlayState = GetPetPlayState();
		return petPlayState.AdvancedAnswered && !petPlayState.AdvancedEnabled;
	}

	public bool IsLobRuntimeEnabled()
	{
		return GetLobState().RuntimeEnabled;
	}

	public bool HasTextValue(string key)
	{
		return !string.IsNullOrWhiteSpace(GetRawValue(key));
	}

	public void SetEnabled(string key, bool enabled)
	{
		SettingDefinition definition = RequireDefinition(key);
		compatibilityStateService.SetPersistentValue(GetCanonicalEnabledKey(key), enabled.ToString());
		compatibilityStateService.SetPersistentValue(GetCanonicalAnsweredKey(key), true.ToString());
		foreach (string legacyEnabledFlag in GetLegacyEnabledFlags(definition))
		{
			if (enabled)
			{
				compatibilityStateService.SetPersistentValue(legacyEnabledFlag, DateTime.Now.ToString());
			}
			else
			{
				compatibilityStateService.DeletePersistentValue(legacyEnabledFlag);
			}
		}
		foreach (string legacyDisabledFlag in GetLegacyDisabledFlags(definition))
		{
			if (enabled)
			{
				compatibilityStateService.DeletePersistentValue(legacyDisabledFlag);
			}
			else
			{
				compatibilityStateService.SetPersistentValue(legacyDisabledFlag, DateTime.Now.ToString());
			}
		}
	}

	public void SetNumericValue(string key, int value)
	{
		SetRawValue(key, value.ToString(CultureInfo.InvariantCulture));
	}

	public void SetRawValue(string key, string value)
	{
		SettingDefinition definition = RequireDefinition(key);
		compatibilityStateService.SetPersistentValue(GetCanonicalValueKey(key), value ?? "");
		if (!string.IsNullOrWhiteSpace(definition.LegacyValueKey))
		{
			compatibilityStateService.SetPersistentValue(definition.LegacyValueKey, value ?? "");
		}
		compatibilityStateService.SetPersistentValue(GetCanonicalAnsweredKey(key), true.ToString());
	}

	public bool TryApplyLegacyFlagSet(string legacyFlagName)
	{
		if (definitionsByLegacyEnabledFlag.TryGetValue(legacyFlagName, out SettingDefinition value))
		{
			compatibilityStateService.SetPersistentValue(GetCanonicalEnabledKey(value.Key), true.ToString());
			compatibilityStateService.SetPersistentValue(GetCanonicalAnsweredKey(value.Key), true.ToString());
			return true;
		}
		if (definitionsByLegacyDisabledFlag.TryGetValue(legacyFlagName, out SettingDefinition value2))
		{
			compatibilityStateService.SetPersistentValue(GetCanonicalEnabledKey(value2.Key), false.ToString());
			compatibilityStateService.SetPersistentValue(GetCanonicalAnsweredKey(value2.Key), true.ToString());
			return true;
		}
		return false;
	}

	public bool TryApplyLegacyFlagDelete(string legacyFlagName)
	{
		if (definitionsByLegacyEnabledFlag.TryGetValue(legacyFlagName, out SettingDefinition value))
		{
			compatibilityStateService.DeletePersistentValue(GetCanonicalEnabledKey(value.Key));
			compatibilityStateService.DeletePersistentValue(GetCanonicalAnsweredKey(value.Key));
			if (string.Equals(value.LegacyValueKey, legacyFlagName, StringComparison.OrdinalIgnoreCase))
			{
				compatibilityStateService.DeletePersistentValue(GetCanonicalValueKey(value.Key));
			}
			return true;
		}
		if (definitionsByLegacyDisabledFlag.TryGetValue(legacyFlagName, out SettingDefinition value2))
		{
			compatibilityStateService.DeletePersistentValue(GetCanonicalEnabledKey(value2.Key));
			compatibilityStateService.DeletePersistentValue(GetCanonicalAnsweredKey(value2.Key));
			return true;
		}
		foreach (SettingDefinition definition in definitions)
		{
			if (string.Equals(definition.LegacyValueKey, legacyFlagName, StringComparison.OrdinalIgnoreCase))
			{
				compatibilityStateService.DeletePersistentValue(GetCanonicalValueKey(definition.Key));
				compatibilityStateService.DeletePersistentValue(GetCanonicalAnsweredKey(definition.Key));
				return true;
			}
		}
		return false;
	}

	public void ClearSetting(string key)
	{
		SettingDefinition definition = RequireDefinition(key);
		compatibilityStateService.DeletePersistentValue(GetCanonicalEnabledKey(key));
		compatibilityStateService.DeletePersistentValue(GetCanonicalAnsweredKey(key));
		compatibilityStateService.DeletePersistentValue(GetCanonicalValueKey(key));
		foreach (string legacyEnabledFlag in GetLegacyEnabledFlags(definition))
		{
			compatibilityStateService.DeletePersistentValue(legacyEnabledFlag);
		}
		foreach (string legacyDisabledFlag in GetLegacyDisabledFlags(definition))
		{
			compatibilityStateService.DeletePersistentValue(legacyDisabledFlag);
		}
		if (!string.IsNullOrWhiteSpace(definition.LegacyValueKey))
		{
			compatibilityStateService.DeletePersistentValue(definition.LegacyValueKey);
		}
	}

	public int ResetSessionRecoveryState()
	{
		int cleared = 0;
		compatibilityStateService.SetPersistentValue("failedSessionEnd", DateTime.Now.AddDays(-1).AddMinutes(-1).ToString());
		cleared++;
		foreach (string key in new string[1] { "leftEarly" })
		{
			if (compatibilityStateService.PersistentEntryExists(key))
			{
				compatibilityStateService.DeletePersistentValue(key);
				cleared++;
			}
		}
		foreach (string path in new string[3] { RuntimePaths.TempFlag("leftEarly"), RuntimePaths.TempFlag("leftEarlyToday"), RuntimePaths.TempFlag("sessionIntro") })
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
				cleared++;
			}
		}
		return cleared;
	}

	public void ResetAllSettingsState()
	{
		HashSet<string> legacyKeysToClear = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (SettingDefinition definition in definitions)
		{
			ClearSetting(definition.Key);
			foreach (string relatedLegacyKey in definition.RelatedLegacyKeys)
			{
				legacyKeysToClear.Add(relatedLegacyKey);
			}
		}
		foreach (string queueableAskSettingKey in queueableAskSettingKeys)
		{
			compatibilityStateService.DeletePersistentValue(GetCanonicalEnabledKey(queueableAskSettingKey));
			compatibilityStateService.DeletePersistentValue(GetCanonicalAnsweredKey(queueableAskSettingKey));
			compatibilityStateService.DeletePersistentValue(GetCanonicalValueKey(queueableAskSettingKey));
		}
		legacyKeysToClear.Add("petPlayAdvanced");
		legacyKeysToClear.Add("petPlayAdvancedNo");
		legacyKeysToClear.Add("analLike");
		legacyKeysToClear.Add("analNeutral");
		legacyKeysToClear.Add("analDislike");
		legacyKeysToClear.Add("analTraining");
		legacyKeysToClear.Add("analTrainingNo");
		foreach (string legacyKey in legacyKeysToClear)
		{
			compatibilityStateService.DeletePersistentValue(legacyKey);
		}
		compatibilityStateService.DeletePersistentValue(GetAskQueueKey());
	}

	private bool IsAskAnswered(string key)
	{
		if (definitionsByKey.TryGetValue(key, out SettingDefinition definition))
		{
			return IsDefinitionAnswered(definition);
		}
		if (compatibilityStateService.PersistentEntryExists(key))
		{
			return true;
		}
		return compatibilityStateService.PersistentEntryExists(key + "No");
	}

	private bool IsDefinitionAnswered(SettingDefinition definition)
	{
		string text = compatibilityStateService.GetPersistentValue(GetCanonicalAnsweredKey(definition.Key));
		if (!string.IsNullOrWhiteSpace(text) && bool.TryParse(text, out bool result))
		{
			return result;
		}
		if (GetLegacyEnabledFlags(definition).Any((string flag) => compatibilityStateService.PersistentEntryExists(flag)))
		{
			return true;
		}
		if (GetLegacyDisabledFlags(definition).Any((string flag) => compatibilityStateService.PersistentEntryExists(flag)))
		{
			return true;
		}
		return false;
	}

	private SettingDefinition RequireDefinition(string key)
	{
		if (!definitionsByKey.TryGetValue(key, out SettingDefinition value))
		{
			throw new InvalidOperationException("Unknown setting key: " + key);
		}
		return value;
	}

	private static string GetCanonicalEnabledKey(string key)
	{
		return "settings." + key + ".enabled";
	}

	private static string GetCanonicalAnsweredKey(string key)
	{
		return "settings." + key + ".answered";
	}

	private static string GetCanonicalValueKey(string key)
	{
		return "settings." + key + ".value";
	}

	private static IReadOnlyList<string> GetLegacyEnabledFlags(SettingDefinition definition)
	{
		if (string.Equals(definition.Key, "hadBlowJob", StringComparison.OrdinalIgnoreCase))
		{
			return new string[2] { "hadBlowJob", "hadBlowjob" };
		}
		if (string.IsNullOrWhiteSpace(definition.LegacyEnabledFlag))
		{
			return Array.Empty<string>();
		}
		return new string[1] { definition.LegacyEnabledFlag };
	}

	private static IReadOnlyList<string> GetLegacyDisabledFlags(SettingDefinition definition)
	{
		if (string.IsNullOrWhiteSpace(definition.LegacyDisabledFlag))
		{
			return Array.Empty<string>();
		}
		return new string[1] { definition.LegacyDisabledFlag };
	}

	private static string GetAskQueueKey()
	{
		return "settings.askQueue";
	}

	private void SaveQueuedAskKeys(IReadOnlyCollection<string> keys)
	{
		if (keys.Count == 0)
		{
			compatibilityStateService.DeletePersistentValue(GetAskQueueKey());
			return;
		}
		compatibilityStateService.SetPersistentValue(GetAskQueueKey(), string.Join("|", keys));
	}

	private int GetLegacyNumericValue(string key, int defaultValue = 0)
	{
		string persistentValue = compatibilityStateService.GetPersistentValue(key);
		if (!string.IsNullOrWhiteSpace(persistentValue) && int.TryParse(persistentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
		{
			return result;
		}
		return defaultValue;
	}

	private void SaveOutsideSessionCounter(string key, int value)
	{
		if (value > 0)
		{
			SetNumericValue(key, value);
		}
		else
		{
			ClearSetting(key);
		}
	}

	private void SaveOutsideSessionDraftCounter(string key, int value)
	{
		SettingDefinition definition = RequireDefinition(key);
		compatibilityStateService.DeletePersistentValue(GetCanonicalAnsweredKey(key));
		if (value > 0)
		{
			compatibilityStateService.SetPersistentValue(GetCanonicalValueKey(key), value.ToString(CultureInfo.InvariantCulture));
		}
		else
		{
			compatibilityStateService.DeletePersistentValue(GetCanonicalValueKey(key));
		}
		if (!string.IsNullOrWhiteSpace(definition.LegacyValueKey))
		{
			compatibilityStateService.DeletePersistentValue(definition.LegacyValueKey);
		}
	}
}
