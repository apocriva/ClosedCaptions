using ClosedCaptions.GUI;
using HarmonyLib;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Common;

namespace ClosedCaptions;

public class ClosedCaptionsModSystem : ModSystem
{
	private Harmony patcher;
	private ICoreClientAPI _capi;

	private CaptionManager _manager;
	private long _gameTickListenerId;

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
		_gameTickListenerId = _capi.Event.RegisterGameTickListener(OnTick, 100);
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
}
