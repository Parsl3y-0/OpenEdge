namespace OpenEdge.scripts;

internal class GenericScript : TalkBaseClass
{
	public string ScriptName { get; }

	public GenericScript(MainWindow mw, TalkBaseClass homeTalk, string scriptName, string hookName = "")
		: base(mw)
	{
		ScriptName = scriptName;
		ModHookName = hookName ?? "";
		this.homeTalk = homeTalk;
		allText = mw.lr.getScript(scriptName);
	}
}
