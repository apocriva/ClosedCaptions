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

	private Harmony patcher;
	private ICoreClientAPI _capi;

	private CaptionManager _manager;

	private CancellationTokenSource _cancelSource;

	public override void StartPre(ICoreAPI api)
	{
		base.StartPre(api);

		UserConfig = api.LoadModConfig<UserConfig>(UserConfig.Filename);
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
			configlib.RegisterCustomConfig(Lang.Get("closedcaptions:config"), (id, buttons) => EditConfig(id, buttons, api));
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

	private void EditConfig(string id, ControlButtons buttons, ICoreAPI api)
	{
		if (buttons.Save)
			api.StoreModConfig(UserConfig, UserConfig.Filename);
		if (buttons.Restore)
			UserConfig = api.LoadModConfig<UserConfig>(UserConfig.Filename);
		if (buttons.Defaults)
			UserConfig = new();

		if (UserConfig != null)
		{
			BuildSettings(UserConfig, id);
		}
	}

	private void BuildSettings(UserConfig config, string id)
	{
		var modified = false;
		config.FilterWeather = OnCheckBox("filter-weather", config.FilterWeather, ref modified);
		config.FilterSelf = OnCheckBox("filter-self", config.FilterSelf, ref modified);
		config.FilterWalk = OnCheckBox("filter-walk", config.FilterWalk, ref modified);

		config.ShowIcons = OnCheckBox("show-icons", config.ShowIcons, ref modified);
		config.MinimumDisplayDuration = OnInputInt("minimum-display-duration", (int)config.MinimumDisplayDuration, ref modified);
		config.FadeOutDuration = OnInputInt("fade-out-duration", (int)config.FadeOutDuration, ref modified);
		config.AttenuationRange = OnInputInt("attenuation-range", config.AttenuationRange, ref modified);
		config.MinimumAttenuationOpacity = OnInputInt("min-attenuation-opacity", (int)(config.MinimumAttenuationOpacity * 100), ref modified) / 100f;
		config.GroupingRange = OnInputInt("grouping-range", config.GroupingRange, ref modified);
		config.GroupingMaxTime = OnInputInt("grouping-max-time", config.GroupingMaxTime, ref modified);

		config.DebugMode = OnCheckBox("debug-mode", config.DebugMode, ref modified);

		if (modified)
			_manager.ForceRefresh();
	}

	private bool OnCheckBox(string option, bool value, ref bool modified)
	{
		bool newValue = value;
		ImGui.Checkbox(Lang.Get("closedcaptions:config-" + option), ref newValue);
		modified |= newValue != value;
		return newValue;
	}

	private int OnInputInt(string option, int value, ref bool modified)
	{
		int newValue = value;
		ImGui.InputInt(Lang.Get("closedcaptions:config-" + option), ref newValue);
		modified |= newValue != value;
		return newValue;
	}
}
