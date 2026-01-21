using ClosedCaptions.GUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Common;

namespace ClosedCaptions;

public class ClosedCaptionsModSystem : ModSystem
{
	private ClosedCaptionsOverlay _overlay;
	private SoundLabelMap _soundLabelMap;

	private ICoreClientAPI _capi;

	private long _gameTickListenerId;

	public override void StartClientSide(ICoreClientAPI api)
	{
		_capi = api;
		base.StartClientSide(_capi);

		InitSoundLabelMap();
		_overlay = new(_capi, _soundLabelMap);
		_overlay.TryOpen(withFocus: false);
		_gameTickListenerId = _capi.Event.RegisterGameTickListener(OnTick, 500);
	}

	private void OnTick(float dt)
	{
		_overlay.Update();
	}

	private void InitSoundLabelMap()
	{
		_soundLabelMap = new();

		_soundLabelMap.AddMapping(MatchRockslide);
		_soundLabelMap.AddMapping(MatchDrifter);
		_soundLabelMap.AddMapping(MatchBowtorn);
		_soundLabelMap.AddMapping(MatchWalk);
		_soundLabelMap.AddMapping(MatchVoice);
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
