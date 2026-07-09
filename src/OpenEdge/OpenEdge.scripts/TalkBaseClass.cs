using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenEdge.helper;

namespace OpenEdge.scripts;

public class TalkBaseClass
{
	protected int pickedScript = -1;

	protected MainWindow mw;

	public int location;

	public string tags = "";

	private Voc voc;

	public TalkBaseClass homeTalk;

	public string currentState = "";

	protected string ModHookName = "";

	private Random random = new Random();

	public bool talkLocked;

	public bool repeating;

	private bool hasContent;

	public string[] allText = new string[1] { "" };

	public TalkBaseClass(MainWindow mw)
	{
		this.mw = mw;
		voc = new Voc(mw);
	}

	public string Talk()
	{
		if (allText.Length > location)
		{
			hasContent = false;
			string sentence = allText[location];
			SessionTraceLogger.Info("script-line", GetType().Name + "[" + location + "] raw=" + sentence);
			sentence = CheckWords(sentence);
			sentence = sentence.Trim();
			string text = "";
			string[] array = sentence.Split(" ");
			foreach (string text2 in array)
			{
				string text3 = interpetText(text2.Trim(), sentence, derivative: false);
				if (text3 == "flagFailed")
				{
					text = "";
					break;
				}
				text = text + text3 + " ";
			}
			sentence = text.Trim();
			sentence = sentence.Replace("  ", " ");
			if (!repeating)
			{
				location++;
			}
			bool outputCameFromThisLine = true;
			if (!hasContent && sentence == "")
			{
				outputCameFromThisLine = false;
				sentence = mw.currentScript.Talk();
			}
			if (sentence == "EMPTY")
			{
				sentence = "";
			}
			if (outputCameFromThisLine && !string.IsNullOrWhiteSpace(sentence))
			{
				SessionTraceLogger.Info("script-output", GetType().Name + "[" + (location - 1) + "] text=" + sentence);
			}
			return sentence;
		}
		if (homeTalk != null)
		{
			if (!string.IsNullOrWhiteSpace(ModHookName))
			{
				mw.CompleteModHook(ModHookName);
				ModHookName = "";
			}
			homeTalk.talkLocked = false;
			mw.currentScript = homeTalk;
			if (currentState != "")
			{
				mw.currentState = currentState;
			}
			return mw.currentScript.Talk();
		}
		mw.pickScript();
		return "";
	}

	protected int getMyScript(int rep = 0)
	{
		return 0;
	}

