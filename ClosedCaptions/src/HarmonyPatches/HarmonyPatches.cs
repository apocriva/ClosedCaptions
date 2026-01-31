using System;
using HarmonyLib;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace ClosedCaptions.HarmonyPatches;

[HarmonyPatchCategory("closedcaptions")]
public static class HarmonyPatches
{
	[HarmonyPostfix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "Start")]
	public static void OnLoadedSoundStarted(ILoadedSound __instance)
	{
		CaptionManager.SoundStarted(__instance, __instance.Params.Location);
	}
}