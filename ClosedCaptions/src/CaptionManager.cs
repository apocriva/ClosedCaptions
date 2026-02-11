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
		// Fully rebuild display list?
		_overlay.Rebuild();
	}

#region Harmony patches
	// [HarmonyPostfix()]
	// [HarmonyPatch(typeof(LoadedSoundNative), "disposeSoundSource")]
	// public static void Sound_Dispose(LoadedSoundNative __instance)
	// {
	// 	if (!Instance._captions.TryGetValue(__instance.GetSourceID(), out Caption? caption))
	// 	{
	// 		//Api.Logger.Debug($"[ClosedCaptions] sound.disposeSoundSource() for untracked sound '{__instance.Params.Location}'");
	// 		return;
	// 	}

	// 	Api.Logger.Debug($"[ClosedCaptions] sound.disposeSoundSource() '{__instance.Params.Location}'");
	// 	Instance.RemoveCaption(caption);
	// }

	[HarmonyPostfix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "Start")]
	public static void Sound_Start(LoadedSoundNative __instance)
	{
		if (Instance._captions.TryGetValue(__instance.GetSourceID(), out Caption? caption))
		{
			Api.Logger.Debug($"[ClosedCaptions] sound.Start() for existing sound '{__instance.Params.Location}'");
			caption.UpdateFrom(__instance);
			return;
		}

		Instance._matchConfig.BuildCaptionForSound(__instance, out caption, out var wasIgnored);
		if (wasIgnored)
		{
			Api.Logger.Debug($"[ClosedCaptions] sound.Start() ignored '{__instance.Params.Location}'");
			return;
		}

		if (caption == null)
			throw new Exception($"[ClosedCaptions] sound.Start() failed to generated caption for '{__instance.Params.Location}'");

		Instance.AddCaption(caption);
	}

	[HarmonyPostfix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "Stop")]
	public static void Sound_Stop(LoadedSoundNative __instance)
	{
		if (!Instance._captions.TryGetValue(__instance.GetSourceID(), out Caption? caption))
		{
			//Api.Logger.Debug($"[ClosedCaptions] sound.Stop() for untracked sound '{__instance.Params.Location}'");
			return;
		}

		Instance.RemoveCaption(caption);
	}
