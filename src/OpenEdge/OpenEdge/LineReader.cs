using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using OpenEdge.scripts;

namespace OpenEdge;

public partial class LineReader : Window, IComponentConnector
{
	private List<string> mods = new List<string>();

	private MainWindow mw;

	public TalkBaseClass talkBaseClass;

	private List<string> allViewedScripts = new List<string>();

	private string[] start = new string[0];

	private Random random = new Random();

	private string[] end = new string[0];

	private string[] seenStrings = new string[100];

	private int seenScriptPointer;

	private readonly LineSelectionBag lineSelectionBag = new LineSelectionBag();

	private string[] currentStrings;

	public LineReader(MainWindow mw)
	{
		InitializeComponent();
		this.mw = mw;
		talkBaseClass = new TalkBaseClass(mw);
		getMods();
	}

	public void readText(string s)
	{
		allViewedScripts.Add(s);
		try
		{
			File.WriteAllLines(mw.p.nameOfDebugFile, allViewedScripts);
		}
		catch
		{
			allViewedScripts.Add("DEBUG SCRIPT WAS BUSY, COULDN'T ADD CONTENT");
		}
	}

	public void getMods()
	{
		mods = GetCurrentLineRoots();
	}

	private List<string> GetCurrentLineRoots()
	{
		List<string> roots = new List<string>();
		if (!Directory.Exists(RuntimePaths.LinesDir))
		{
			Directory.CreateDirectory(RuntimePaths.LinesDir);
		}
		string[] directories = Directory.GetDirectories(RuntimePaths.LinesDir);
		for (int i = 0; i < directories.Length; i++)
		{
			if (Directory.Exists(Path.Combine(directories[i], "Scripts")) && Directory.Exists(Path.Combine(directories[i], "Vocab")))
			{
				roots.Add(directories[i]);
			}
		}
		foreach (string enabledLineRoot in ModService.GetEnabledLineRoots())
		{
			roots.Add(enabledLineRoot);
		}
		return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	public List<string> getVocabList(string vocabName)
	{
		string[][] allLines = getAllLines(vocabName, vocab: true);
		List<string> list = new List<string>();
		string[][] array = allLines;
		foreach (string[] array2 in array)
		{
			if (array2.Length == 0)
			{
				continue;
			}
			string[] array3 = array2;
			foreach (string text in array3)
			{
				if (text != "")
				{
					list.Add(talkBaseClass.CheckWords(text));
				}
			}
		}
		return list;
	}

	public string getVocab(string vocabName)
	{
		string[][] allLines = getAllLines(vocabName, vocab: true);
		List<string> list = new List<string>();
		if (allLines.Length < 1)
		{
			readText("vocab: " + vocabName + "   options:" + allLines.Length);
		}
		string[][] array = allLines;
		foreach (string[] array2 in array)
		{
			if (array2.Length == 0)
			{
				continue;
			}
			string[] array3 = array2;
			foreach (string text in array3)
			{
				if (text != "")
				{
					list.Add(talkBaseClass.CheckWords(text));
				}
			}
		}
		if (list.Count > 0)
		{
			string text2 = "";
			int index = lineSelectionBag.NextIndex("vocab:" + vocabName, list);
			string[] array3 = list[index].Split(" ");
			foreach (string text3 in array3)
			{
				string text4 = talkBaseClass.interpetText(text3.Trim(), list[index]);
				if (text4 == "flagFailed")
				{
					text2 = "";
					break;
				}
				text2 = text2 + text4 + " ";
			}
			seenStrings[seenScriptPointer % 100] = vocabName + index;
			seenScriptPointer++;
			return text2.Trim();
		}
		return vocabName;
	}

	public string[] getScript(string scriptName)
	{
		string[][] allLines = getAllLines(scriptName);
		readText("script: " + scriptName + "   options:" + allLines.Length);
		SessionTraceLogger.Info("script-select", scriptName + " options=" + allLines.Length);
		if (allLines.Length != 0)
		{
			int num = random.Next(0, allLines.Length);
			if (allLines.Length > 1)
			{
				while (Enumerable.Contains(seenStrings, scriptName + num))
				{
					num = random.Next(0, allLines.Length);
					seenStrings[seenScriptPointer % 100] = "";
					seenScriptPointer++;
				}
			}
			seenStrings[seenScriptPointer % 100] = scriptName + num;
			seenScriptPointer++;
			SessionTraceLogger.Info("script-select", scriptName + " picked=" + num + " lines=" + allLines[num].Length);
			return allLines[num];
		}
		return new string[0];
	}

	private string[][] getAllLines(string fileName, bool vocab = false)
	{
		string text = "Scripts";
		if (vocab)
		{
			text = "Vocab";
		}
		string[] collection = new string[0];
		List<string> list = new List<string>();
		foreach (string mod in GetCurrentLineRoots())
		{
			string basePath = Path.Combine(mod, text, "Base", fileName + ".txt");
			string extendPath = Path.Combine(mod, text, "Extend", fileName + ".txt");
			if (File.Exists(basePath))
			{
				collection = File.ReadAllLines(basePath);
			}
			if (File.Exists(extendPath))
			{
				list.AddRange(File.ReadAllLines(extendPath));
			}
		}
		list.AddRange(collection);
		string[] collection2 = new string[0];
		foreach (string item in list)
		{
			if (item.Contains("EXTENDS:"))
			{
				collection2 = canExtend(item, vocab);
			}
		}
		list.AddRange(collection2);
		return filterScripts(list.ToArray());
	}

	private string[] canExtend(string fileName, bool vocab = false)
	{
		fileName = fileName.Split("EXTENDS:")[1].Trim();
		string text = "Scripts";
		if (vocab)
		{
			text = "Vocab";
		}
		string[] collection = new string[0];
		List<string> list = new List<string>();
		foreach (string mod in GetCurrentLineRoots())
		{
			string basePath = Path.Combine(mod, text, "Base", fileName + ".txt");
			string extendPath = Path.Combine(mod, text, "Extend", fileName + ".txt");
			if (File.Exists(basePath))
			{
				collection = File.ReadAllLines(basePath);
			}
			if (File.Exists(extendPath))
			{
				list.AddRange(File.ReadAllLines(extendPath));
			}
		}
		list.AddRange(collection);
		string[] collection2 = new string[0];
		foreach (string item in list)
		{
			if (item.Contains("EXTENDS:"))
			{
				collection2 = canExtend(item, vocab);
			}
		}
		list.AddRange(collection2);
		return list.ToArray();
	}

	public string[][] filterScripts(string[] unScripts)
	{
		List<string[]> list = new List<string[]>();
		string[][] array = new string[0][];
		string[] array2 = new string[0];
		string text = "";
		for (int i = 0; i < unScripts.Length; i++)
		{
			if (unScripts[i].Replace("\t", "") != "" && !unScripts[i].Contains("//"))
			{
				text += unScripts[i].Replace("\t", "").Trim();
				if (i + 1 != unScripts.Length)
				{
					text += "\n";
				}
			}
		}
		if (!text.Contains("{") && !string.IsNullOrWhiteSpace(text))
		{
			return new string[1][] { text.Split('\n') };
		}
		array = getPart(text);
		array2 = getCondition(text);
		for (int j = 0; j < array.Length; j++)
		{
			bool flag = true;
			currentStrings = array[j];
			string[] array3 = array2[j].Split(" ");
			foreach (string requirement in array3)
			{
				if (!readRequirement(requirement, currentStrings))
				{
					flag = false;
				}
			}
			if (flag)
			{
				list.Add(currentStrings);
			}
		}
		for (int l = 0; l < list.Count; l++)
		{
			list[l] = start.Concat(list[l].Concat(end)).ToArray();
		}
		start = new string[0];
		end = new string[0];
		return list.ToArray();
	}

	public string[] getCondition(string s)
	{
		string[] array = new string[s.Split("{").Length - 1];
		for (int i = 0; i < s.Split("{").Length - 1; i++)
		{
			string[] array2 = s.Split("{");
			string[] array3 = array2[i].Split("}");
			if (array3.Length >= 2)
			{
				string text = array3[1].Replace("\n", " ");
				array[i] = text.Trim();
			}
			else
			{
				array[i] = array2[i].Trim();
			}
		}
		return array;
	}

	public string[][] getPart(string s)
	{
		string[][] array = new string[s.Split("{").Length - 1][];
		for (int i = 1; i < s.Split("{").Length; i++)
		{
			string[] array2 = s.Split("{")[i].Split("}")[0].Split("\n");
			array[i - 1] = array2;
		}
		return array;
	}

	public bool readRequirement(string requirement, string[] script)
	{
		requirement = requirement.Trim();
		string text = requirement;
		if (!(text == ""))
		{
			if (text != null)
			{
				if (text.StartsWith("START:"))
				{
					start = script;
					return false;
				}
				if (text.StartsWith("END:"))
				{
					end = script;
					return false;
				}
				if (text.StartsWith("EXTENDS:"))
				{
					return true;
				}
				string text2 = text;
				if (text2.StartsWith("ISFLAG:"))
				{
					return talkBaseClass.getFlag(text2);
				}
				string text3 = text;
				if (text3.StartsWith("ISFLAGT:"))
				{
					return talkBaseClass.getFlag(text3, temp: true);
				}
				string text4 = text;
				if (text4.StartsWith("ISNOFLAG:"))
				{
					return !talkBaseClass.getFlag(text4);
				}
				string text5 = text;
				if (text5.StartsWith("ISNOFLAGT:"))
				{
					return !talkBaseClass.getFlag(text5, temp: true);
				}
				if (ScriptSettingPredicateEvaluator.TryEvaluate(text, mw.isSettingEnabled, mw.isSettingAnswered, mw.isSettingDeclined, mw.getSettingValue, int.Parse, out bool settingPredicateResult))
				{
					return settingPredicateResult;
				}
				if (ScriptStructuredPredicateEvaluator.TryEvaluate(text, mw.isAnalStage, mw.isAnalPreference, mw.hasCompletedFirstAnalSession, mw.hasDeclinedAnalTraining, mw.hasActiveAnalTraining, mw.isPetPlayAdvancedDeclined, mw.isPetPlayAdvancedEnabled, mw.isOutsideSessionRuleActive, mw.isLobRuntimeEnabled, mw.hasSettingText, out bool structuredPredicateResult))
				{
					return structuredPredicateResult;
				}
				if (text.StartsWith("ISCONTEXT:"))
				{
					return DerivedContextService.IsContextActive(mw, text.Split(':')[1].Trim());
				}
				if (text.StartsWith("SETCONTEXTMEDIA:"))
				{
					string contextKey = text.Split(':')[1].Trim();
					if (!DerivedContextService.IsContextActive(mw, contextKey))
					{
						return false;
					}
					string mediaTags = DerivedContextService.GetContextMediaTags(contextKey);
					if (!string.IsNullOrWhiteSpace(mediaTags))
					{
						for (int i = 0; i < currentStrings.Length; i++)
						{
							if (currentStrings[i].Trim().Length > 0)
							{
								currentStrings[i] = "TEMPTAG:" + mediaTags + " " + currentStrings[i];
							}
						}
					}
					return true;
				}
				string text6 = text;
				if (text6.StartsWith("SETTAGGEDMEDIA:"))
				{
					string text7 = text6.Split(':')[1].Trim();
					if (mw.atLeastXMedia(text7, 2))
					{
						for (int i = 0; i < currentStrings.Length; i++)
						{
							if (currentStrings[i].Trim().Length > 0)
							{
								currentStrings[i] = "TEMPTAG:" + text7 + " " + currentStrings[i];
							}
						}
						return true;
					}
					return false;
				}
				string text8 = text;
				if (text8.StartsWith("TAG:"))
				{
					return mw.atLeastXMedia(text8.Split(':')[1].Trim());
				}
				string text9 = text;
				if (text9.StartsWith("IMGTAG:"))
				{
					return mw.atLeastXImages(text9.Split(':')[1].Trim());
				}
				string text10 = text;
				if (text10.StartsWith("VIDTAG:"))
				{
					return mw.atLeastXVideos(text10.Split(':')[1].Trim());
				}
				string text11 = text;
				if (text11.StartsWith("H") || text11.StartsWith("D") || text11.StartsWith("V"))
				{
					try
					{
						text11.Trim().StartsWith("V=0:state");
						double num = 0.0;
						if (text11.StartsWith("H"))
						{
							num = mw.getFlagTimeHours(text11.Split(":")[1].Trim());
						}
						if (text11.StartsWith("D"))
						{
							num = mw.getFlagTimeDays(text11.Split(":")[1].Trim());
						}
						if (text11.StartsWith("V"))
						{
							num = double.Parse(mw.getVar(text11.Split(":")[1].Trim()));
						}
						string text12 = text11.Split(":")[0];
						double num2 = double.Parse(text12.Substring(2));
						if (text12[1] == '>' && num > num2)
						{
							return true;
						}
						if (text12[1] == '<' && num < num2)
						{
							return true;
						}
						if (text12[1] == '=' && num == num2)
						{
							return true;
						}
						return false;
					}
					catch
					{
					}
				}
			}
			readText("A REQUIREMENT WAS UNRECOGNISED: " + requirement + "\nALLOWING THIS SCRIPT THROUGH:\n" + arrayToString(script));
			return true;
		}
		return true;
	}

	private string arrayToString(string[] strings)
	{
		string text = "";
		foreach (string text2 in strings)
		{
			text = text + text2 + "\n";
		}
		return text;
	}

	private string[] searchSubFolders(string currentDirectory)
	{
		List<string> list = new List<string>();
		string[] directories = Directory.GetDirectories(currentDirectory);
		for (int i = 0; i < directories.Length; i++)
		{
			list.AddRange(searchSubFolders(directories[i]));
		}
		list.AddRange(Directory.GetFiles(currentDirectory));
		return list.ToArray();
	}
}
