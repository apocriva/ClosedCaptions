using System;
using System.Numerics;
using System.Threading;
using ClosedCaptions.Config;
using ClosedCaptions.GUI;
using ConfigLib;
using HarmonyLib;
using ImGuiNET;
using Microsoft.VisualBasic;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace ClosedCaptions;

public class ClosedCaptionsModSystem : ModSystem
{
	public static UserConfig UserConfig = new();

	private Harmony? patcher;
	private ICoreClientAPI? _capi;

	private CaptionManager? _manager;

	private CancellationTokenSource _cancelSource = new();

	private string[] _captionAnchorStrings = [];
	private string[] _directionIndicatorsStrings = [];
	private string[] _iconStrings = [];
	private string[] _showMusicStrings = [];

	public override void StartPre(ICoreAPI api)
	{
		base.StartPre(api);

		UserConfig = api.LoadModConfig<UserConfig>(UserConfig.Filename) ?? new UserConfig();
	}

	public override void StartClientSide(ICoreClientAPI api)
	{
		_capi = api;
		base.StartClientSide(_capi);
		
		InitConfigLuts();

		if (!Harmony.HasAnyPatches(Mod.Info.ModID))
		{
			patcher = new Harmony(Mod.Info.ModID);
			patcher.PatchCategory(Mod.Info.ModID);
		}

		_manager = new(_capi);

		if (api.ModLoader.IsModEnabled("configlib"))
		{
			var configlib = api.ModLoader.GetModSystem<ConfigLibModSystem>();
			configlib.RegisterCustomConfig(Lang.Get("closedcaptions:config"), (id, buttons) => EditConfig(buttons, api));
		}

		_cancelSource = new CancellationTokenSource();
		new Thread(() =>
		{
			while (true)
			{
				Thread.Sleep(60);
				if (_cancelSource.IsCancellationRequested)
					return;
				_capi.Event.EnqueueMainThreadTask(_manager.Tick, "closedcaptiontick");
			}
		}).Start();
	}

	public override void Dispose()
	{
		_cancelSource?.Cancel();

		_manager?.Dispose();
		patcher?.UnpatchAll(patcher.Id);
	}

	private void EditConfig(ControlButtons buttons, ICoreAPI api)
	{
		if (buttons.Save)
			api.StoreModConfig(UserConfig, UserConfig.Filename);
		if (buttons.Restore)
			UserConfig = api.LoadModConfig<UserConfig>(UserConfig.Filename);
		if (buttons.Defaults)
			UserConfig = new();

		if (UserConfig != null)
		{
			BuildSettings(UserConfig);
		}
	}

	private void InitConfigLuts()
	{
		_captionAnchorStrings = [
			Lang.Get("closedcaptions:config-anchor-lefttop"),
			Lang.Get("closedcaptions:config-anchor-centertop"),
			Lang.Get("closedcaptions:config-anchor-righttop"),
			Lang.Get("closedcaptions:config-anchor-left"),
			Lang.Get("closedcaptions:config-anchor-center"),
			Lang.Get("closedcaptions:config-anchor-right"),
			Lang.Get("closedcaptions:config-anchor-leftbottom"),
			Lang.Get("closedcaptions:config-anchor-centerbottom"),
			Lang.Get("closedcaptions:config-anchor-rightbottom")
		];
		_directionIndicatorsStrings = [
			Lang.Get("closedcaptions:config-direction-indicators-none"),
			Lang.Get("closedcaptions:config-direction-indicators-left"),
			Lang.Get("closedcaptions:config-direction-indicators-right"),
			Lang.Get("closedcaptions:config-direction-indicators-both"),
		];
		_iconStrings = [
			Lang.Get("closedcaptions:config-direction-indicators-none"),
			Lang.Get("closedcaptions:config-direction-indicators-left"),
			Lang.Get("closedcaptions:config-direction-indicators-right"),
		];
		_showMusicStrings = [
			Lang.Get("closedcaptions:config-music-none"),
			Lang.Get("closedcaptions:config-music-onlyevent"),
			Lang.Get("closedcaptions:config-music-all"),
		];
	}

