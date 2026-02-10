using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ClosedCaptions.Config;
using ClosedCaptions.Extensions;
using ClosedCaptions.GUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ClosedCaptions;

public class CaptionManager
{
	[Flags]
	public enum Tags
	{
		None		= 0,
		Ambience	= 1 << 0,
		Animal		= 1 << 1,
		Block		= 1 << 2,
		Combat		= 1 << 3,
		Danger		= 1 << 4,
		Enemy		= 1 << 5,
		Environment	= 1 << 6,
		Interaction = 1 << 7,
		Temporal	= 1 << 8,
		Tool		= 1 << 9,
		Voice		= 1 << 10,
		Walk		= 1 << 11,
		Wearable	= 1 << 12,
		Weather		= 1 << 13,
	}

	[Flags]
	public enum Flags
	{
		None			= 0,
		Directionless	= 1 << 0,
	}

	public class Caption
	{
		public bool IsVisibile = true;
		public readonly long ID;
		public readonly SoundParams Params;
		public readonly string Text;
		public long StartTime;
		public long FadeOutStartTime;
		public readonly Tags Tags;
		public readonly Flags Flags;
		public readonly MatchConfig.Unique? Unique;
		public readonly MatchConfig.Icon? Icon;

		public ILoadedSound? LoadedSound { get; set; }

		// Params.Position can be null, why?
		public Vec3f Position
		{
			get
			{
				if (Params.Position != null)
					return Params.Position;

				return Vec3f.Zero;
			}

			set
			{
				if (Params.Position != null)
					Params.Position = value;
			}
		}

		public Caption(
			long id, ILoadedSound loadedSound,
			long startTime, string text,
			Tags tags, Flags flags,
			MatchConfig.Unique? unique = null,
			MatchConfig.Icon? icon = null)
		{
			ID = id;
			LoadedSound = loadedSound;
			StartTime = startTime;
			Text = text;
			FadeOutStartTime = 0;
			Tags = tags;
			Flags = flags;
			Unique = unique;
			Icon = icon;

			var p = LoadedSound.Params;
			Params = new()
			{
				Location = p.Location,
				Position = p.Position ?? Vec3f.Zero,
				RelativePosition = p.RelativePosition,
				ShouldLoop = p.ShouldLoop,
				DisposeOnFinish = p.DisposeOnFinish,
				Pitch = p.Pitch,
				LowPassFilter = p.LowPassFilter,
				ReferenceDistance = p.ReferenceDistance,
				Range = p.Range,
				SoundType = p.SoundType,
				Volume = p.Volume
			};
		}

		public bool IsDisposeFlagged => LoadedSound == null;
		public bool IsLoadedSoundDisposed => LoadedSound != null && LoadedSound.IsDisposed;
		public bool IsPlaying => LoadedSound != null && LoadedSound.IsPlaying;
		public bool IsPaused => LoadedSound != null && LoadedSound.IsPaused;
		public bool IsFading => FadeOutStartTime > 0;

		public void FlagAsDisposed()
		{
			LoadedSound = null;
		}
	}

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
	private static ICoreClientAPI API
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
	private long _nextCaptionId = 1;
	private readonly List<Caption> _captions = [];

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

	public static void SoundStarted(ILoadedSound loadedSound, AssetLocation location)
	{
		string? text = null;
		Tags tags = Tags.None;
		Flags flags = Flags.None;
		MatchConfig.Unique? unique = null;
		MatchConfig.Icon? icon = null;
		bool wasIgnored = false;

		if (!Instance._matchConfig.FindCaptionForSound(location, ref wasIgnored, ref text, ref tags, ref flags, ref unique, ref icon))
		{
			if (wasIgnored)
				return;

			API.Logger.Warning("[Closed Captions] Unconfigured sound: " + location.ToString());
			if (!ClosedCaptionsModSystem.UserConfig.ShowUnknown)
				return;
		}

		if (text == null)
			return;

		if (Instance.IsFiltered(loadedSound, tags))
			return;

		// If this is a duplicate of a nearby sound, we'll ignore it entirely.
		foreach (var caption in Instance._captions)
		{
			// Literally the same sound!
			if (caption.LoadedSound == loadedSound)
			{
				API.Logger.Warning("[Closed Captions] Duplicate sound played: " + location.ToString());
				return;
			}

			if (caption.Text == text)
			{
				// Filter sounds that are very similar and close to sounds that are already playing.
				var position = loadedSound.Params.Position ?? API.World.Player.Entity.Pos.XYZFloat;
				var distance = (position - caption.Params.Position).Length();
				if (API.ElapsedMilliseconds - caption.StartTime < ClosedCaptionsModSystem.UserConfig.GroupingMaxTime &&
					distance < ClosedCaptionsModSystem.UserConfig.GroupingRange)
				{
					// Reset its timers and stuff.
					caption.LoadedSound = loadedSound;
					caption.StartTime = API.ElapsedMilliseconds;
					caption.FadeOutStartTime = 0;
					caption.Position = position;
					return;
				}
			}
		}

		Instance._captions.Add(new Caption(
			Instance._nextCaptionId++,
			loadedSound,
			API.ElapsedMilliseconds,
			text,
			tags,
			flags,
			unique,
			icon
			));
		Instance._needsRefresh = true;
	}