#endregion

	private void AddCaption(Caption caption)
	{
		if (_captions.ContainsKey(caption.ID))
			throw new Exception($"[ClosedCaptions] Attempting to add duplicate caption. [{caption.ID}] '{caption.AssetLocation}'");

		Api.Logger.Debug($"[ClosedCaptions] Added tracked sound [{caption.ID}] '{caption.AssetLocation}");
		_captions.Add(caption.ID, caption);
		AddDisplayedCaption(caption);
	}

	private void RemoveCaption(Caption caption)
	{
		if (!_captions.ContainsKey(caption.ID))
			throw new Exception($"[ClosedCaptions] Attempting to remove untracked caption. [{caption.ID}] '{caption.AssetLocation}'");

		Api.Logger.Debug($"[ClosedCaptions] Removed tracked sound [{caption.ID}] '{caption.AssetLocation}");
		_captions.Remove(caption.ID);
		_displayedCaptions.RemoveAll(match => match.ID == caption.ID);

		// This might have been suppressing another caption!
		if (caption.Group != null)
		{
			_captions.Where(e => e.Value.Group != null && e.Value.Group.Name == caption.Group.Name)
				.Do(c => AddDisplayedCaption(c.Value));
		}

		_needsRefresh = true;
	}

	private void AddDisplayedCaption(Caption caption)
	{
		// Add into the display list if appopriate.
		float distance = caption.IsRelative ? 0f : (caption.Position - Api.World.Player.Entity.Pos.XYZFloat).Length();

		if (_displayedCaptions.Where(check => check.ID == caption.ID).Any())
		{
			Api.Logger.Warning($"[ClosedCaptions] Display list already contains new caption. [{caption.ID}] '{caption.AssetLocation}'");
			return;
		}

		bool shouldAdd = true;
		for (int i = _displayedCaptions.Count - 1; i >= 0; --i)
		{
			var comp = _displayedCaptions[i];
			if (comp.Group != null && caption.Group != null &&
				comp.Group.Name == caption.Group.Name)
			{
				// We're in the same group! Only allow the highest priority to stay.
				if (caption.Group.Priority >= comp.Group.Priority)
				{
					_displayedCaptions.RemoveAt(i);
					break;
				}
				else
				{
					shouldAdd = false;
					break;
				}
			}

			// Are we close enough that we should be grouped anyway?
			if (caption.Text == comp.Text)
			{
				if(!caption.IsRelative && !comp.IsRelative &&
					(caption.Position - comp.Position).Length() <= ClosedCaptionsModSystem.UserConfig.GroupingRange ||
					Math.Abs(caption.StartTime - comp.StartTime) <= ClosedCaptionsModSystem.UserConfig.GroupingMaxTime)
				{
					_displayedCaptions.RemoveAt(i);
					break;
				}
			}
		}

		if (shouldAdd)
		{
			_displayedCaptions.Add(caption);
			_needsRefresh = true;
		}
	}

	private void RebuildDisplayedCaptions()
	{
		_displayedCaptions.Clear();

		foreach (var caption in _captions.Values)
		{
			AddDisplayedCaption(caption);
		}
	}

	private void UpdateSoundsStatus()
	{
		foreach (var caption in _captions.Values)
		{
			if (!AL.IsSource(caption.ID))
			{
				RemoveCaption(caption);
				continue;
			}

			AL.GetSource(caption.ID, ALGetSourcei.SourceState, out var statei);

			var state = (ALSourceState)statei;
			if (state != ALSourceState.Playing)
			{
				RemoveCaption(caption);
				continue;
			}

			AL.GetSource(caption.ID, ALSourcef.Gain, out var value);
			caption.Volume = value;

			AL.GetSource(caption.ID, ALSource3f.Position, out var position);
			caption.Position.Set(position.X, position.Y, position.Z);
		}
	}

	// public static void SoundStarted(ILoadedSound loadedSound, AssetLocation location)
	// {
	// 	string? text = null;
	// 	CaptionTags tags = CaptionTags.None;
	// 	CaptionFlags flags = CaptionFlags.None;
	// 	CaptionGroup? group = null;
	// 	CaptionIcon? icon = null;
	// 	bool wasIgnored = false;

	// 	if (!Instance._matchConfig.FindCaptionForSound(location, ref wasIgnored, ref text, ref tags, ref flags, ref group, ref icon))
	// 	{
	// 		if (wasIgnored)
	// 			return;

	// 		Api.Logger.Warning("[Closed Captions] Unconfigured sound: " + location.ToString());
	// 		if (!ClosedCaptionsModSystem.UserConfig.ShowUnknown)
	// 			return;
	// 	}

	// 	if (text == null)
	// 		return;

	// 	if (Instance.IsFiltered(loadedSound, tags))
	// 		return;

	// 	// If this is a duplicate of a nearby sound, we'll ignore it entirely.
	// 	foreach (var caption in Instance._captions)
	// 	{
	// 		// Literally the same sound!
	// 		if (caption.LoadedSound == loadedSound)
	// 		{
	// 			Api.Logger.Warning("[Closed Captions] Duplicate sound played: " + location.ToString());
	// 			return;
	// 		}

	// 		if (caption.Text == text)
	// 		{
	// 			// Filter sounds that are very similar and close to sounds that are already playing.
	// 			var position = loadedSound.Params.Position ?? Api.World.Player.Entity.Pos.XYZFloat;
	// 			var distance = (position - caption.Params.Position).Length();
	// 			if (Api.ElapsedMilliseconds - caption.StartTime < ClosedCaptionsModSystem.UserConfig.GroupingMaxTime &&
	// 				distance < ClosedCaptionsModSystem.UserConfig.GroupingRange)
	// 			{
	// 				// Reset its timers and stuff.
	// 				caption.LoadedSound = loadedSound;
	// 				caption.StartTime = Api.ElapsedMilliseconds;
	// 				caption.FadeOutStartTime = 0;
	// 				caption.Position = position;
	// 				return;
	// 			}
	// 		}
	// 	}

	// 	Instance._captions.Add(new Caption(
	// 		Instance._nextCaptionId++,
	// 		loadedSound,
	// 		Api.ElapsedMilliseconds,
	// 		text,
	// 		tags,
	// 		flags,
	// 		group,
	// 		icon
	// 		));
	// 	Instance._needsRefresh = true;
	// }

	// public static IOrderedEnumerable<Caption> GetSortedCaptions()
	// {
	// 	Instance.UpdateCaptions();

	// 	var player = Api.World.Player;
	// 	// This has an error for a sound that is reverbing
	// 	// Also an error where sounds that are far enough away that they haven't displayed
	// 	// are still being displayed when they stop. This whole thing needs to be refactored!
	// 	var ordered = Instance._captions
	// 		// .Where(caption =>
	// 		// 	(!caption.IsLoadedSoundDisposed &&
	// 		// 	!caption.IsPaused &&
	// 		// 	caption.IsPlaying &&
	// 		// 	caption.Params.Volume > 0.1f &&
	// 		// 	(!caption.Params.RelativePosition &&
	// 		// 	(caption.Params.Position - player.Entity.Pos.XYZFloat).Length() < caption.Params.Range ||
	// 		// 	caption.Params.RelativePosition)) ||
	// 		// 	caption.IsFading)
	// 		.Where(caption =>
	// 		{
	// 			if (!caption.IsVisibile)
	// 				return false;

	// 			var distance = 0f;
	// 			if (!caption.Params.RelativePosition)
	// 				distance = (caption.Params.Position - player.Entity.Pos.XYZFloat).Length();
				
	// 			// We show disposed sounds...
	// 			if (caption.IsLoadedSoundDisposed ||
	// 				caption.IsFading)
	// 			{
	// 				// ...but only if they are fading _and_ were already visible.
	// 				// (TODO: second part of that)
	// 				if (caption.IsFading && distance < caption.Params.Range)
	// 				{
	// 					return true;
	// 				}

	// 				return false;
	// 			}

	// 			// Sometimes sounds will have stopped by the time we're asking
	// 			// for them, but *before* we've updated them in Tick().
	// 			// Maybe we should call Tick() at the start of this method.
	// 			// (Not exactly though, because it forces a refresh which
	// 			// will call in here, etc)
	// 			if (caption.IsPlaying &&
	// 				!caption.IsPaused &&
	// 				distance < caption.Params.Range)
	// 			{
	// 				return true;
	// 			}

	// 			return false;
	// 		})
	// 		.OrderBy(caption =>
	// 		{
	// 			if (caption.Params.Position == null)
	// 				return 0f;
				
	// 			var relativePosition = caption.Params.Position - player.Entity.Pos.XYZFloat;
	// 			if (caption.Params.RelativePosition)
	// 				relativePosition = caption.Params.Position;

	// 			return (float)-relativePosition.Length();
	// 		});

	// 	return ordered;
	// }

	// public static IEnumerable<Caption> GetCaptions()
	// {
	// 	return Instance._captions.AsReadOnly();
	// }

	// public void ForceRefresh()
	// {
	// 	// Rebuild caption list.
	// 	_captions.Clear();

	// 	foreach (var loadedSound in Api.GetActiveSounds())
	// 	{
	// 		SoundStarted(loadedSound, loadedSound.Params.Location);
	// 	}

	// 	_needsRefresh = true;
	// }

	// private void UpdateCaptions()
	// {
	// 	Dictionary<string, Caption> uniqueGroups = [];

	// 	var playerPos = Api.World.Player != null ? Api.World.Player.Entity.Pos.XYZFloat : Vec3f.Zero;
	// 	for (int i = _captions.Count - 1; i >= 0; --i)
	// 	{
	// 		var caption = _captions[i];
			
	// 		if (caption.Group != null &&
	// 			caption.IsPlaying &&
	// 			caption.Params.Volume > 0.05f)
	// 		{
	// 			if (uniqueGroups.TryGetValue(caption.Group.Name, out Caption? comp))
	// 			{
	// 				if (caption.Group.Priority > comp.Group!.Priority ||
	// 					caption.Group.Priority == comp.Group!.Priority &&
	// 					(caption.Position - playerPos).Length() < (comp.Position - playerPos).Length())
	// 				{
	// 					uniqueGroups[caption.Group.Name] = caption;
	// 					comp.IsVisibile = false;
	// 					caption.IsVisibile = true;
	// 				}
	// 				else
	// 				{
	// 					comp.IsVisibile = true;
	// 					caption.IsVisibile = false;
	// 				}
	// 			}
	// 			else
	// 			{
	// 				uniqueGroups[caption.Group.Name] = caption;
	// 				caption.IsVisibile = true;
	// 			}
	// 		}
	// 		else if (caption.Group != null)
	// 		{
	// 			caption.IsVisibile = false;
	// 		}

	// 		if (caption.IsLoadedSoundDisposed &&
	// 			!caption.IsDisposeFlagged)
	// 		{
	// 			// System says sound is disposed, so we need to let go
	// 			caption.FlagAsDisposed();

	// 			// Want the caption to show up for at least long enough to read
	// 			caption.FadeOutStartTime = Api.ElapsedMilliseconds;
	// 			if (caption.FadeOutStartTime < caption.StartTime + ClosedCaptionsModSystem.UserConfig.MinimumDisplayDuration)
	// 				caption.FadeOutStartTime = caption.StartTime + ClosedCaptionsModSystem.UserConfig.MinimumDisplayDuration;
	// 		}
	// 		else if (caption.IsFading)
	// 		{
	// 			if (Api.ElapsedMilliseconds - caption.FadeOutStartTime > ClosedCaptionsModSystem.UserConfig.FadeOutDuration)
	// 			{
	// 				_captions.RemoveAt(i);
	// 				_needsRefresh = true;
	// 			}
	// 		}
	// 	}
	// }

	// public void Tick()
	// {
	// 	UpdateCaptions();
	// 	if (_needsRefresh)
	// 		_overlay.Refresh();

	// 	_needsRefresh = false;
	// }

	// public void Dispose()
	// {
	// 	_captions.Clear();
	// }

	// private bool IsFiltered(ILoadedSound loadedSound, CaptionTags soundTags)
	// {
	// 	var assetName = loadedSound.Params.Location.ToString();

	// 	// If any one of these passes, do not filter. This supercedes cases such as, for example
	// 	// a nearby lightning strike being filtered due to ShowWeather, but should be shown because
	// 	// it is tagged as Danger. We still want to let the case fall through if ShowDanger is unchecked
	// 	// but ShowWeather is checked.
	// 	if (soundTags != CaptionTags.None)
	// 	{
	// 		if ((soundTags & CaptionTags.Ambience) != 0 && ClosedCaptionsModSystem.UserConfig.ShowAmbience ||
	// 			(soundTags & CaptionTags.Animal) != 0 && ClosedCaptionsModSystem.UserConfig.ShowAnimal ||
	// 			(soundTags & CaptionTags.Block) != 0 && ClosedCaptionsModSystem.UserConfig.ShowBlock ||
	// 			(soundTags & CaptionTags.Combat) != 0 && ClosedCaptionsModSystem.UserConfig.ShowCombat ||
	// 			(soundTags & CaptionTags.Danger) != 0 && ClosedCaptionsModSystem.UserConfig.ShowDanger ||
	// 			(soundTags & CaptionTags.Enemy) != 0 && ClosedCaptionsModSystem.UserConfig.ShowEnemy ||
	// 			(soundTags & CaptionTags.Environment) != 0 && ClosedCaptionsModSystem.UserConfig.ShowEnvironment ||
	// 			(soundTags & CaptionTags.Interaction) != 0 && ClosedCaptionsModSystem.UserConfig.ShowInteraction ||
	// 			(soundTags & CaptionTags.Temporal) != 0 && ClosedCaptionsModSystem.UserConfig.ShowTemporal ||
	// 			(soundTags & CaptionTags.Tool) != 0 && ClosedCaptionsModSystem.UserConfig.ShowTool ||
	// 			(soundTags & CaptionTags.Voice) != 0 && ClosedCaptionsModSystem.UserConfig.ShowVoice ||
	// 			(soundTags & CaptionTags.Walk) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWalk ||
	// 			(soundTags & CaptionTags.Wearable) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWearable ||
	// 			(soundTags & CaptionTags.Weather) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWeather)
	// 			return false;
	// 	}

	// 	// Check user filters.
	// 	if (!ClosedCaptionsModSystem.UserConfig.ShowWeather)
	// 	{
	// 		if (loadedSound.Params.SoundType == EnumSoundType.Weather)
	// 			return true;
	// 	}

	// 	if (!ClosedCaptionsModSystem.UserConfig.ShowWalk)
	// 	{
	// 		if (assetName.Contains("walk") ||
	// 			assetName.Contains("wearable"))
	// 		{
    //             return true;
    //         }
	// 	}

	// 	// It is a tagged sound and should not be shown.
	// 	if (soundTags != CaptionTags.None)
	// 		return true;

	// 	return false;
	// }
}