	private void BuildSettings(UserConfig config)
	{
		var modified = false;

		if (ImGui.CollapsingHeader(Lang.Get("closedcaptions:config-filters-header")))
		{
			ImGui.Indent();
			ImGui.TextWrapped(Lang.Get("closedcaptions:config-filters-description"));
			if (ImGui.Button(Lang.Get("closedcaptions:config-check-all")))
			{
				modified = true;
				config.ShowAmbience = true;
				config.ShowAnimal = true;
				config.ShowBlock = true;
				config.ShowCombat = true;
				config.ShowDanger = true;
				config.ShowEnemy = true;
				config.ShowEnvironment = true;
				config.ShowInteraction = true;
				config.ShowMachinery = true;
				config.ShowRust = true;
				config.ShowTemporal = true;
				config.ShowTool = true;
				config.ShowVoice = true;
				config.ShowWalk = true;
				config.ShowWearable = true;
				config.ShowWeather = true;

				config.ShowMusic = MusicOption.None;
			}
			if (ImGui.Button(Lang.Get("closedcaptions:config-uncheck-all")))
			{
				modified = true;
				config.ShowAmbience = false;
				config.ShowAnimal = false;
				config.ShowBlock = false;
				config.ShowCombat = false;
				config.ShowDanger = false;
				config.ShowEnemy = false;
				config.ShowEnvironment = false;
				config.ShowInteraction = false;
				config.ShowMachinery = true;
				config.ShowRust = false;
				config.ShowTemporal = true;
				config.ShowTool = false;
				config.ShowVoice = false;
				config.ShowWalk = false;
				config.ShowWearable = false;
				config.ShowWeather = false;
				config.ShowUnknown = false;

				config.ShowMusic = MusicOption.All;
			}
			config.ShowAmbience = OnCheckBox("show-ambience", config.ShowAmbience, ref modified);
			config.ShowAnimal = OnCheckBox("show-animal", config.ShowAnimal, ref modified);
			config.ShowBlock = OnCheckBox("show-block", config.ShowBlock, ref modified);
			config.ShowCombat = OnCheckBox("show-combat", config.ShowCombat, ref modified);
			config.ShowDanger = OnCheckBox("show-danger", config.ShowDanger, ref modified);
			config.ShowEnemy = OnCheckBox("show-enemy", config.ShowEnemy, ref modified);
			config.ShowEnvironment = OnCheckBox("show-environment", config.ShowEnvironment, ref modified);
			config.ShowInteraction = OnCheckBox("show-interaction", config.ShowInteraction, ref modified);
			config.ShowMachinery = OnCheckBox("show-machinery", config.ShowMachinery, ref modified);
			config.ShowRust = OnCheckBox("show-rust", config.ShowRust, ref modified);
			config.ShowTemporal = OnCheckBox("show-temporal", config.ShowTemporal, ref modified);
			config.ShowTool = OnCheckBox("show-tool", config.ShowTool, ref modified);
			config.ShowVoice = OnCheckBox("show-voice", config.ShowVoice, ref modified);
			config.ShowWalk = OnCheckBox("show-walk", config.ShowWalk, ref modified);
			config.ShowWearable = OnCheckBox("show-wearable", config.ShowWearable, ref modified);
			config.ShowWeather = OnCheckBox("show-weather", config.ShowWeather, ref modified);
			config.ShowUnknown = OnCheckBox("show-unknown", config.ShowUnknown, ref modified);
			ImGui.NewLine();
			config.ShowMusic = (MusicOption)OnDropdown("show-music", (int)config.ShowMusic, _showMusicStrings, ref modified);
			ImGui.Unindent();
		}

		if (ImGui.CollapsingHeader(Lang.Get("closedcaptions:config-behavior-header")))
		{
			ImGui.Indent();
			config.MinimumDirectionDistance = OnInputFloat("minimum-direction-distance", (int)config.MinimumDirectionDistance, ref modified, 0f);
			config.MinimumDisplayDuration = OnInputInt("minimum-display-duration", (int)config.MinimumDisplayDuration, ref modified, 0);
			config.DimTime = OnInputInt("dim-time", (int)config.DimTime, ref modified, 0);
			config.DimPercent = OnInputInt("dim-percent", (int)(config.DimPercent * 100), ref modified, 0, 100) / 100f;
			config.FadeOutDuration = OnInputInt("fade-out-duration", (int)config.FadeOutDuration, ref modified, 1);
			config.MinimumAttenuationOpacity = OnInputInt("min-attenuation-opacity", (int)(config.MinimumAttenuationOpacity * 100), ref modified, 0, 100) / 100f;
			config.GroupingRange = OnInputInt("grouping-range", config.GroupingRange, ref modified, 0);
			config.GroupingMaxTime = OnInputInt("grouping-max-time", config.GroupingMaxTime, ref modified, 0);
			ImGui.Unindent();
		}


		if (ImGui.CollapsingHeader(Lang.Get("closedcaptions:config-display-header")))
		{
			ImGui.Indent();
			config.ScreenAnchor = (CaptionAnchor)OnDropdown("screen-anchor", (int)config.ScreenAnchor, _captionAnchorStrings, ref modified);
			config.CaptionAnchor = (CaptionAnchor)OnDropdown("caption-align", (int)config.CaptionAnchor, _captionAnchorStrings, ref modified);
			config.DisplayOffset = OnVec2i("display-offset", config.DisplayOffset, ref modified);
			config.FontSize = OnInputInt("font-size", config.FontSize, ref modified, 6, 100);
			config.CaptionBackgroundOpacity = OnInputInt("caption-opacity", (int)(config.CaptionBackgroundOpacity * 100f), ref modified, 0, 100) / 100f;
			config.CaptionPaddingH = OnInputInt("caption-padding-h", config.CaptionPaddingH, ref modified, 0);
			config.CaptionPaddingV = OnInputInt("caption-padding-v", config.CaptionPaddingV, ref modified, 0);
			config.CaptionSpacing = OnInputInt("caption-spacing", config.CaptionSpacing, ref modified, 0);
			config.DirectionIndicators = (CaptionDirectionIndicators)OnDropdown("direction-indicators", (int)config.DirectionIndicators, _directionIndicatorsStrings, ref modified);
			config.Icon = (CaptionIconIndicator)OnDropdown("show-icons", (int)config.Icon, _iconStrings, ref modified);
			config.Color = OnColor("color", config.Color, ref modified);
			config.DangerColor = OnColor("danger-color", config.DangerColor, ref modified);
			config.DangerBold = OnCheckBox("danger-bold", config.DangerBold, ref modified);
			config.RustColor = OnColor("rust-color", config.RustColor, ref modified);
			config.TemporalColor = OnColor("temporal-color", config.TemporalColor, ref modified);
			config.PassiveColor = OnColor("passive-color", config.PassiveColor, ref modified);
			config.PassiveItalic = OnCheckBox("passive-italic", config.PassiveItalic, ref modified);
			config.MusicColor = OnColor("music-color", config.MusicColor, ref modified);
			config.ShowGlitch = OnCheckBox("show-glitch", config.ShowGlitch, ref modified);
			ImGui.Unindent();
		}

		config.DebugMode = OnCheckBox("debug-mode", config.DebugMode, ref modified);

		if (modified)
			_manager?.ForceRefresh();
	}

