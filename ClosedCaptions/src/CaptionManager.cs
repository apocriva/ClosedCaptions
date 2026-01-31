using System;
using System.Collections.Generic;
using System.Linq;
using ClosedCaptions.GUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ClosedCaptions;

public class CaptionManager
{
	public static readonly long MinimumDisplayDuration = 200;
	public static readonly long FadeOutDuration = 500;

	public class Caption
	{
		public readonly long ID;
		public readonly SoundParams Params;
		public readonly string Text;
		public readonly long StartTime;
		public long FadeOutStartTime;

		public ILoadedSound? LoadedSound { get; private set; }

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

		public Caption(long id, ILoadedSound loadedSound, long startTime, string text)
		{
			ID = id;
			LoadedSound = loadedSound;
			StartTime = startTime;
			Text = text;
			FadeOutStartTime = 0;

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
	private ClosedCaptionsConfig _config;
	private long _nextCaptionId = 1;
	private readonly List<Caption> _captions = [];

	private bool _needsRefresh = false;

	public CaptionManager(ICoreClientAPI capi)
	{
		_instance = this;
		_capi = capi;

        try
        {
            _config = capi.Assets.Get(new AssetLocation("closedcaptions", "config/closedcaptions.json")).ToObject<ClosedCaptionsConfig>();
        }
		catch (Exception e)
		{
            throw e;
        }

        _overlay = new(capi, _config);
	}

	public static void SoundStarted(ILoadedSound loadedSound, AssetLocation location)
	{
		//_capi.Logger.Log(EnumLogType.Debug, string.Format("StartPlaying: {0}", location));

		if (_instance.IsFiltered(loadedSound))
			return;

		var time = _capi.InWorldEllapsedMilliseconds;
		var text = _instance._config.FindCaptionForSound(location);
		if (string.IsNullOrEmpty(text))
			text = "[" + location.GetName() + "?]";

		_instance._captions.Add(new Caption(_instance._nextCaptionId++, loadedSound, time, text));
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
				caption.Params.Volume > 0.3f) ||
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
				caption.FadeOutStartTime = _capi.InWorldEllapsedMilliseconds;
				if (caption.FadeOutStartTime < caption.StartTime + MinimumDisplayDuration)
					caption.FadeOutStartTime = caption.StartTime + MinimumDisplayDuration;
			}
			else if (caption.IsFading)
			{
				if (_capi.InWorldEllapsedMilliseconds - caption.FadeOutStartTime > FadeOutDuration)
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

	private bool IsFiltered(ILoadedSound loadedSound)
	{
		var assetName = loadedSound.Params.Location.ToString();
		if (assetName.Contains("menubutton"))
			return true;

		// Filter out sounds 
		foreach (var caption in _captions)
		{
			// This literal sound is already playing, get outta here!
			if (caption.LoadedSound == loadedSound)
				return true;

            // Filter sounds that are very similar and close to sounds that are already playing.
            var position = loadedSound.Params.Position ?? Vec3f.Zero;
            var distance = (position - caption.Params.Position).Length();
			if (caption.Params.Location == loadedSound.Params.Location &&
				_capi.InWorldEllapsedMilliseconds - caption.StartTime > 250 &&
				distance < 5f)
				return true;
		}

		return false;
	}
}