using System;
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
	private long _gameTickListenerId;

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
		_gameTickListenerId = _capi.Event.RegisterGameTickListener(OnTick, 60);

		if (api.ModLoader.IsModEnabled("configlib"))
		{
			var configlib = api.ModLoader.GetModSystem<ConfigLibModSystem>();
			configlib.RegisterCustomConfig(Lang.Get("closedcaptions:config"), (id, buttons) => EditConfig(id, buttons, api));
		}
	}

	public override void Dispose()
	{
		if (_gameTickListenerId != 0)
		{
			_capi.Event.UnregisterGameTickListener(_gameTickListenerId);
			_gameTickListenerId = 0;
		}

		_manager?.Dispose();
		patcher?.UnpatchAll(patcher.Id);
	}

	private void OnTick(float dt)
	{
		_manager.Tick();
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
		config.FilterWeather = OnCheckBox("filter-weather", config.FilterWeather);
		config.FilterSelf = OnCheckBox("filter-self", config.FilterSelf);
		config.FilterWalk = OnCheckBox("filter-walk", config.FilterWalk);

		_manager.ForceRefresh();
	}

	private bool OnCheckBox(string option, bool value)
	{
		bool newValue = value;
		ImGui.Checkbox(Lang.Get("closedcaptions:config-" + option), ref newValue);
		return newValue;
	}
}
