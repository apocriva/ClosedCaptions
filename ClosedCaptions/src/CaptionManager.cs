using System;
using System.Collections.Generic;
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

	public class Caption
	{
		public readonly long ID;
		public readonly SoundParams Params;
		public readonly string Text;
		public long StartTime;
		public long FadeOutStartTime;
		public readonly Tags Tags;
		public readonly string? IconType;
		public readonly string? IconCode;

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
		}

		public Caption(long id, ILoadedSound loadedSound, long startTime, string text, Tags tags, string? iconType = null, string? iconCode = null)
		{
			ID = id;
			LoadedSound = loadedSound;
			StartTime = startTime;
			Text = text;
			FadeOutStartTime = 0;
			Tags = tags;
			IconType = iconType;
			IconCode = iconCode;

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

	private static CaptionManager _instance;
	private static ICoreClientAPI _capi;

	private ClosedCaptionsOverlay _overlay;
	private MatchConfig _matchConfig;
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
			throw e;
		}

		_overlay = new(capi, _matchConfig);
	}

	public static void SoundStarted(ILoadedSound loadedSound, AssetLocation location)
	{
		//_capi.Logger.Log(EnumLogType.Debug, string.Format("StartPlaying: {0}", location));

		Tags tags = Tags.None;
		string? iconType = null;
		string? iconCode = null;
		var text = _instance._matchConfig.FindCaptionForSound(location, ref tags, ref iconType, ref iconCode);
		if (text == null)
			return;

		if (_instance.IsFiltered(loadedSound, tags))
			return;

		var duplicate = _instance.FindDuplicate(loadedSound, text);
		if (duplicate != null)
		{
			// Update with a new timestamp.
			duplicate.StartTime = _capi.ElapsedMilliseconds;
			duplicate.FadeOutStartTime = 0;
			duplicate.LoadedSound = loadedSound;
			_instance._needsRefresh = true;
			return;
		}

		if (string.IsNullOrEmpty(text))
			text = "[" + location.GetName() + "?]";

		_instance._captions.Add(new Caption(
			_instance._nextCaptionId++,
			loadedSound,
			_capi.ElapsedMilliseconds,
			text,
			tags,
			iconType,
			iconCode
			));
		_instance._needsRefresh = true;
	}

	public static IOrderedEnumerable<Caption> GetSortedCaptions()
	{
		var player = _capi.World.Player;
		// This has an error for a sound that is reverbing
		var ordered = _instance._captions
			.Where(caption =>
				(!caption.IsLoadedSoundDisposed &&
				!caption.IsPaused &&
				caption.IsPlaying &&
				caption.Params.Volume > 0.1f &&
				(!caption.Params.RelativePosition &&
				(caption.Params.Position - player.Entity.Pos.XYZFloat).Length() < caption.Params.Range ||
				caption.Params.RelativePosition)) ||
				caption.IsFading)
			.OrderBy(caption =>
			{
				if (caption.Params.Position == null)
					return 0f;
				
				var relativePosition = caption.Params.Position - player.Entity.Pos.XYZFloat;
				if (caption.Params.RelativePosition)
					relativePosition = caption.Params.Position;

				return -relativePosition.Length();
			});

		return ordered;
	}

	public static IEnumerable<Caption> GetCaptions()
	{
		return _instance._captions.AsReadOnly();
	}

	public void ForceRefresh()
	{
		// Rebuild caption list.
		_captions.Clear();

		foreach (var loadedSound in _capi.GetActiveSounds())
		{
			SoundStarted(loadedSound, loadedSound.Params.Location);
		}

		_needsRefresh = true;
	}

	public void Tick()
	{
		bool refresh = _needsRefresh;
		for (int i = _captions.Count - 1; i >= 0; --i)
		{
			var caption = _captions[i];
			if (caption.IsLoadedSoundDisposed &&
				!caption.IsDisposeFlagged)
			{
				// System says sound is disposed, so we need to let go
				caption.FlagAsDisposed();

				// Want the caption to show up for at least long enough to read
				caption.FadeOutStartTime = _capi.ElapsedMilliseconds;
				if (caption.FadeOutStartTime < caption.StartTime + ClosedCaptionsModSystem.UserConfig.MinimumDisplayDuration)
					caption.FadeOutStartTime = caption.StartTime + ClosedCaptionsModSystem.UserConfig.MinimumDisplayDuration;
			}
			else if (caption.IsFading)
			{
				if (_capi.ElapsedMilliseconds - caption.FadeOutStartTime > ClosedCaptionsModSystem.UserConfig.FadeOutDuration)
				{
					_captions.RemoveAt(i);
					refresh = true;
				}
			}
		}

		if (refresh)
			_overlay.Refresh();
		else
			_overlay.Tick();

		_needsRefresh = false;
	}

	public void Dispose()
	{
		_captions.Clear();
	}

	private Caption? FindDuplicate(ILoadedSound newSound, string newText)
	{
        // Filter out sounds that are similar to one that's playing and close in space.
        foreach (var caption in _captions)
		{
			// This literal sound is already playing, get outta here!
			if (caption.LoadedSound == newSound)
				return caption;

			// Filter sounds that are very similar and close to sounds that are already playing.
			var position = newSound.Params.Position ?? _capi.World.Player.Entity.Pos.XYZ.ToVec3f();
			var distance = (position - caption.Params.Position).Length();
			if (caption.Text == newText &&
				_capi.ElapsedMilliseconds - caption.StartTime < ClosedCaptionsModSystem.UserConfig.GroupingMaxTime &&
				distance < ClosedCaptionsModSystem.UserConfig.GroupingRange)
				return caption;
		}
		return null;
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
			if ((soundTags & Tags.Danger) != 0 && ClosedCaptionsModSystem.UserConfig.ShowDanger ||
				(soundTags & Tags.Animal) != 0 && ClosedCaptionsModSystem.UserConfig.ShowAnimal ||
				(soundTags & Tags.Enemy) != 0 && ClosedCaptionsModSystem.UserConfig.ShowEnemy)
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