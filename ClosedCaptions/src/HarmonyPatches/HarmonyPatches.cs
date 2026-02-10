using System;
using HarmonyLib;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace ClosedCaptions.HarmonyPatches;

[HarmonyPatchCategory("closedcaptions")]
public static class HarmonyPatches
{
	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "FadeIn")]
	public static void LoadedSound_FadeIn(LoadedSoundNative __instance, float duration)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.FadeIn(): [{__instance.ToIntPtr()}] {__instance.Params.Location} duration={duration}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "FadeOut")]
	public static void LoadedSound_FadeOut(LoadedSoundNative __instance, float duration)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.FadeOut(): [{__instance.ToIntPtr()}] {__instance.Params.Location} duration={duration}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "FadeOutAndStop")]
	public static void LoadedSound_FadeOutAndStop(LoadedSoundNative __instance, float duration)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.FadeOutAndStop(): [{__instance.ToIntPtr()}] {__instance.Params.Location} duration={duration}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "FadeTo")]
	public static void LoadedSound_FadeTo(LoadedSoundNative __instance, double newVolume, float duration)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.FadeTo(): [{__instance.ToIntPtr()}] {__instance.Params.Location} newVolume={newVolume} duration={duration}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "Pause")]
	public static void LoadedSound_Pause(LoadedSoundNative __instance)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.Pause(): [{__instance.ToIntPtr()}] {__instance.Params.Location}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetLooping")]
	public static void LoadedSound_SetLooping(LoadedSoundNative __instance, bool on)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.SetLooping(): [{__instance.ToIntPtr()}] {__instance.Params.Location} on={on}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetLowPassfiltering")]
	public static void LoadedSound_SetLowPassfiltering(LoadedSoundNative __instance, float value)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.SetLowPassfiltering(): [{__instance.ToIntPtr()}] {__instance.Params.Location} value={value}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetPitch")]
	public static void LoadedSound_SetPitch(LoadedSoundNative __instance, float val)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.SetPitch(): [{__instance.ToIntPtr()}] {__instance.Params.Location} val={val}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetPitchOffset")]
	public static void LoadedSound_SetPitchOffset(LoadedSoundNative __instance, float val)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.SetPitchOffset(): [{__instance.ToIntPtr()}] {__instance.Params.Location} val={val}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetPosition", typeof(Vec3f))]
	public static void LoadedSound_SetPosition(LoadedSoundNative __instance, Vec3f position)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.SetPoisition(): [{__instance.ToIntPtr()}] {__instance.Params.Location} position={position}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetPosition", typeof(float), typeof(float), typeof(float))]
	public static void LoadedSound_SetPosition(LoadedSoundNative __instance, float x, float y, float z)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.SetPosition(): [{__instance.ToIntPtr()}] {__instance.Params.Location} ({x}, {y}, {z})");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetReverb")]
	public static void LoadedSound_SetReverb(LoadedSoundNative __instance, float reverbDecayTime)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.SetReverb(): [{__instance.ToIntPtr()}] {__instance.Params.Location} reverbDecayTime={reverbDecayTime}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetVolume", typeof(float))]
	public static void LoadedSound_SetVolume(LoadedSoundNative __instance, float val)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.SetVolume(): [{__instance.ToIntPtr()}] {__instance.Params.Location} val={val}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "SetVolume", [])]
	public static void LoadedSound_SetVolume(LoadedSoundNative __instance)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.SetVolume(): [{__instance.ToIntPtr()}] {__instance.Params.Location}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "Start")]
	public static void LoadedSound_Start(LoadedSoundNative __instance)
	{
		//CaptionManager.SoundStarted(__instance, __instance.Params.Location);
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.Start(): [{__instance.ToIntPtr()}] {__instance.Params.Location}");
	}

	[HarmonyPrefix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "Stop")]
	public static void LoadedSound_Stop(LoadedSoundNative __instance)
	{
		if (!__instance.IsPlaying)
			return;
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.Stop(): [{__instance.ToIntPtr()}] {__instance.Params.Location}");
	}

	[HarmonyPostfix()]
	[HarmonyPatch(typeof(LoadedSoundNative), "Toggle")]
	public static void LoadedSound_Toggle(ILoadedSound __instance, bool on)
	{
		CaptionManager.Api.Logger.Debug($"[ClosedCaptions] sound.Toggle(): [{__instance.ToIntPtr()}] {__instance.Params.Location} on={on}");
	}
}