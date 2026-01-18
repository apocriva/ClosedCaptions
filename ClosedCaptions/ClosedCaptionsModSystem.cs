using ClosedCaptions.GUI;
using Gantry.Core.ModSystems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ClosedCaptions;

public class ClosedCaptionsModSystem : ClientModSystem
{
	private static ClosedCaptionsOverlay _overlay;

	private static ICoreClientAPI _capi;

	public override void StartClientSide(ICoreClientAPI api)
	{
		_capi = api;
		base.StartClientSide(_capi);

		_overlay = new(_capi);

		_overlay.TryOpen(withFocus: false);
	}

}
