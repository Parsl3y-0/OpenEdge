using System;
using System.IO;

namespace OpenEdge;

public static class RuntimePaths
{
	public static string RuntimeRoot => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

	public static string AudioDir => Path.Combine(RuntimeRoot, "audio");

	public static string DebugDir => Path.Combine(RuntimeRoot, "debug");

	public static string FlagsDir => Path.Combine(RuntimeRoot, "flags");

	public static string TempFlagsDir => Path.Combine(FlagsDir, "temp");

	public static string ImagesDir => Path.Combine(RuntimeRoot, "images");

	public static string VideosDir => Path.Combine(RuntimeRoot, "videos");

	public static string ResourcesDir => Path.Combine(RuntimeRoot, "resources");

	public static string LinesDir => Path.Combine(RuntimeRoot, "lines");

	public static string ModsDir => Path.Combine(RuntimeRoot, "mods");

	public static string ModLoadOrderFile => Path.Combine(RuntimeRoot, "mod-load-order.json");

	public static string ContextsDir => Path.Combine(RuntimeRoot, "contexts");

	public static string ContextsFile => Path.Combine(ContextsDir, "contexts.json");

	public static string OptionsFile => Path.Combine(RuntimeRoot, "options.txt");

	public static string TasksFile => Path.Combine(RuntimeRoot, "tasks.txt");

	public static string TagsFile => Path.Combine(RuntimeRoot, "tags.txt");

	public static string TagGroupsFile => Path.Combine(RuntimeRoot, "tagGroups.txt");

	public static string CompatibilityStateFile => Path.Combine(RuntimeRoot, "compatibility-state.json");

	public static string CompatibilityBackupsDir => Path.Combine(RuntimeRoot, "compatibility-backups");

	public static string CompatibilityTransfersDir => Path.Combine(RuntimeRoot, "compatibility-transfers");

	public static string ICAddressFile => Path.Combine(RuntimeRoot, "ICAddress.txt");

	public static string ICPathFile => Path.Combine(RuntimeRoot, "ICPath.txt");

	public static string BtDevicesFile => Path.Combine(RuntimeRoot, "btDevices.txt");

	public static string Resource(string fileName) => Path.Combine(ResourcesDir, fileName);

	public static string ToRuntimeRelativePath(string fullPath)
	{
		string relativePath = Path.GetRelativePath(RuntimeRoot, fullPath);
		return Path.DirectorySeparatorChar + relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
	}

	public static string Flag(string name) => Path.Combine(FlagsDir, name + ".txt");

	public static string TempFlag(string name) => Path.Combine(TempFlagsDir, name + ".txt");

	public static string ResolveRuntimePath(string relativePath)
	{
		string trimmed = relativePath.TrimStart('\\', '/');
		return Path.Combine(RuntimeRoot, trimmed.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
	}
}
