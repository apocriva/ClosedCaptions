using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ClosedCaptions.Config;
using ClosedCaptions.Extensions;
using ClosedCaptions.GUI;
using HarmonyLib;
using OpenTK.Audio.OpenAL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client;
using Vintagestory.Common;

namespace ClosedCaptions;

[HarmonyPatchCategory("closedcaptions")]
public class CaptionManager
{
	private static CaptionManager? _instance;
	private static CaptionManager Instance
	{
		get
		{
			if (_instance == null)
				throw new NullReferenceException();
			return _instance;
		}
	}

	private static ICoreClientAPI? _capi;
	public static ICoreClientAPI Api
	{
		get
		{
			if (_capi == null)
				throw new NullReferenceException();
			return _capi;
		}
	}

	private readonly ClosedCaptionsOverlay _overlay;
	private readonly MatchConfig _matchConfig;
	private readonly Dictionary<int, Caption> _captions = [];
	private readonly List<Caption> _displayedCaptions = [];

	private bool _needsRefresh = false;

	public CaptionManager(ICoreClientAPI capi)
	{
		_instance = this;
		_capi = capi;

		try
		{
			_matchConfig = capi.Assets.Get(new AssetLocation("closedcaptions", MatchConfig.Filename)).ToObject<MatchConfig>();
			_matchConfig.Api = capi;
		}
		catch (Exception e)
		{
			capi.Logger.Error($"Error loading {MatchConfig.Filename}: {e}");
			_matchConfig = new(capi);
		}

		_overlay = new(capi);
	}

	public static List<Caption> GetDisplayedCaptions()
	{
		return [.. Instance._displayedCaptions];
	}

	public void Dispose()
	{
		_overlay?.Dispose();
	}

	public void Tick()
	{
		UpdateSoundsStatus();

		if (_needsRefresh)
		{
			ForceRefresh();
			_needsRefresh = false;
		}
	}

	public void ForceRefresh()
	{
		RebuildDisplayCaptions();
		_overlay.Rebuild();
	}

#region Harmony patches
	[HarmonyPostfix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "Start")]
	public static void Sound_Start(LoadedSoundNative __instance)
	{
		if (Instance._captions.TryGetValue(__instance.GetSourceID(), out Caption? oldCaption))
		{
			// Orphan the old caption. We don't have its underlying
			// source ID anymore, so we will let it complete gracefully.
			var oldId = oldCaption.ID;
			oldCaption.Orphan();
			Instance._captions.Remove(oldId);
			Instance._captions.Add(oldCaption.ID, oldCaption);

			// if (__instance.Params.Location.ToString().Contains("walk/grass"))
			// 	Api.Logger.Debug($"[ClosedCaptions] sound.Start() orphaning [{oldId}] -> [{caption.ID}] '{caption.AssetLocation}");
		}

		Instance._matchConfig.BuildCaptionForSound(__instance, out Caption? caption, out var wasIgnored);
		if (wasIgnored)
		{
			//Api.Logger.Debug($"[ClosedCaptions] sound.Start() ignored '{__instance.Params.Location}'");
			return;
		}

		if (caption == null)
			throw new Exception($"[ClosedCaptions] sound.Start() failed to generate caption for '{__instance.Params.Location}'");

		// if (__instance.Params.Location.ToString().Contains("walk/grass"))
		// 	Api.Logger.Debug($"[ClosedCaptions] sound.Start() new [{__instance.GetSourceID()}] '{__instance.Params.Location}");
		Instance.AddCaption(caption);
	}

	[HarmonyPostfix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetVolume", [])]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetVolume", typeof(float))]
	public static void Sound_SetVolume(LoadedSoundNative __instance)
	{
		if (!Instance._captions.ContainsKey(__instance.GetSourceID()))
			Sound_Start(__instance);
	}

	[HarmonyPostfix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "Stop")]
	public static void Sound_Stop(LoadedSoundNative __instance)
	{
		if (!Instance._captions.TryGetValue(__instance.GetSourceID(), out Caption? caption))
		{
			//Api.Logger.Debug($"[ClosedCaptions] sound.Stop() for untracked sound. [{__instance.GetSourceID()}] '{__instance.Params.Location}'");
			return;
		}

		if (!caption.IsFading)
			caption.BeginFade();
	}