	public static IOrderedEnumerable<Caption> GetSortedCaptions()
	{
		Instance.UpdateCaptions();

		var player = API.World.Player;
		// This has an error for a sound that is reverbing
		// Also an error where sounds that are far enough away that they haven't displayed
		// are still being displayed when they stop. This whole thing needs to be refactored!
		var ordered = Instance._captions
			// .Where(caption =>
			// 	(!caption.IsLoadedSoundDisposed &&
			// 	!caption.IsPaused &&
			// 	caption.IsPlaying &&
			// 	caption.Params.Volume > 0.1f &&
			// 	(!caption.Params.RelativePosition &&
			// 	(caption.Params.Position - player.Entity.Pos.XYZFloat).Length() < caption.Params.Range ||
			// 	caption.Params.RelativePosition)) ||
			// 	caption.IsFading)
			.Where(caption =>
			{
				if (!caption.IsVisibile)
					return false;

				var distance = 0f;
				if (!caption.Params.RelativePosition)
					distance = (caption.Params.Position - player.Entity.Pos.XYZFloat).Length();
				
				// We show disposed sounds...
				if (caption.IsLoadedSoundDisposed ||
					caption.IsFading)
				{
					// ...but only if they are fading _and_ were already visible.
					// (TODO: second part of that)
					if (caption.IsFading && distance < caption.Params.Range)
					{
						return true;
					}

					return false;
				}

				// Sometimes sounds will have stopped by the time we're asking
				// for them, but *before* we've updated them in Tick().
				// Maybe we should call Tick() at the start of this method.
				// (Not exactly though, because it forces a refresh which
				// will call in here, etc)
				if (caption.IsPlaying &&
					!caption.IsPaused &&
					distance < caption.Params.Range)
				{
					return true;
				}

				return false;
			})
			.OrderBy(caption =>
			{
				if (caption.Params.Position == null)
					return 0f;
				
				var relativePosition = caption.Params.Position - player.Entity.Pos.XYZFloat;
				if (caption.Params.RelativePosition)
					relativePosition = caption.Params.Position;

				return (float)-relativePosition.Length();
			});

		return ordered;
	}

	public static IEnumerable<Caption> GetCaptions()
	{
		return Instance._captions.AsReadOnly();
	}

	public void ForceRefresh()
	{
		// Rebuild caption list.
		_captions.Clear();

		foreach (var loadedSound in API.GetActiveSounds())
		{
			SoundStarted(loadedSound, loadedSound.Params.Location);
		}

		_needsRefresh = true;
	}