	public string interpetText(string sentence, string fullSentence, bool derivative = true)
	{
		string text = sentence;
		if (text == null)
		{
			goto IL_09cb;
		}
		if (text.StartsWith("##"))
		{
			text = fullSentence.Split("##")[1];
			string text2 = "";
			string[] array = text.Split(" ");
			foreach (string sentence2 in array)
			{
				text2 = text2 + interpetText(sentence2, text) + " ";
			}
			mw.hypnosisAnswer(text2.Trim());
			return "";
		}
		if (fullSentence.Contains("##"))
		{
			return "";
		}
		string text3 = text;
		if (text3.StartsWith("ASK:"))
		{
			sentence = ask(text3);
		}
		else
		{
			string text4 = text;
			if (text4.StartsWith("COMMAND:"))
			{
				sentence = command(text4);
			}
			else
			{
				string text5 = text;
				if (text5.StartsWith("CHANCE:"))
				{
					if (!chance(text5))
					{
						return "flagFailed";
					}
					return "";
				}
				if (fullSentence.Contains("[") && fullSentence.Contains("]"))
				{
					return "";
				}
				if (text.StartsWith("SINGLE:"))
				{
					return text.Replace("SINGLE:", "").Trim();
				}
				if (text == "STROBEON:")
				{
					setFlag("FLAG:strobe");
					return "";
				}
				if (text == "STROBEOFF:")
				{
					deleteFlag("DELFLAG:strobe");
					return "";
				}
				if (text == "HYPNOSIS:")
				{
					return "";
				}
				string text6 = text;
				if (text6.StartsWith("GOTO:"))
				{
					goToGOTO(text6.Split("GOTO:")[^1]);
					return "";
				}
				string text7 = text;
				if (text7.StartsWith("(") && text7.EndsWith(")"))
				{
					return "";
				}
				string text8 = text;
				if (text8.StartsWith("FLAG:"))
				{
					setFlag(text8);
					return "";
				}
				string text9 = text;
				if (text9.StartsWith("DELFLAG:"))
				{
					deleteFlag(text9);
					return "";
				}
				string text10 = text;
				if (text10.StartsWith("ISFLAG:"))
				{
					if (!getFlag(text10))
					{
						return "flagFailed";
					}
					return "";
				}
				if (ScriptSettingPredicateEvaluator.TryEvaluate(text, mw.isSettingEnabled, mw.isSettingAnswered, mw.isSettingDeclined, mw.getSettingValue, delegate(string value)
				{
					return int.Parse(interpetText(value, value));
				}, out bool settingPredicateResult))
				{
					return settingPredicateResult ? "" : "flagFailed";
				}
				if (text.StartsWith("ISCONTEXT:"))
				{
					if (!DerivedContextService.IsContextActive(mw, text.Replace("ISCONTEXT:", "").Trim()))
					{
						return "flagFailed";
					}
					return "";
				}
				if (text.StartsWith("SETCONTEXTMEDIA:"))
				{
					string contextKey = text.Replace("SETCONTEXTMEDIA:", "").Trim();
					if (!DerivedContextService.IsContextActive(mw, contextKey))
					{
						return "flagFailed";
					}
					string mediaTags = DerivedContextService.GetContextMediaTags(contextKey);
					if (!string.IsNullOrWhiteSpace(mediaTags))
					{
						placeTag("TEMPTAG:" + mediaTags, isTemp: true);
					}
					return "";
				}
				if (ScriptStructuredPredicateEvaluator.TryEvaluate(text, mw.isAnalStage, mw.isAnalPreference, mw.hasCompletedFirstAnalSession, mw.hasDeclinedAnalTraining, mw.hasActiveAnalTraining, mw.isPetPlayAdvancedDeclined, mw.isPetPlayAdvancedEnabled, mw.isOutsideSessionRuleActive, mw.isLobRuntimeEnabled, mw.hasSettingText, out bool structuredPredicateResult))
				{
					return structuredPredicateResult ? "" : "flagFailed";
				}
				string text11 = text;
				if (text11.StartsWith("ISNOFLAG:"))
				{
					if (getFlag(text11))
					{
						return "flagFailed";
					}
					return "";
				}
				string text12 = text;
				if (text12.StartsWith("LENGTH:"))
				{
					length(text12);
					return "";
				}
				string text13 = text;
				if (text13.StartsWith("FLAGT:"))
				{
					setFlag(text13, temp: true);
					return "";
				}
				string text14 = text;
				if (text14.StartsWith("DELFLAGT:"))
				{
					deleteFlag(text14, temp: true);
					return "";
				}
				string text15 = text;
				if (text15.StartsWith("ISFLAGT:"))
				{
					if (!getFlag(text15, temp: true))
					{
						return "flagFailed";
					}
					return "";
				}
				string text16 = text;
				if (text16.StartsWith("ISNOFLAGT:"))
				{
					if (getFlag(text16, temp: true))
					{
						return "flagFailed";
					}
					return "";
				}
				string text17 = text;
				if (text17.StartsWith("INCREMENT:"))
				{
					incrementFlag(text17);
					return "";
				}
				string text18 = text;
				if (text18.StartsWith("DECREMENT:"))
				{
					decrementFlag(text18);
					return "";
				}
				string text19 = text;
				if (text19.StartsWith("SETVAR:"))
				{
					setVar(text19);
					return "";
				}
				string text20 = text;
				if (text20.StartsWith("ADDVAR:"))
				{
					addVar(text20);
					return "";
				}
				ScriptSettingCommandExecutor settingCommandExecutor = new ScriptSettingCommandExecutor(mw.setSettingEnabled, mw.setSettingValue, mw.setSettingText, mw.setAnalStage, mw.setAnalPreference, mw.setAnalTraining, mw.setPetPersona, mw.setOutsideRule, mw.setLobWindow, delegate(string value)
				{
					return interpetText(value, value);
				}, mw.setCuckStage, mw.setCuckFriday, mw.setChastityState, mw.setCensorIntensity, mw.setBreathTime);
				if (settingCommandExecutor.TryExecute(text))
				{
					return "";
				}
				string text21 = text;
				if (text21.StartsWith("TEXTFIELD:"))
				{
					textField(text21);
					return "";
				}
				string text22 = text;
				if (text22.StartsWith("TRIBUTE:"))
				{
					tribute(text22);
					return "";
				}
				string text23 = text;
				if (text23.StartsWith("TASK:"))
				{
					task(text23);
					return "";
				}
				string text24 = text;
				if (text24.StartsWith("ISVAR:"))
				{
					if (!isVar(text24))
					{
						return "flagFailed";
					}
					return "";
				}
				string text25 = text;
				if (text25.StartsWith("GETVAR:"))
				{
					string text26 = "";
					if (text25.EndsWith(","))
					{
						text25 = text25.Replace(',', ' ');
						text26 = ",";
					}
					if (text25.EndsWith("."))
					{
						text25 = text25.Replace('.', ' ');
						text26 = ".";
					}
					if (text25.EndsWith("?"))
					{
						text25 = text25.Replace('?', ' ');
						text26 = "?";
					}
					if (text25.EndsWith("!"))
					{
						text25 = text25.Replace('!', ' ');
						text26 = "!";
					}
					if (text25.EndsWith("*"))
					{
						text25 = text25.Replace('*', ' ');
						text26 = "*";
					}
					text25 = text25.Trim();
					return getVar(text25) + text26;
				}
				string text27 = text;
				if (text27.StartsWith("GETTIME:"))
				{
					return getTime(text27);
				}
				string text28 = text;
				if (text28.StartsWith("BIGGERTHAN:"))
				{
					if (!biggerThan(text28))
					{
						return "flagFailed";
					}
					return "";
				}
				if (text.StartsWith("ENDSESSION"))
				{
					endSession();
					return "";
				}
				string text29 = text;
				if (text29.StartsWith("SESSIONTIMERELATIVE:"))
				{
					sessionTimeRelative(text29);
					return "";
				}
				string text30 = text;
				if (text30.StartsWith("SESSIONTIME:"))
				{
					sessionTime(text30);
					return "";
				}
				if (text.StartsWith("REPEAT"))
				{
					repeat();
					return "";
				}
				if (text.StartsWith("KNEELNO"))
				{
					kneel(kneeling: false);
					sentence = "";
				}
				else if (text.StartsWith("KNEEL"))
				{
					kneel(kneeling: true);
					sentence = "";
				}
				else if (text.StartsWith("FOURSNO"))
				{
					fours(fours: false);
					sentence = "";
				}
				else if (text.StartsWith("FOURS"))
				{
					fours(fours: true);
					sentence = "";
				}
				else if (text.StartsWith("CENSORON"))
				{
					mw.censorCheck(forceCensor: true);
					sentence = "";
				}
				else if (text.StartsWith("PLUGASSNO"))
				{
					plug(plug: false);
					sentence = "";
				}
				else if (text.StartsWith("PLUGASS"))
				{
					plug(plug: true);
					sentence = "";
				}
				else if (text.StartsWith("ONANO"))
				{
					ona(ona: false);
					sentence = "";
				}
				else if (text.StartsWith("ONA"))
				{
					ona(ona: true);
					sentence = "";
				}
				else if (text.StartsWith("BINDBALLSNO"))
				{
					ballsBound(ballsBound: false);
					sentence = "";
				}
				else if (text.StartsWith("SHOWFAVOR:"))
				{
					showFavor();
					sentence = "";
				}
				else if (text.StartsWith("HIDEFAVOR:"))
				{
					hideFavor();
					sentence = "";
				}
				else if (text.StartsWith("BINDBALLS"))
				{
					ballsBound(ballsBound: true);
					sentence = "";
				}
				else if (text.StartsWith("COLLARNO"))
				{
					collared(collared: false);
					sentence = "";
				}
				else if (text.StartsWith("COLLAR"))
				{
					collared(collared: true);
					sentence = "";
				}
				else if (text.StartsWith("GAGNO"))
				{
					gagged(gagged: false);
					sentence = "";
				}
				else if (text.StartsWith("GAG"))
				{
					gagged(gagged: true);
					sentence = "";
				}
				else if (text.StartsWith("CLAMPNO"))
				{
					clamp(clamp: false);
					sentence = "";
				}
				else
				{
					if (!text.StartsWith("CLAMP"))
					{
						string text31 = text;
						if (!text31.StartsWith("MOOD:"))
						{
							if (!(text == "SNAP:"))
							{
								if (text == "SNAPDOUBLE:")
								{
									mw.playDoubleSnapAudio();
									return "";
								}
								string text32 = text;
								if (text32.StartsWith("TEMPTAG:"))
								{
									placeTag(text32, isTemp: true);
									return "";
								}
								string text33 = text;
								if (text33.StartsWith("TAG:"))
								{
									placeTag(text33);
									return "";
								}
								if (text.StartsWith("EXTRAASK:"))
								{
									mw.TryStartExtraSettingAsk();
									return "";
								}
								if (text.StartsWith("LOB:"))
								{
									mw.setLOB();
									return "";
								}
								if (text.StartsWith("LOBNO:"))
								{
									mw.setLOB(chkStartUp: false);
									return "";
								}
								if (text.StartsWith("DELIMG:"))
								{
									mw.secWindow.removeCurrentImage();
									return "";
								}
								if (text.StartsWith("LOCKIMG:"))
								{
									mw.secWindow.setImgLocked(isLocked: true);
									return "";
								}
								if (text.StartsWith("UNLOCKIMG:"))
								{
									mw.secWindow.setImgLocked(isLocked: false);
									return "";
								}
								if (text.StartsWith("DELVIDEO:"))
								{
									mw.secWindow.removeCurrentVideo();
									return "";
								}
								if (text.StartsWith("LOCKVIDEO:"))
								{
									mw.secWindow.setVideoLocked(isLocked: true);
									return "";
								}
								if (text.StartsWith("UNLOCKVIDEO:"))
								{
									mw.secWindow.setVideoLocked(isLocked: false);
									return "";
								}
								string text34 = text;
								if (text34.StartsWith("TIME:"))
								{
									text34 = text34.Replace("TIME:", "");
									text34 = text34.Trim();
									text34 = interpetText(text34, text34);
									mw.methodTime(int.Parse(text34));
									return "";
								}
								goto IL_09cb;
							}
							mw.playSnapAudio();
							Task.Run(delegate
							{
								mw.strobeCenter();
							});
							return "";
						}
						changeMood(text31);
						return "";
					}
					clamp(clamp: true);
					sentence = "";
				}
			}
		}
		goto IL_09ec;
		IL_09cb:
		if (sentence.Trim() != "")
		{
			sentence = startMethod(sentence.Trim(), derivative);
		}
		goto IL_09ec;
		IL_09ec:
		return sentence;
	}