#endregion

	private void AddCaption(Caption caption)
	{
		if (_captions.ContainsKey(caption.ID))
		{
			Api.Logger.Warning($"[ClosedCaptions] Attempting to add duplicate caption. [{caption.ID}] '{caption.AssetLocation}'");
			return;
		}

		// if (caption.Text.Contains("rass"))
		// 	Api.Logger.Debug($"[ClosedCaptions] Added tracked sound [{caption.ID}] '{caption.AssetLocation}");
		_captions.Add(caption.ID, caption);
		AddOrUpdateDisplayedCaption(caption);
	}

	private void RemoveCaption(Caption caption)
	{
		if (!_captions.ContainsKey(caption.ID))
		{
			Api.Logger.Warning($"[ClosedCaptions] Attempting to remove untracked caption. [{caption.ID}] '{caption.AssetLocation}'");
			return;
		}

		// if (caption.Text.Contains("rass"))
		// 	Api.Logger.Debug($"[ClosedCaptions] Removed tracked sound [{caption.ID}] '{caption.AssetLocation}");
		_captions.Remove(caption.ID);
		_displayedCaptions.RemoveAll(match => match.ID == caption.ID);

		// This might have been suppressing another caption, let's just rebuild the display list.
		_needsRefresh = true;
	}

	private void RebuildDisplayCaptions()
	{
		_displayedCaptions.Clear();

		foreach (var kv in _captions)
		{
			if (kv.Value == null)
			{
				Api.Logger.Warning($"[ClosedCaptions] Captions list has a null caption for ID [{kv.Key}]");
				_captions.Remove(kv.Key);
				continue;
			}

			AddOrUpdateDisplayedCaption(kv.Value);
		}
	}

	private void AddOrUpdateDisplayedCaption(Caption caption)
	{
		int removed = _displayedCaptions.RemoveAll(c => c == caption);

		// We don't show filtered captions.
		if (IsFiltered(caption))
		{
			if (removed > 0)
			{
				// if (caption.Text.Contains("rass"))
				// 	Api.Logger.Debug($"[ClosedCaptions] On update, displayed sound is filtered. [{caption.ID}] '{caption.AssetLocation}'");
				_needsRefresh = true;
			}
			return;
		}

		float distance = caption.IsRelative ? 0f : (caption.Position - Api.World.Player.Entity.Pos.XYZFloat).Length();
		bool shouldAdd = true;
		for (int i = _displayedCaptions.Count - 1; i >= 0; --i)
		{
			var comp = _displayedCaptions[i];
			if (comp.Group != null && caption.Group != null &&
				comp.Group.Name == caption.Group.Name)
			{
				if (caption.Group.Priority > comp.Group.Priority)
				{
					// Only allow the highest priority to stay.
					_displayedCaptions.RemoveAt(i);
					break;
				}
				else if (caption.Group.Priority == comp.Group.Priority)
				{
					// If we are the same priority we'll try to take the closest.
					float compDistance = comp.IsRelative ? 0f : (comp.Position - Api.World.Player.Entity.Pos.XYZFloat).Length();

					if (!caption.IsRelative && !comp.IsRelative &&
						distance < compDistance)
					{
						// New caption is closer!
						_displayedCaptions.RemoveAt(i);
						break;
					}
					else if (caption.Volume > comp.Volume)
					{
						// New caption is louder!
						_displayedCaptions.RemoveAt(i);
						break;
					}
					else
					{
						shouldAdd = false;
					}
				}
				else
				{
					// Lower priority! Not a chance!
					shouldAdd = false;
					break;
				}
			}

			// Are we close enough that we should be grouped anyway?
			if (caption.Text == comp.Text)
			{
				var distTime = Math.Abs(caption.StartTime - comp.StartTime);
				var checkSpace = !caption.IsRelative && !comp.IsRelative;
				var distSpace = (comp.Position - caption.Position).Length();
				var keepNew = false;
				if (checkSpace && distSpace <= ClosedCaptionsModSystem.UserConfig.GroupingRange &&
					distTime <= ClosedCaptionsModSystem.UserConfig.GroupingMaxTime ||
					!checkSpace && distTime <= ClosedCaptionsModSystem.UserConfig.GroupingMaxTime)
				{
					// Keep the most recent as long as it isn't fading.
					if (caption.StartTime > comp.StartTime &&
						!caption.IsFading ||
						comp.IsFading)
					{
						keepNew = true;
					}
					else if (caption.StartTime < comp.StartTime &&
						!comp.IsFading ||
						caption.IsFading)
					{
						keepNew = false;
					}
					else
					{
						// Too many things going on, just keep the new one.
						keepNew = true;
					}
				}

				if (keepNew)
				{
					_displayedCaptions.RemoveAt(i);
					shouldAdd = true;
				}
				else
				{
					shouldAdd = false;
				}
			}
		}

		if (shouldAdd)
		{
			_displayedCaptions.Add(caption);
			if (removed == 0)
				_needsRefresh = true;
		}
		else if (removed > 0)
		{
			_needsRefresh = true;
		}
	}

	private void UpdateSoundsStatus()
	{
		foreach (var caption in _captions.Values)
		{
			if (caption.IsFading)
			{
				// if (caption.Text.Contains("rass"))
				// 	Api.Logger.Debug($"[ClosedCaptions] Fading... {Api.ElapsedMilliseconds - caption.FadeOutStartTime}ms [{caption.ID}] '{caption.AssetLocation}'");
				if (Api.ElapsedMilliseconds - caption.FadeOutStartTime >= ClosedCaptionsModSystem.UserConfig.FadeOutDuration)
				{
					// if (caption.Text.Contains("rass"))
					// 	Api.Logger.Debug($"[ClosedCaptions] Sound finished fading. [{caption.ID}] '{caption.AssetLocation}'");
					RemoveCaption(caption);
				}

				continue;
			}

			if (AL.IsSource(caption.ID))
			{
				AL.GetSource(caption.ID, ALGetSourcei.SourceState, out var statei);
				var state = (ALSourceState)statei;
				if (state == ALSourceState.Playing)
				{
					AL.GetSource(caption.ID, ALSourcef.Gain, out var value);
					caption.Volume = value;

					AL.GetSource(caption.ID, ALSource3f.Position, out var position);
					caption.Position = new Vec3f(position.X, position.Y, position.Z);

					AddOrUpdateDisplayedCaption(caption);
					continue;
				}
				// else
				// {
				// 	if (caption.Text.Contains("rass"))
				// 		Api.Logger.Debug($"[ClosedCaptions] No longer playing, starting fade{(caption.IsFading ? "but is already fading?" : "")}. [{caption.ID}] '{caption.AssetLocation}'");
				// }
			}
			// else
			// {
			// 	if (caption.Text.Contains("rass"))
			// 		Api.Logger.Debug($"[ClosedCaptions] No longer valid source, starting fade{(caption.IsFading ? "but is already fading?" : "")}. [{caption.ID}] '{caption.AssetLocation}'");
			// }

			// Sound is no longer visible for one reason or another.
			caption.BeginFade();
		}
	}
	
	private bool IsFiltered(Caption caption)
	{
		// If any one of these passes, do not filter. This supercedes cases such as, for example
		// a nearby lightning strike being filtered due to ShowWeather, but should be shown because
		// it is tagged as Danger. We still want to let the case fall through if ShowDanger is unchecked
		// but ShowWeather is checked.
		if (caption.Tags != CaptionTags.None)
		{
			if ((caption.Tags & CaptionTags.Ambience) != 0 && ClosedCaptionsModSystem.UserConfig.ShowAmbience ||
				(caption.Tags & CaptionTags.Animal) != 0 && ClosedCaptionsModSystem.UserConfig.ShowAnimal ||
				(caption.Tags & CaptionTags.Block) != 0 && ClosedCaptionsModSystem.UserConfig.ShowBlock ||
				(caption.Tags & CaptionTags.Combat) != 0 && ClosedCaptionsModSystem.UserConfig.ShowCombat ||
				(caption.Tags & CaptionTags.Danger) != 0 && ClosedCaptionsModSystem.UserConfig.ShowDanger ||
				(caption.Tags & CaptionTags.Enemy) != 0 && ClosedCaptionsModSystem.UserConfig.ShowEnemy ||
				(caption.Tags & CaptionTags.Environment) != 0 && ClosedCaptionsModSystem.UserConfig.ShowEnvironment ||
				(caption.Tags & CaptionTags.Interaction) != 0 && ClosedCaptionsModSystem.UserConfig.ShowInteraction ||
				(caption.Tags & CaptionTags.Temporal) != 0 && ClosedCaptionsModSystem.UserConfig.ShowTemporal ||
				(caption.Tags & CaptionTags.Tool) != 0 && ClosedCaptionsModSystem.UserConfig.ShowTool ||
				(caption.Tags & CaptionTags.Voice) != 0 && ClosedCaptionsModSystem.UserConfig.ShowVoice ||
				(caption.Tags & CaptionTags.Walk) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWalk ||
				(caption.Tags & CaptionTags.Wearable) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWearable ||
				(caption.Tags & CaptionTags.Weather) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWeather)
				return false;
		}

		// It is a tagged sound and should not be shown.
		if (caption.Tags != CaptionTags.None)
			return true;

		return false;
	}
}