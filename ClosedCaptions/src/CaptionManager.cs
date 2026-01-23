using System;
using System.Collections.Generic;
using System.Linq;
using ClosedCaptions.GUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ClosedCaptions;

public class CaptionManager
{
	public class Caption(long id, ILoadedSound loadedSound, long startTime, string text)
	{
		public readonly long ID = id;
		public readonly ILoadedSound LoadedSound = loadedSound;
		public readonly long StartTime = startTime;
		public readonly string Text = text;
	}

	private static CaptionManager _instance;
	private static ICoreClientAPI _capi;

	private ClosedCaptionsOverlay _overlay;
	private SoundLabelMap _soundLabelMap;
	private long _nextCaptionId = 1;
	private readonly List<Caption> _captions = [];

	private bool _needsRefresh = false;

	public CaptionManager(ICoreClientAPI capi)
	{
		_instance = this;
		_capi = capi;

		_soundLabelMap = new();
		InitSoundLabelMap();

		_overlay = new(capi, _soundLabelMap);
	}

	public static void SoundStarted(ILoadedSound loadedSound, AssetLocation location)
	{
		//_capi.Logger.Log(EnumLogType.Debug, string.Format("StartPlaying: {0}", location));

		if (_instance.IsFiltered(loadedSound))
			return;

		var time = _capi.World.ElapsedMilliseconds;
		var text = _instance._soundLabelMap.FindCaptionForSound(location);
		if (string.IsNullOrEmpty(text))
			text = "[" + location.GetName() + "?]";

		_instance._captions.Add(new Caption(_instance._nextCaptionId++, loadedSound, time, text));
		_instance._needsRefresh = true;
	}

