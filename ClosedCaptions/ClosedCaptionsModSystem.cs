using ClosedCaptions.GUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ClosedCaptions;

public class ClosedCaptionsModSystem : ModSystem
{
	private static ClosedCaptionsOverlay _overlay;

	private static ICoreClientAPI _capi;

	private long _gameTickListenerId;

	public override void StartClientSide(ICoreClientAPI api)
	{
		_capi = api;
		base.StartClientSide(_capi);

		_overlay = new(_capi);

		_overlay.TryOpen(withFocus: false);
		_gameTickListenerId = _capi.Event.RegisterGameTickListener(OnTick, 500);
	}

	private void OnTick(float dt)
	{
		_overlay.Update();
	}

}