	private string startMethod(string methodName, bool derivative)
	{
		if (ShouldTraceScriptCommand(methodName))
		{
			SessionTraceLogger.Info("script-command", GetType().Name + " command=" + methodName);
		}
		string text;
		string text2;
		string text3;
		if (methodName != null && methodName.StartsWith("HOOKHANDLED:", StringComparison.OrdinalIgnoreCase))
		{
			string reason = methodName.Replace("HOOKHANDLED:", "").Trim();
			mw.MarkCurrentModHookHandled(string.IsNullOrWhiteSpace(reason) ? "explicit" : reason);
			return "";
		}
		if (methodName != null && methodName.StartsWith("OUTCOME:", StringComparison.OrdinalIgnoreCase))
		{
			string outcomeText = methodName.Replace("OUTCOME:", "").Trim();
			string[] outcomeParts = outcomeText.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			if (outcomeParts.Length >= 2)
			{
				mw.ApplyModOutcome(outcomeParts[0], outcomeParts[1]);
			}
			else
			{
				SessionTraceLogger.Info("mod-outcome", "invalid outcome command=" + methodName);
			}
			return "";
		}
		if (methodName != null && methodName.StartsWith("RUNHOOK:", StringComparison.OrdinalIgnoreCase))
		{
			string hookText = methodName.Replace("RUNHOOK:", "").Trim();
			string[] hookParts = hookText.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			string hookName = hookParts.Length > 0 ? hookParts[0].Trim() : "";
			string fallbackScript = hookParts.Length > 1 ? hookParts[1].Trim() : "";
			if (!string.IsNullOrWhiteSpace(hookName) && !mw.RunModHookFromScript(hookName) && !string.IsNullOrWhiteSpace(fallbackScript))
			{
				SessionTraceLogger.Info("mod-hooks", "hook=" + hookName + " fallbackScript=" + fallbackScript);
				mw.currentScript = new GenericScript(mw, mw.currentScript, fallbackScript);
			}
			return "";
		}
		if (methodName != null && methodName.StartsWith("RUNSCRIPT:", StringComparison.OrdinalIgnoreCase))
		{
			string scriptName = methodName.Replace("RUNSCRIPT:", "").Trim();
			if (!string.IsNullOrWhiteSpace(scriptName))
			{
				SessionTraceLogger.Info("script-command", GetType().Name + " runscript=" + scriptName);
				mw.currentScript = new GenericScript(mw, mw.currentScript, scriptName);
			}
			return "";
		}
		switch (methodName)
		{
		default:
			if (methodName == null)
			{
				goto IL_0d71;
			}
			if (!methodName.StartsWith("ASMRON:"))
			{
				switch (methodName)
				{
				case "DRONINGON:":
					break;
				case "DRONINGOFF:":
					goto IL_08d1;
				case "HYPNOSISIMAGESON:":
					goto IL_08e1;
				case "HYPNOSISIMAGESOFF:":
					goto IL_08f1;
				case "HYPNOSISPLUS:":
					goto IL_0902;
				case "HYPNOSISMIN:":
					goto IL_0913;
				case "STOPSTROKING:":
					goto IL_0924;
				default:
					goto IL_0934;
				}
				mw.methodPlayDroningAudio();
			}
			else
			{
				mw.methodASMRDirectional(methodName);
			}
			break;
		case "ILLEGALCUM:":
			mw.methodIllegalCum();
			break;
		case "BREATHPLAY:":
			mw.methodHoldBreath();
			break;
		case "CANCELBREATH:":
			mw.methodCancelBreath();
			break;
		case "ORGASMDENIED:":
			mw.methodOrgasmDenied();
			break;
		case "HYPNOSISON:":
			mw.methodHypnosisOn();
			break;
		case "HYPNOSISOFF:":
			mw.methodHypnosisOff();
			break;
		case "SUBLIMINALAUDIOON:":
			mw.methodSubliminalAudio(active: true);
			break;
		case "SUBLIMINALAUDIOOFF:":
			mw.methodSubliminalAudio(active: false);
			break;
		case "SUBLIMINALON:":
			mw.methodSubliminal(active: true);
			break;
		case "SUBLIMINALOFF:":
			mw.methodSubliminal(active: true);
			break;
		case "ASMROFF:":
			mw.methodStopASMR();
			break;
		case "ASMRON:":
			{
				mw.methodPlayASMR();
				break;
			}
			IL_09b7:
			mw.methodStrokeFast();
			break;
			IL_09a7:
			mw.methodStrokeNormal();
			break;
			IL_0997:
			mw.methodStrokeOff();
			break;
			IL_0934:
			text = methodName;
			if (!text.StartsWith("SPEED:"))
			{
				switch (methodName)
				{
				case "SPEEDUP:":
					break;
				case "SPEEDDOWN:":
					goto IL_0967;
				case "STROKESLOW:":
					goto IL_0977;
				case "STROKEON:":
					goto IL_0987;
				case "STROKEOFF:":
					goto IL_0997;
				case "STROKENORMAL:":
					goto IL_09a7;
				case "STROKEFAST:":
					goto IL_09b7;
				case "EDGE:":
					goto IL_09c7;
				case "EDGEHOLD:":
					goto IL_09d7;
				case "ORGASMDECIDE:":
					goto IL_09e7;
				case "CBTSTART:":
					goto IL_09f7;
				case "CBTEXTREMESTART:":
					goto IL_0a07;
				case "ANALSTART:":
					goto IL_0a17;
				case "ANALEXTREMESTART:":
					goto IL_0a27;
				case "SILENTSTOP:":
					goto IL_0a37;
				default:
					goto IL_0a47;
				}
				mw.methodSpeedUp();
			}
			else
			{
				mw.methodSpeed(text);
			}
			break;
			IL_0977:
			mw.methodStrokeSlow();
			break;
			IL_0967:
			mw.methodSpeedDown();
			break;
			IL_0a47:
			text2 = methodName;
			if (text2.StartsWith("EDGEHOLD:"))
			{
				mw.methodEdgeHoldDuration(text2);
				break;
			}
			text3 = methodName;
			if (!text3.StartsWith("EDGE:"))
			{
				switch (methodName)
				{
				case "ACTIVATETAGGER:":
					break;
				case "CUM:":
					goto IL_0a9a;
				case "RUIN:":
					goto IL_0aaa;
				case "DENY:":
					goto IL_0aba;
				case "CUMSCRIPT:":
					goto IL_0aca;
				case "RUINSCRIPT:":
					goto IL_0af0;
				case "DENYSCRIPT:":
					goto IL_0b16;
				case "PUNISHMENT:":
					goto IL_0b3c;
				case "MAKENOTE:":
					goto IL_0b62;
				case "CBTBALLS:":
					goto IL_0b88;
				case "CBTCOCK:":
					goto IL_0bae;
				case "CBTEXTREME:":
					goto IL_0bd4;
				case "CBTPALMING:":
					goto IL_0bfa;
				case "ASSSPANKING:":
					goto IL_0c20;
				case "NIPPLEPINCHING:":
					goto IL_0c46;
				case "ANAL:":
					goto IL_0c6c;
				case "VIBE:":
					goto IL_0c93;
				case "CHASTITYON:":
					goto IL_0cb9;
				case "CHASTITYOFF:":
					goto IL_0cd4;
				case "LOSTKEY:":
					goto IL_0cef;
				case "ANALEXTREME:":
					goto IL_0d12;
				case "COMMANDMENTS:":
					goto IL_0d36;
				case "REPORT:":
					goto IL_0d59;
				default:
					goto IL_0d71;
				}
				mw.methodTagger();
			}
			else
			{
				mw.methodEdgeMultiple(text3);
			}
			break;
			IL_0924:
			mw.methodStopStroking();
			break;
			IL_0913:
			mw.methodHypnosisIntensity(-1);
			break;
			IL_0902:
			mw.methodHypnosisIntensity(1);
			break;
			IL_08f1:
			mw.setNewMediaFormat();
			break;
			IL_0d71:
			if (!derivative)
			{
				hasContent = true;
			}
			return methodName;
			IL_0987:
			mw.methodStrokeOn();
			break;
			IL_08d1:
			mw.methodStopDroningAudio();
			break;
			IL_0d59:
			mw.currentScript = new Reporting(mw);
			break;
			IL_0d36:
			mw.currentScript = new Commandments(mw, mw.currentScript);
			break;
			IL_0d12:
			mw.currentScript = new Anal(mw, mw.currentScript, extreme: true);
			break;
			IL_0cef:
			mw.currentScript = new ChastityLostKey(mw, mw.currentScript);
			break;
			IL_0cd4:
			mw.currentScript = new ChastityRemove(mw);
			break;
			IL_0cb9:
			mw.currentScript = new ChastityWear(mw);
			break;
			IL_0c93:
			mw.currentScript = new Vibe(mw, mw.currentScript);
			break;
			IL_0c6c:
			mw.currentScript = new Anal(mw, mw.currentScript);
			break;
			IL_0c46:
			mw.currentScript = new CbtNipplePinching(mw, mw.currentScript);
			break;
			IL_0c20:
			mw.currentScript = new AssSpanking(mw, mw.currentScript);
			break;
			IL_0bfa:
			mw.currentScript = new CbtPalming(mw, mw.currentScript);
			break;
			IL_0bd4:
			mw.currentScript = new CbtExtreme(mw, mw.currentScript);
			break;
			IL_0bae:
			mw.currentScript = new CbtCock(mw, mw.currentScript);
			break;
			IL_0b88:
			mw.currentScript = new CbtBalls(mw, mw.currentScript);
			break;
			IL_0b62:
			mw.currentScript = new MakeNote(mw, mw.currentScript);
			break;
			IL_0b3c:
			mw.currentScript = new Punishment(mw, mw.currentScript);
			break;
			IL_0b16:
			mw.currentScript = new OrgasmDecideDeny(mw, mw.currentScript);
			break;
			IL_0af0:
			mw.currentScript = new OrgasmDecideRuin(mw, mw.currentScript);
			break;
			IL_0aca:
			mw.currentScript = new OrgasmDecideCum(mw, mw.currentScript);
			break;
			IL_0aba:
			mw.methodDenial();
			break;
			IL_0aaa:
			mw.methodRuin();
			break;
			IL_0a9a:
			mw.methodCumming();
			break;
			IL_08e1:
			mw.methodHypnosisImagesOn();
			break;
			IL_0a37:
			mw.methodSilentStop();
			break;
			IL_0a27:
			mw.methodAnalExtremeStart();
			break;
			IL_0a17:
			mw.methodAnalStart();
			break;
			IL_0a07:
			mw.methodCBTExtremeStart();
			break;
			IL_09f7:
			mw.methodCBTStart();
			break;
			IL_09e7:
			mw.methodOrgasmDecide();
			break;
			IL_09d7:
			mw.methodEdgeHold();
			break;
			IL_09c7:
			mw.methodEdge();
			break;
		}
		if (!derivative)
		{
			hasContent = true;
		}
		return "";
	}