	private void UpdateCaptions()
	{
		Dictionary<string, Caption> uniqueGroups = [];

		var playerPos = API.World.Player != null ? API.World.Player.Entity.Pos.XYZFloat : Vec3f.Zero;
		for (int i = _captions.Count - 1; i >= 0; --i)
		{
			var caption = _captions[i];
			
			if (caption.Unique != null &&
				caption.IsPlaying &&
				caption.Params.Volume > 0.05f)
			{
				if (uniqueGroups.TryGetValue(caption.Unique.Group, out Caption? comp))
				{
					if (caption.Unique.Priority > comp.Unique!.Priority ||
						caption.Unique.Priority == comp.Unique!.Priority &&
						(caption.Position - playerPos).Length() < (comp.Position - playerPos).Length())
					{
						uniqueGroups[caption.Unique.Group] = caption;
						comp.IsVisibile = false;
						caption.IsVisibile = true;
					}
					else
					{
						comp.IsVisibile = true;
						caption.IsVisibile = false;
					}
				}
				else
				{
					uniqueGroups[caption.Unique.Group] = caption;
					caption.IsVisibile = true;
				}
			}
			else if (caption.Unique != null)
			{
				caption.IsVisibile = false;
			}

			if (caption.IsLoadedSoundDisposed &&
				!caption.IsDisposeFlagged)
			{
				// System says sound is disposed, so we need to let go
				caption.FlagAsDisposed();

				// Want the caption to show up for at least long enough to read
				caption.FadeOutStartTime = API.ElapsedMilliseconds;
				if (caption.FadeOutStartTime < caption.StartTime + ClosedCaptionsModSystem.UserConfig.MinimumDisplayDuration)
					caption.FadeOutStartTime = caption.StartTime + ClosedCaptionsModSystem.UserConfig.MinimumDisplayDuration;
			}
			else if (caption.IsFading)
			{
				if (API.ElapsedMilliseconds - caption.FadeOutStartTime > ClosedCaptionsModSystem.UserConfig.FadeOutDuration)
				{
					_captions.RemoveAt(i);
					_needsRefresh = true;
				}
			}
		}
	}

	public void Tick()
	{
		UpdateCaptions();
		if (_needsRefresh)
			_overlay.Refresh();

		_needsRefresh = false;
	}

	public void Dispose()
	{
		_captions.Clear();
	}

	private bool IsFiltered(ILoadedSound loadedSound, Tags soundTags)
	{
		var assetName = loadedSound.Params.Location.ToString();

		// If any one of these passes, do not filter. This supercedes cases such as, for example
		// a nearby lightning strike being filtered due to ShowWeather, but should be shown because
		// it is tagged as Danger. We still want to let the case fall through if ShowDanger is unchecked
		// but ShowWeather is checked.
		if (soundTags != Tags.None)
		{
			if ((soundTags & Tags.Ambience) != 0 && ClosedCaptionsModSystem.UserConfig.ShowAmbience ||
				(soundTags & Tags.Animal) != 0 && ClosedCaptionsModSystem.UserConfig.ShowAnimal ||
				(soundTags & Tags.Block) != 0 && ClosedCaptionsModSystem.UserConfig.ShowBlock ||
				(soundTags & Tags.Combat) != 0 && ClosedCaptionsModSystem.UserConfig.ShowCombat ||
				(soundTags & Tags.Danger) != 0 && ClosedCaptionsModSystem.UserConfig.ShowDanger ||
				(soundTags & Tags.Enemy) != 0 && ClosedCaptionsModSystem.UserConfig.ShowEnemy ||
				(soundTags & Tags.Environment) != 0 && ClosedCaptionsModSystem.UserConfig.ShowEnvironment ||
				(soundTags & Tags.Interaction) != 0 && ClosedCaptionsModSystem.UserConfig.ShowInteraction ||
				(soundTags & Tags.Temporal) != 0 && ClosedCaptionsModSystem.UserConfig.ShowTemporal ||
				(soundTags & Tags.Tool) != 0 && ClosedCaptionsModSystem.UserConfig.ShowTool ||
				(soundTags & Tags.Voice) != 0 && ClosedCaptionsModSystem.UserConfig.ShowVoice ||
				(soundTags & Tags.Walk) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWalk ||
				(soundTags & Tags.Wearable) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWearable ||
				(soundTags & Tags.Weather) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWeather)
				return false;
		}

		// Check user filters.
		if (!ClosedCaptionsModSystem.UserConfig.ShowWeather)
		{
			if (loadedSound.Params.SoundType == EnumSoundType.Weather)
				return true;
		}

		if (!ClosedCaptionsModSystem.UserConfig.ShowWalk)
		{
			if (assetName.Contains("walk") ||
				assetName.Contains("wearable"))
			{
                return true;
            }
		}

		// It is a tagged sound and should not be shown.
		if (soundTags != Tags.None)
			return true;

		return false;
	}
}