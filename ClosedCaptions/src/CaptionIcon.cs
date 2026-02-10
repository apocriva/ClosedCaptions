using Vintagestory.API.Common;

namespace ClosedCaptions;

public class CaptionIcon
{
	public string Type = "";
	public string Code = "";

	public CollectibleObject GetCollectibleObject(ICoreAPI capi)
	{
		if (string.IsNullOrEmpty(Type) || string.IsNullOrEmpty(Code))
			return capi.World.GetBlock(0);

		CollectibleObject? ret;
		if (Type == "item")
			ret = capi.World.GetItem(new AssetLocation(Code));
		else
			ret = capi.World.GetBlock(new AssetLocation(Code));

		return ret ?? capi.World.GetBlock(0);
	}
}