	private void placeTag(string s, bool isTemp = false)
	{
		s = s.Replace("TEMPTAG:", "");
		s = s.Replace("TAG:", "");
		s = s.Trim();
		tags = s;
		if (s != "")
		{
			mw.setTag(s, isTemp);
		}
		else
		{
			mw.unSetTag();
		}
	}

	private void sessionTime(string s)
	{
		s = s.Replace("SESSIONTIME:", "");
		s = s.Trim();
		mw.sessionLength += int.Parse(s) * 60;
	}

	private void nextSessionTime(int extraTime)
	{
		mw.setVar("extraTime", extraTime.ToString() ?? "");
	}

	private void sessionTimeRelative(string s)
	{
		s = s.Replace("SESSIONTIMERELATIVE:", "");
		s = s.Trim();
		double num = double.Parse(s);
		int num2 = (int)((double)mw.sessionLength * num);
		int extraTime = mw.sessionLength - num2;
		mw.sessionLength = num2;
		nextSessionTime(extraTime);
	}

	private void textField(string s)
	{
		s = s.Replace("TEXTFIELD:", "");
		s = s.Trim();
		string[] array = s.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].Trim();
		}
		if (array.Length > 1)
		{
			mw.createTextField(array[0], array[1]);
		}
		else
		{
			mw.createTextField(array[0]);
		}
	}

	private void changeMood(string s)
	{
		s = s.Replace("MOOD:", "");
		s = s.Trim();
		mw.changeMoodBy(int.Parse(s));
	}

	private void kneel(bool kneeling)
	{
		if (kneeling)
		{
			mw.currentScript = new Kneel(mw, mw.currentScript);
		}
		else
		{
			mw.currentScript = new KneelNo(mw, mw.currentScript);
		}
		mw.currentState = "module";
	}

	private void fours(bool fours)
	{
		if (fours)
		{
			mw.currentScript = new Fours(mw, mw.currentScript);
		}
		else
		{
			mw.currentScript = new FoursNo(mw, mw.currentScript);
		}
		mw.currentState = "module";
	}

	private void plug(bool plug)
	{
		if (plug)
		{
			mw.currentScript = new Plug(mw, mw.currentScript);
		}
		else
		{
			mw.currentScript = new PlugNo(mw, mw.currentScript);
		}
		mw.currentState = "module";
	}

	private void ona(bool ona)
	{
		if (ona)
		{
			mw.currentScript = new Ona(mw, mw.currentScript);
		}
		else
		{
			mw.currentScript = new OnaNo(mw, mw.currentScript);
		}
		mw.currentState = "module";
	}

	private void ballsBound(bool ballsBound)
	{
		if (ballsBound)
		{
			mw.currentScript = new BallsBound(mw, mw.currentScript);
		}
		else
		{
			mw.currentScript = new BallsBoundNo(mw, mw.currentScript);
		}
		mw.currentState = "module";
	}

	private void collared(bool collared)
	{
		if (collared)
		{
			mw.currentScript = new Collar(mw, mw.currentScript);
		}
		else
		{
			mw.currentScript = new CollarNo(mw, mw.currentScript);
		}
		mw.currentState = "module";
	}

	private void gagged(bool gagged)
	{
		if (gagged)
		{
			mw.currentScript = new Gag(mw, mw.currentScript);
		}
		else
		{
			mw.currentScript = new GagNo(mw, mw.currentScript);
		}
		mw.currentState = "module";
	}

	private void clamp(bool clamp)
	{
		if (clamp)
		{
			mw.currentScript = new Clamp(mw, mw.currentScript);
		}
		else
		{
			mw.currentScript = new ClampNo(mw, mw.currentScript);
		}
		mw.currentState = "module";
	}

	private void repeat()
	{
		repeating = true;
	}

	public string getFileLocation(string name, bool temp = false)
	{
		if (temp)
		{
			return RuntimePaths.TempFlag(name);
		}
		return RuntimePaths.Flag(name);
	}

	public void setFlag(string flagName, bool temp = false)
	{
		flagName = flagName.Replace("FLAG:", "");
		flagName = flagName.Replace("FLAGT:", "");
		flagName = flagName.Trim();
		SessionTraceLogger.Info("state-write", "set flag temp=" + temp + " name=" + flagName);
		if (temp)
		{
			mw.setTFlag(flagName);
		}
		else
		{
			mw.setPersistentFlag(flagName);
		}
	}

	public bool chance(string flagName)
	{
		flagName = flagName.Replace("CHANCE:", "");
		return int.Parse(flagName) >= random.Next(0, 100);
	}

	public bool getFlag(string flagName, bool temp = false)
	{
		flagName = flagName.Replace("ISFLAG:", "");
		flagName = flagName.Replace("ISFLAGT:", "");
		flagName = flagName.Replace("ISNOFLAG:", "");
		flagName = flagName.Replace("ISNOFLAGT:", "");
		flagName = flagName.Trim();
		if (temp)
		{
			return mw.getTFlag(flagName);
		}
		return mw.getFlag(flagName);
	}

	public void setVar(string flagName)
	{
		flagName = flagName.Replace("SETVAR:", "");
		string[] array = flagName.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].Trim();
		}
		array[1] = interpetText(array[1], array[1]);
		SessionTraceLogger.Info("state-write", "set var " + array[0] + "=" + array[1]);
		mw.setVar(array[0], array[1]);
	}

	private void setSetting(string settingString)
	{
		settingString = settingString.Replace("SETSETTING:", "");
		string[] array = settingString.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length < 2)
		{
			return;
		}
		string key = array[0].Trim();
		string text = interpetText(array[1].Trim(), array[1].Trim());
		bool enabled = string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "on", StringComparison.OrdinalIgnoreCase) || text == "1";
		SessionTraceLogger.Info("state-write", "set setting " + key + "=" + enabled);
		mw.setSettingEnabled(key, enabled);
	}

	private void setSettingValue(string settingString)
	{
		settingString = settingString.Replace("SETSETTINGVALUE:", "");
		string[] array = settingString.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length < 2)
		{
			return;
		}
		string key = array[0].Trim();
		string text = interpetText(array[1].Trim(), array[1].Trim());
		if (int.TryParse(text, out int result))
		{
			SessionTraceLogger.Info("state-write", "set setting value " + key + "=" + result);
			mw.setSettingValue(key, result);
		}
		else
		{
			SessionTraceLogger.Info("state-write", "set setting text " + key + "=" + text);
			mw.setSettingText(key, text);
		}
	}

	private void setAnalStage(string settingString)
	{
		string text = interpetText(settingString.Replace("SETANALSTAGE:", "").Trim(), settingString.Replace("SETANALSTAGE:", "").Trim());
		SessionTraceLogger.Info("state-write", "set anal stage " + text);
		mw.setAnalStage(text);
	}

	private void setAnalPreference(string settingString)
	{
		string text = interpetText(settingString.Replace("SETANALPREFERENCE:", "").Trim(), settingString.Replace("SETANALPREFERENCE:", "").Trim());
		SessionTraceLogger.Info("state-write", "set anal preference " + text);
		mw.setAnalPreference(text);
	}

	private void setAnalTraining(string settingString)
	{
		string text = interpetText(settingString.Replace("SETANALTRAINING:", "").Trim(), settingString.Replace("SETANALTRAINING:", "").Trim());
		SessionTraceLogger.Info("state-write", "set anal training " + text);
		mw.setAnalTraining(text);
	}

	private void setPetPersona(string settingString)
	{
		string text = interpetText(settingString.Replace("SETPETPERSONA:", "").Trim(), settingString.Replace("SETPETPERSONA:", "").Trim());
		SessionTraceLogger.Info("state-write", "set pet persona " + text);
		mw.setPetPersona(text);
	}

	private void setOutsideRule(string settingString)
	{
		settingString = settingString.Replace("SETOUTSIDERULE:", "");
		string[] array = settingString.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length < 2)
		{
			return;
		}
		string key = array[0].Trim();
		string text = interpetText(array[1].Trim(), array[1].Trim());
		if (int.TryParse(text, out int result))
		{
			SessionTraceLogger.Info("state-write", "set outside rule " + key + "=" + result);
			mw.setOutsideRule(key, result);
		}
	}

	private void setLobWindow(string settingString)
	{
		settingString = settingString.Replace("SETLOBWINDOW:", "");
		string[] array = settingString.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length < 2)
		{
			return;
		}
		string earlyText = interpetText(array[0].Trim(), array[0].Trim());
		string lateText = interpetText(array[1].Trim(), array[1].Trim());
		if (int.TryParse(earlyText, out int earlyHour) && int.TryParse(lateText, out int lateHour))
		{
			SessionTraceLogger.Info("state-write", "set LOB window " + earlyHour + "," + lateHour);
			mw.setLobWindow(earlyHour, lateHour);
		}
	}

	public bool isVar(string flagName)
	{
		flagName = flagName.Replace("ISVAR:", "");
		string[] array = flagName.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].Trim();
		}
		array[1] = interpetText(array[1], array[1]);
		bool result = true;
		if (array[1] != "")
		{
			result = mw.getFlagTime(array[0]) - double.Parse(array[1]) >= 0.0;
		}
		return result;
	}

	private bool biggerThan(string flagName)
	{
		flagName = flagName.Replace("BIGGERTHAN:", "");
		string[] array = flagName.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].Trim();
		}
		array[0] = interpetText(array[0], array[0]);
		array[1] = interpetText(array[1], array[1]);
		return int.Parse(array[0]) > int.Parse(array[1]);
	}

	public void incrementFlag(string flagName)
	{
		flagName = flagName.Replace("INCREMENT:", "");
		string text = getVar(flagName);
		if (text == "")
		{
			text = "0";
		}
		text = (int.Parse(text) + 1).ToString() ?? "";
		mw.setVar(flagName, text);
	}

	public void decrementFlag(string flagName)
	{
		flagName = flagName.Replace("DECREMENT:", "");
		string text = getVar(flagName);
		if (text == "")
		{
			text = "0";
		}
		text = (int.Parse(text) - 1).ToString() ?? "";
		mw.setVar(flagName, text);
	}

	public void tribute(string amount)
	{
		amount = amount.Replace("TRIBUTE:", "");
		amount = amount.Trim();
		amount = interpetText(amount, amount);
		mw.setFavor(int.Parse(mw.getVar("favor")) - int.Parse(amount));
		mw.setVar("totalTribute", (int.Parse(amount) + int.Parse(mw.getVar("totalTribute"))).ToString() ?? "");
	}

	public void task(string taskString)
	{
		taskString = taskString.Replace("TASK:", "");
		taskString = taskString.Trim();
		taskString = interpetText(taskString, taskString);
		string[] array = taskString.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 3)
		{
			mw.setNewTask(int.Parse(array[0]), int.Parse(array[1]), int.Parse(array[2]));
		}
		else if (array.Length == 2)
		{
			mw.setNewTask(int.Parse(array[0]), int.Parse(array[1]));
		}
		else if (array.Length == 1)
		{
			mw.setNewTask(int.Parse(array[0]));
		}
		else
		{
			mw.setNewTask();
		}
		setFlag("taskAdded", temp: true);
	}

	public void showFavor()
	{
		mw.showFavor();
	}

	public void hideFavor()
	{
		mw.hideFavor();
	}

	public void length(string flagName)
	{
		flagName = flagName.Replace("LENGTH:", "");
		flagName = flagName.Trim();
		mw.sessionLength += int.Parse(flagName);
	}

	public void addVar(string flagName)
	{
		string originalFlagName = flagName;
		flagName = flagName.Replace("ADDVAR:", "");
		flagName = flagName.Trim();
		string[] array = flagName.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].Trim();
		}
		array[0] = interpetText(array[0], array[0]);
		array[1] = interpetText(array[1], array[1]);
		string newValue = (int.Parse(array[1]) + int.Parse(mw.getVar(array[0]))).ToString() ?? "";
		SessionTraceLogger.Info("state-write", "add var " + array[0] + " += " + array[1] + " => " + newValue + " from=" + originalFlagName);
		mw.setVar(array[0], newValue);
	}

	public string getVar(string flagName)
	{
		flagName = flagName.Replace("GETVAR:", "");
		return mw.getVar(flagName);
	}

	public string getTime(string flagName)
	{
		flagName = flagName.Replace("GETTIME:", "");
		return mw.getFlagTime(flagName).ToString() ?? "";
	}

	public void deleteFlag(string flagName, bool temp = false)
	{
		flagName = flagName.Replace("DELFLAG:", "");
		flagName = flagName.Replace("DELFLAGT:", "");
		flagName = flagName.Trim();
		if (temp)
		{
			if (mw.getTFlag(flagName))
			{
				SessionTraceLogger.Info("state-write", "delete flag temp=" + temp + " name=" + flagName);
			}
			mw.deleteTFlag(flagName);
		}
		else
		{
			SessionTraceLogger.Info("state-write", "delete flag temp=" + temp + " name=" + flagName);
			mw.deletePersistentFlag(flagName);
		}
	}

	public void goToGOTO(string s)
	{
		s = s.Replace("GOTO:", "");
		s = s.Trim();
		string[] array = s.Split(',');
		if (array.Length != 0)
		{
			s = array[random.Next(0, array.Length)];
		}
		for (int i = 0; i < allText.Length; i++)
		{
			if (allText[i] == "(" + s + ")")
			{
				location = i;
				break;
			}
		}
	}

	public void buttonClicked(string buttonText)
	{
		string value = "[" + buttonText + "]";
		for (int i = location; i < allText.Length; i++)
		{
			if (allText[i].Contains(value))
			{
				location = i;
				break;
			}
		}
	}

	public string command(string s)
	{
		s = s.Replace("COMMAND:", "");
		s = s.Trim();
		return ask(s, command: true);
	}

	public string ask(string question, bool command = false)
	{
		question = question.Replace("ASK:", "");
		question = question.Trim();
		Task.Delay(100).ContinueWith(delegate
		{
			mw.setTPOTo1Question();
		});
		List<string> list = new List<string>();
		for (int num = location; num < allText.Length; num++)
		{
			int num2 = num + 1;
			if (num2 >= allText.Length || allText[num2].StartsWith("ASK:") || allText[num2].StartsWith("COMMAND:"))
			{
				break;
			}
			if (allText[num2].Split(" ")[0].StartsWith("IS"))
			{
				if (interpetText(allText[num2].Split(" ")[0], allText[num2].Split(" ")[0]).Contains("flagFailed") || !allText[num2].Contains("[") || !allText[num2].Contains("]"))
				{
					continue;
				}
				string text = allText[num2].Split("[")[^1].Replace("[", "").Replace("]", "");
				string text2 = "";
				string[] array = text.Split(" ");
				foreach (string text3 in array)
				{
					string text4 = interpetText(text3.Trim(), text);
					if (text4 == "flagFailed")
					{
						text2 = "";
						break;
					}
					text2 = text2 + text4 + " ";
				}
				text2 = CheckWords(text2.Trim()).Trim();
				list.Add(text2);
			}
			else
			{
				if (!allText[num2].Contains("[") || !allText[num2].Contains("]"))
				{
					continue;
				}
				string text5 = allText[num2].Split("[")[^1].Replace("[", "").Replace("]", "");
				string text6 = "";
				string[] array = text5.Split(" ");
				foreach (string text7 in array)
				{
					string text8 = interpetText(text7.Trim(), text5);
					if (text8 == "flagFailed")
					{
						text6 = "";
						break;
					}
					text6 = text6 + text8 + " ";
				}
				text6 = CheckWords(text6.Trim()).Trim();
				list.Add(text6);
				allText[num2] = "[" + text6 + "]";
			}
		}
		if (list.Count > 0)
		{
			mw.scriptPaused = true;
			string[] array2 = list.ToArray();
			int[] array3 = new int[array2.Length];
			for (int num4 = 0; num4 < array3.Length; num4++)
			{
				array3[num4] = 0;
			}
			if (command)
			{
				array2 = array2.Concat(new string[1] { mw.lr.getVocab("refuse") }).ToArray();
				array3 = array3.Concat(new int[1] { 4 }).ToArray();
			}
			mw.createNewButtons(array2, array3);
		}
		return question;
	}

	public string CheckWords(string sentence)
	{
		string[] array = sentence.Split(' ');
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].Trim();
			string word = getWord(array[i]);
			word = word.Trim();
			if (word == "AGAINSTWISHES")
			{
				return "";
			}
			array[i] = word;
			array[i] += " ";
		}
		sentence = "";
		string[] array2 = array;
		foreach (string text in array2)
		{
			sentence += text;
		}
		return sentence;
	}

	public string getWord(string word)
	{
		if (word.StartsWith("@"))
		{
			string text = "";
			word = word.Replace("@", "");
			if (word.EndsWith(","))
			{
				word = word.Replace(',', ' ');
				text = ",";
			}
			if (word.EndsWith("'s"))
			{
				word = word.Replace("'s", "");
				text = "'s";
			}
			if (word.EndsWith("."))
			{
				word = word.Replace('.', ' ');
				text = ".";
			}
			if (word.EndsWith("?"))
			{
				word = word.Replace('?', ' ');
				text = "?";
			}
			if (word.EndsWith("!"))
			{
				word = word.Replace('!', ' ');
				text = "!";
			}
			if (word.EndsWith("*"))
			{
				word = word.Replace('*', ' ');
				text = "*";
			}
			if (word.EndsWith("\""))
			{
				word = word.Replace("\"", " ");
				text = "\"";
			}
			word = word.Trim();
			return voc.getVoc(word) + text;
		}
		return word;
	}

	private static bool ShouldTraceScriptCommand(string methodName)
	{
		if (string.IsNullOrWhiteSpace(methodName))
		{
			return false;
		}
		if (methodName.Contains(':'))
		{
			return true;
		}
		if (methodName.Length < 2)
		{
			return false;
		}
		bool hasLetter = false;
		foreach (char character in methodName)
		{
			if (char.IsLetter(character))
			{
				hasLetter = true;
				if (char.IsLower(character))
				{
					return false;
				}
			}
		}
		return hasLetter;
	}

	private void endSession()
	{
		if (!getFlag("closeWithoutSession", temp: true))
		{
			string[] files = Directory.GetFiles(RuntimePaths.TempFlagsDir);
			for (int i = 0; i < files.Length; i++)
			{
				File.Delete(files[i]);
			}
			setFlag("sessionEnd");
			incrementFlag("sessions");
			mw.setVar("sessionLength", "0");
		}
		mw.sessionActive = false;
		mw.endSession();
	}
}
