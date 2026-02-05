using System;
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
using Vintagestory.Common;

namespace ClosedCaptions;

public class ClosedCaptionsModSystem : ModSystem
{
	public static UserConfig UserConfig = new();

	private Harmony? patcher;
	private ICoreClientAPI? _capi;

	private CaptionManager? _manager;

	private CancellationTokenSource _cancelSource = new();

	public override void StartPre(ICoreAPI api)
	{
		base.StartPre(api);

		UserConfig = api.LoadModConfig<UserConfig>(UserConfig.Filename) ?? new UserConfig();
	}

	public override void StartClientSide(ICoreClientAPI api)
	{
		_capi = api;
		base.StartClientSide(_capi);

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
				config.ShowTemporal = true;
				config.ShowTool = true;
				config.ShowVoice = true;
				config.ShowWalk = true;
				config.ShowWearable = true;
				config.ShowWeather = true;
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
				config.ShowTemporal = false;
				config.ShowTool = false;
				config.ShowVoice = false;
				config.ShowWalk = false;
				config.ShowWearable = false;
				config.ShowWeather = false;
				config.ShowUnknown = false;
			}
			config.ShowAmbience = OnCheckBox("show-ambience", config.ShowAmbience, ref modified);
			config.ShowAnimal = OnCheckBox("show-animal", config.ShowAnimal, ref modified);
			config.ShowBlock = OnCheckBox("show-block", config.ShowBlock, ref modified);
			config.ShowCombat = OnCheckBox("show-combat", config.ShowCombat, ref modified);
			config.ShowDanger = OnCheckBox("show-danger", config.ShowDanger, ref modified);
			config.ShowEnemy = OnCheckBox("show-enemy", config.ShowEnemy, ref modified);
			config.ShowEnvironment = OnCheckBox("show-environment", config.ShowEnvironment, ref modified);
			config.ShowInteraction = OnCheckBox("show-interaction", config.ShowInteraction, ref modified);
			config.ShowTemporal = OnCheckBox("show-temporal", config.ShowTemporal, ref modified);
			config.ShowTool = OnCheckBox("show-tool", config.ShowTool, ref modified);
			config.ShowVoice = OnCheckBox("show-voice", config.ShowVoice, ref modified);
			config.ShowWalk = OnCheckBox("show-walk", config.ShowWalk, ref modified);
			config.ShowWearable = OnCheckBox("show-wearable", config.ShowWearable, ref modified);
			config.ShowWeather = OnCheckBox("show-weather", config.ShowWeather, ref modified);
			config.ShowUnknown = OnCheckBox("show-unknown", config.ShowUnknown, ref modified);
			ImGui.Unindent();
		}

		// config.ShowIcons = OnCheckBox("show-icons", config.ShowIcons, ref modified);
		config.DisplayOffset = OnInputInt("display-offset", (int)config.DisplayOffset, ref modified, 0);
		config.MinimumDirectionDistance = OnInputFloat("minimum-direction-distance", (int)config.MinimumDirectionDistance, ref modified, 0f);
		config.MinimumDisplayDuration = OnInputInt("minimum-display-duration", (int)config.MinimumDisplayDuration, ref modified, 0);
		config.FadeOutDuration = OnInputInt("fade-out-duration", (int)config.FadeOutDuration, ref modified, 1);
		config.AttenuationRange = OnInputInt("attenuation-range", config.AttenuationRange, ref modified, 0);
		config.MinimumAttenuationOpacity = OnInputInt("min-attenuation-opacity", (int)(config.MinimumAttenuationOpacity * 100), ref modified, 0, 100) / 100f;
		config.GroupingRange = OnInputInt("grouping-range", config.GroupingRange, ref modified, 0);
		config.GroupingMaxTime = OnInputInt("grouping-max-time", config.GroupingMaxTime, ref modified, 0);

		config.DebugMode = OnCheckBox("debug-mode", config.DebugMode, ref modified);

		if (modified)
			_manager?.ForceRefresh();
	}

	private bool OnCheckBox(string option, bool value, ref bool modified)
	{
		bool newValue = value;
		ImGui.Checkbox(Lang.Get("closedcaptions:config-" + option), ref newValue);
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
		{
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
			ImGui.TextUnformatted(Lang.Get($"closedcaptions:config-{option}-tooltip"));
			ImGui.EndTooltip();
		}
		modified |= newValue != value;
		return newValue;
	}

	private float OnInputFloat(string option, float value, ref bool modified, float min = float.MinValue, float max = float.MaxValue)
	{
		float newValue = value;
		ImGui.InputFloat(Lang.Get("closedcaptions:config-" + option), ref newValue);
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
		{
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
			ImGui.TextUnformatted(Lang.Get($"closedcaptions:config-{option}-tooltip"));
			ImGui.EndTooltip();
		}
		newValue = Math.Max(min, Math.Min(newValue, max));
		modified |= newValue != value;
		return newValue;
	}

	private int OnInputInt(string option, int value, ref bool modified, int min = int.MinValue, int max = int.MaxValue)
	{
		int newValue = value;
		ImGui.InputInt(Lang.Get("closedcaptions:config-" + option), ref newValue);
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
		{
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
			ImGui.TextUnformatted(Lang.Get($"closedcaptions:config-{option}-tooltip"));
			ImGui.EndTooltip();
		}
		newValue = Math.Max(min, Math.Min(newValue, max));
		modified |= newValue != value;
		return newValue;
	}
}
