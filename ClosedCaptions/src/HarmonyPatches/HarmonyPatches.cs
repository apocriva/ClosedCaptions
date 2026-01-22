using HarmonyLib;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace ClosedCaptions.HarmonyPatches;

[HarmonyPatchCategory("closedcaptions")]
public static class HarmonyPatches
{
	[HarmonyPostfix()]
	[HarmonyPatch(typeof(ClientMain), "StartPlaying", typeof(ILoadedSound), typeof(AssetLocation))]
	public static void OnStartPlaying(ClientMain __instance, ILoadedSound loadedSound, AssetLocation location)
	{
		CaptionManager.SoundStarted(loadedSound, location);
	}
}