	private void Tooltip(string option)
	{
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
		{
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
			ImGui.TextUnformatted(Lang.Get($"closedcaptions:config-{option}-tooltip"));
			ImGui.EndTooltip();
		}
	}

	private bool OnCheckBox(string option, bool value, ref bool modified)
	{
		bool newValue = value;
		ImGui.Checkbox(Lang.Get("closedcaptions:config-" + option), ref newValue);
		Tooltip(option);
		modified |= newValue != value;
		return newValue;
	}

	private float OnInputFloat(string option, float value, ref bool modified, float min = float.MinValue, float max = float.MaxValue)
	{
		float newValue = value;
		ImGui.InputFloat(Lang.Get("closedcaptions:config-" + option), ref newValue);
		Tooltip(option);
		newValue = Math.Max(min, Math.Min(newValue, max));
		modified |= newValue != value;
		return newValue;
	}

	private int OnInputInt(string option, int value, ref bool modified, int min = int.MinValue, int max = int.MaxValue)
	{
		int newValue = value;
		ImGui.InputInt(Lang.Get("closedcaptions:config-" + option), ref newValue);
		Tooltip(option);
		newValue = Math.Max(min, Math.Min(newValue, max));
		modified |= newValue != value;
		return newValue;
	}

	private Vec2i OnVec2i(string option, Vec2i value, ref bool modified)
	{
		Vec2i newValue = value.Copy();
		ImGui.InputInt2(Lang.Get("closedcaptions:config-" + option), ref newValue.X);
		Tooltip(option);
		modified |= newValue != value;
		return newValue;
	}

	private Vec4f OnColor(string option, Vec4f value, ref bool modified)
	{
		Vector4 newValue = new(value.X, value.Y, value.Z, value.W);
		ImGui.ColorEdit4(Lang.Get("closedcaptions:config-" + option), ref newValue);
		Tooltip(option);
		Vec4f ret = new(newValue.X, newValue.Y, newValue.Z, newValue.W);
		modified |= ret != value;
		return ret;
	}

	private int OnDropdown(string option, int value, string[] items, ref bool modified)
	{
		int newValue = value;
		ImGui.Combo(Lang.Get("closedcaptions:config-" + option), ref newValue, items, items.Length);
		Tooltip(option);
		modified |= newValue != value;
		return newValue;
	}
}