	public static IOrderedEnumerable<Caption> GetSortedCaptions()
	{
		var player = _capi.World.Player;
		var ordered = _instance._captions
			.Where(caption =>
				!caption.LoadedSound.IsPaused &&
				caption.LoadedSound.IsPlaying &&
				caption.LoadedSound.Params?.Volume > 0.3f)
			.OrderBy(caption =>
			{
				var sound = caption.LoadedSound;
				if (sound.Params.Position == null)
					return 0f;
				
				var relativePosition = sound.Params.Position - player.Entity.Pos.XYZFloat;
				if (sound.Params.RelativePosition)
					relativePosition = sound.Params.Position;

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
			var loadedSound = _captions[i].LoadedSound;
			if (loadedSound.IsDisposed &&
				_capi.World.ElapsedMilliseconds - _captions[i].StartTime > 1000)
			{
				var location = loadedSound.Params?.Location;
				//_capi.Logger.Log(EnumLogType.Debug, string.Format("Sound disposed: {0}", location));
				_captions.RemoveAt(i);
				refresh = true;
			}
		}

		if (refresh)
			_overlay.Refresh();
		else
			_overlay.Tick();
	}

	public void Dispose()
	{
		_captions.Clear();
		_soundLabelMap.Dispose();
	}

	private bool IsFiltered(ILoadedSound loadedSound)
	{
		if (loadedSound.Params.Location.ToString().Contains("menubutton"))
			return true;

		// Filter sounds that are very similar and close to sounds that are already playing.
		foreach (var caption in _captions)
		{
			if (caption.LoadedSound == loadedSound)
				return true;
			if (caption.LoadedSound.Params.Location != loadedSound.Params.Location)
				continue;

			// Close enough in time?
			if (_capi.World.ElapsedMilliseconds - caption.StartTime > 250)
				continue;
			
			// Close enough in space?
			var distance = (loadedSound.Params.Position - caption.LoadedSound.Params.Position).Length();
			if (distance > 5f)
				continue;

			// Treat as duplicate!
			return true;
		}

		return false;
	}

	private void InitSoundLabelMap()
	{
		_soundLabelMap = new();

		_soundLabelMap.AddMapping(MatchBlock);
		_soundLabelMap.AddMapping(MatchCreature);
		_soundLabelMap.AddMapping(MatchRockslide);
		_soundLabelMap.AddMapping(MatchWalk);
		_soundLabelMap.AddMapping(MatchWearable);
		_soundLabelMap.AddMapping(MatchVoice);
	}

	private static string? MatchBlock(string assetName)
	{
		if (!assetName.Contains("block"))
			return  null;
		
		return Lang.Get("closedcaptions:block");
	}

	private static string? MatchCreature(string assetName)
	{
		if (!assetName.Contains("creature"))
			return  null;
		
		string? ret = null;
		if ((ret = MatchDrifter(assetName)) != null)
			return ret;
		else if ((ret = MatchBowtorn(assetName)) != null)
			return ret;
		
		return Lang.Get("closedcaptions:creature");
	}

	private static string? MatchRockslide(string assetName)
	{
		if (!assetName.Contains("effect/rockslide"))
			return null;

		return Lang.Get("closedcaptions:rockslide");
	}

	private static string? MatchDrifter(string assetName)
	{
		if (!assetName.Contains("drifter"))
			return null;
		
		if (assetName.Contains("hurt"))
			return Lang.Get("closedcaptions:drifter-hurt");
		else if (assetName.Contains("death"))
			return Lang.Get("closedcaptions:drifter-death");
		
		return Lang.Get("closedcaptions:drifter");
	}

	private static string? MatchBowtorn(string assetName)
	{
		if (!assetName.Contains("bowtorn"))
			return null;
		
		if (assetName.Contains("hurt"))
			return Lang.Get("closedcaptions:bowtorn-hurt");
		else if (assetName.Contains("death"))
			return Lang.Get("closedcaptions:bowtorn-death");
		else if (assetName.Contains("draw"))
			return Lang.Get("closedcaptions:bowtorn-draw");
		else if (assetName.Contains("reload"))
			return Lang.Get("closedcaptions:bowtorn-reload");
		
		return Lang.Get("closedcaptions:bowtorn");
	}

	private static string? MatchWalk(string assetName)
	{
		if (!assetName.Contains("walk"))
			return null;

		if (assetName.Contains("cloth"))
			return Lang.Get("closedcaptions:walk-cloth");
		else if (assetName.Contains("grass"))
			return Lang.Get("closedcaptions:walk-grass");
		else if (assetName.Contains("gravel"))
			return Lang.Get("closedcaptions:walk-gravel");
		else if (assetName.Contains("sand"))
			return Lang.Get("closedcaptions:walk-sand");
		else if (assetName.Contains("sludge"))
			return Lang.Get("closedcaptions:walk-sludge");
		else if (assetName.Contains("stone"))
			return Lang.Get("closedcaptions:walk-stone");

		return Lang.Get("closedcaptions:walk");
	}

	private static string? MatchWearable(string assetName)
	{
		if (!assetName.Contains("wearable"))
			return null;

		if (assetName.Contains("brigandine"))
			return Lang.Get("closedcaptions:wearable-brigandine");
		else if (assetName.Contains("chain"))
			return Lang.Get("closedcaptions:wearable-chain");
		else if (assetName.Contains("leather"))
			return Lang.Get("closedcaptions:wearable-leather");
		else if (assetName.Contains("plate"))
			return Lang.Get("closedcaptions:wearable-plate");
		else if (assetName.Contains("scale"))
			return Lang.Get("closedcaptions:wearable-scale");

		return Lang.Get("closedcaptions:wearable");
	}

	private static string? MatchVoice(string assetName)
	{
		if (!assetName.Contains("voice"))
			return null;

		if (assetName.Contains("accordion"))
			return Lang.Get("closedcaptions:voice-accordion");
		else if (assetName.Contains("altoflute"))
			return Lang.Get("closedcaptions:voice-altoflute");
		else if (assetName.Contains("clarinet"))
			return Lang.Get("closedcaptions:voice-clarinet");
		else if (assetName.Contains("dukduk"))
			return Lang.Get("closedcaptions:voice-dukduk");
		else if (assetName.Contains("altoharmonica"))
			return Lang.Get("closedcaptions:voice-harmonica");
		else if (assetName.Contains("harp"))
			return Lang.Get("closedcaptions:voice-harp");
		else if (assetName.Contains("harpsichord"))
			return Lang.Get("closedcaptions:voice-harpsichord");
		else if (assetName.Contains("oboe"))
			return Lang.Get("closedcaptions:voice-oboe");
		else if (assetName.Contains("ocarina"))
			return Lang.Get("closedcaptions:voice-ocarina");
		else if (assetName.Contains("sax"))
			return Lang.Get("closedcaptions:voice-sax");
		else if (assetName.Contains("trumpet"))
			return Lang.Get("closedcaptions:voice-trumpet");
		else if (assetName.Contains("tuba"))
			return Lang.Get("closedcaptions:voice-tuba");

		return Lang.Get("closedcaptions:voice");
	}
}