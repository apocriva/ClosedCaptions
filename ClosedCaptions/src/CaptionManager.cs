using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ClosedCaptions.Config;
using ClosedCaptions.Extensions;
using ClosedCaptions.GUI;
using HarmonyLib;
using OpenTK.Audio.OpenAL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client;
using Vintagestory.Common;

namespace ClosedCaptions;

[HarmonyPatchCategory("closedcaptions")]
public class CaptionManager
{
    private static CaptionManager? _instance;
    private static CaptionManager Instance
    {
        get
        {
            if (_instance == null)
                throw new NullReferenceException();
            return _instance;
        }
    }

    private static ICoreClientAPI? _capi;
    public static ICoreClientAPI Api
    {
        get
        {
            if (_capi == null)
                throw new NullReferenceException();
            return _capi;
        }
    }

    private readonly ClosedCaptionsOverlay _overlay;
    private readonly MatchConfig _matchConfig;
    private readonly Dictionary<int, Caption> _captions = [];
    private readonly List<Caption> _displayedCaptions = [];

    private bool _needsRefresh = false;

    public CaptionManager(ICoreClientAPI capi)
    {
        _instance = this;
        _capi = capi;

        try
        {
            _matchConfig = capi.Assets.Get(new AssetLocation("closedcaptions", MatchConfig.Filename)).ToObject<MatchConfig>();
            _matchConfig.Api = capi;
        }
        catch (Exception e)
        {
            capi.Logger.Error($"Error loading {MatchConfig.Filename}: {e}");
            _matchConfig = new(capi);
        }

        _overlay = new(capi);
    }

    public static List<Caption> GetDisplayedCaptions()
    {
        return [.. Instance._displayedCaptions];
    }

    public void Dispose()
    {
        _overlay?.Dispose();
    }

    public void Tick()
    {
        UpdateSoundsStatus();

        if (_needsRefresh)
        {
            ForceRefresh();
            _needsRefresh = false;
        }
    }

    public void ForceRefresh()
    {
        RebuildDisplayCaptions();
        _overlay.Rebuild();
    }

#region Harmony patches
    // public void PlaySound(SoundAttributes sound);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySound), typeof(SoundAttributes))]
    // public static void World_PlaySound(ClientMain __instance, SoundAttributes sound)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySound(sound={sound.ToString().Escape()})");
    // }

    // public void PlaySound(AssetLocation location, bool randomizePitch = false, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySound), typeof(AssetLocation), typeof(bool), typeof(float))]
    // public static void World_PlaySound(ClientMain __instance, AssetLocation location, bool randomizePitch, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySound(location={location}, randomizePitch={randomizePitch}, volume={volume})");
    // }

    // public int PlaySoundAt(SoundAttributes sound, double x, double y, double z, int dimension, IPlayer? dualCallByPlayer = null, float volumeMultiplier = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(SoundAttributes), typeof(double), typeof(double), typeof(double), typeof(int), typeof(IPlayer), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, int __result, SoundAttributes sound, double x, double y, double z, int dimension, IPlayer? dualCallByPlayer, float volumeMultiplier)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(sound={sound.ToString().Escape()}, x={x}, y={y}, z={z}, dimension={dimension}, dualCallByPlayer={dualCallByPlayer}, volumeMultiplier={volumeMultiplier}");
    // }

    // public int PlaySoundAt(SoundAttributes sound, BlockPos pos, double yOffsetFromCenter, IPlayer? dualCallByPlayer = null, float volumeMultiplier = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(SoundAttributes), typeof(BlockPos), typeof(double), typeof(IPlayer), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, int __result, SoundAttributes sound, BlockPos pos, double yOffsetFromCenter, IPlayer? dualCallByPlayer, float volumeMultiplier)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(sound={sound.ToString().Escape()}, pos={pos}, yOffsetFromCenter={yOffsetFromCenter}, dualCallByPlayer={dualCallByPlayer}, volumeMultiplier={volumeMultiplier})");
    // }

    // public int PlaySoundAt(SoundAttributes sound, Entity atEntity, IPlayer? dualCallByPlayer = null, float volumeMultiplier = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(SoundAttributes), typeof(Entity), typeof(IPlayer), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, int __result, SoundAttributes sound, Entity atEntity, IPlayer? dualCallByPlayer, float volumeMultiplier)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(sound={sound.ToString().Escape()}, atEntity={atEntity}, dualCallByPlayer={dualCallByPlayer}, volumeMultiplier={volumeMultiplier})");
    //     if (sound.Location != null && atEntity != null)
    //         Instance.StoreSoundEntitySource(sound.Location.ToString(), atEntity);
    // }

    // public int PlaySoundAt(SoundAttributes sound, IPlayer atPlayer, IPlayer? dualCallByPlayer = null, float volumeMultiplier = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(SoundAttributes), typeof(IPlayer), typeof(IPlayer), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, int __result, SoundAttributes sound, IPlayer atPlayer, IPlayer? dualCallByPlayer, float volumeMultiplier)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(sound={sound.ToString().Escape()}, atPlayer={atPlayer}, dualCallByPlayer={dualCallByPlayer}, volumeMultiplier={volumeMultiplier})");
    // }

    // public void PlaySoundAt(AssetLocation? location, Entity atEntity, IPlayer? dualCallByPlayer = null, float pitch = 1, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(AssetLocation), typeof(Entity), typeof(IPlayer), typeof(float), typeof(float), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, AssetLocation? location, Entity atEntity, IPlayer? dualCallByPlayer, float pitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(location={location}, atEntity={atEntity}, dualCallByPlayer={dualCallByPlayer}, pitch={pitch}, range={range}, volume={volume})");
    //     if (location != null && atEntity != null)
    //         Instance.StoreSoundEntitySource(location, atEntity);
    // }

    // public void PlaySoundAt(AssetLocation? location, double posx, double posy, double posz, IPlayer? dualCallByPlayer = null, float pitch = 1, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(AssetLocation), typeof(double), typeof(double), typeof(double), typeof(IPlayer), typeof(float), typeof(float), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, AssetLocation? location, double posx, double posy, double posz, IPlayer? dualCallByPlayer, float pitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(location={location}, posx={posx}, posy={posy}, posz={posz}, dualCallByPlayer={dualCallByPlayer}, pitch={pitch}, range={range}, volume={volume})");
    // }

    // public void PlaySoundAt(AssetLocation? location, Entity atEntity, IPlayer? ignorePlayerUid = null, bool randomizePitch = true, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(AssetLocation), typeof(Entity), typeof(IPlayer), typeof(bool), typeof(float), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, AssetLocation? location, Entity atEntity, IPlayer? ignorePlayerUid, bool randomizePitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(location={location}, atEntity={atEntity}, dualCallByPlayer={ignorePlayerUid}, randomizePitch={randomizePitch}, range={range}, volume={volume})");
    //     if (location != null && atEntity != null)
    //         Instance.StoreSoundEntitySource(location.ToString(), atEntity);
    // }

    // public void PlaySoundAt(AssetLocation? location, double x, double y, double z, IPlayer? ignorePlayerUid = null, bool randomizePitch = true, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(AssetLocation), typeof(double), typeof(double), typeof(double), typeof(IPlayer), typeof(bool), typeof(float), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, AssetLocation? location, double x, double y, double z, IPlayer? ignorePlayerUid, bool randomizePitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(location={location}, x={x}, y={y}, z={z}, ignorePlayerUid={ignorePlayerUid}, randomizePitch={randomizePitch}, range={range}, volume={volume})");
    // }

    // public void PlaySoundAt(AssetLocation? location, BlockPos pos, double yOffsetFromCenter, IPlayer? ignorePlayerUid = null, bool randomizePitch = true, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(AssetLocation), typeof(BlockPos), typeof(double), typeof(IPlayer), typeof(bool), typeof(float), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, AssetLocation? location, BlockPos pos, double yOffsetFromCenter, IPlayer? ignorePlayerUid, bool randomizePitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(location={location}, pos={pos}, yOffsetFromCenter={yOffsetFromCenter}, ignorePlayerUid={ignorePlayerUid}, randomizePitch={randomizePitch}, range={range}, volume={volume})");
    // }

    // public int PlaySoundAt(AssetLocation? location, double x, double y, double z, float volume, bool randomizePitch = true, float range = 32);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(AssetLocation), typeof(double), typeof(double), typeof(double), typeof(float), typeof(bool), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, int __result, AssetLocation? location, double x, double y, double z, float volume, bool randomizePitch, float range)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(location={location}, x={x}, y={y}, z={z}, randomizePitch={randomizePitch}, range={range}, volume={volume})");
    // }

    // public void PlaySoundAt(AssetLocation? location, IPlayer? atPlayer, IPlayer? ignorePlayerUid = null, bool randomizePitch = true, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(AssetLocation), typeof(IPlayer), typeof(IPlayer), typeof(bool), typeof(float), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, AssetLocation? location, IPlayer? atPlayer, IPlayer? ignorePlayerUid, bool randomizePitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(location={location}, atPlayer={atPlayer}, ignorePlayerUid={ignorePlayerUid}, randomizePitch={randomizePitch}, range={range}, volume={volume})");
    // }

    // public void PlaySoundAt(AssetLocation? location, double posx, double posy, double posz, IPlayer? dualCallByPlayer, EnumSoundType soundType, float pitch = 1, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAt), typeof(AssetLocation), typeof(double), typeof(double), typeof(double), typeof(IPlayer), typeof(EnumSoundType), typeof(float), typeof(float), typeof(float))]
    // public static void World_PlaySoundAt(ClientMain __instance, AssetLocation? location, double posx, double posy, double posz, IPlayer? dualCallByPlayer, EnumSoundType soundType, float pitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAt(location={location}, posx={posx}, posy={posy}, posz={posz}, dualCallByPlayer={dualCallByPlayer}, soundType={soundType}, pitch={pitch}, range={range}, volume={volume})");
    // }

    // public int PlaySoundAtAndGetDuration(AssetLocation? location, double x, double y, double z, IPlayer? ignorePlayerUid = null, bool randomizePitch = true, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundAtAndGetDuration), typeof(AssetLocation), typeof(double), typeof(double), typeof(double), typeof(IPlayer), typeof(bool), typeof(float), typeof(float))]
    // public static void World_PlaySoundAtAndGetDuration(ClientMain __instance, int __result, AssetLocation? location, double x, double y, double z, IPlayer? ignorePlayerUid, bool randomizePitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundAtAndGetDuration(location={location}, x={x}, y={y}, z={z}, ignorePlayerUid={ignorePlayerUid}, randomizePitch={randomizePitch}, range={range}, volume={volume})");
    // }

    // public void PlaySoundFor(AssetLocation? location, IPlayer? atPlayer, float pitch, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundFor), typeof(AssetLocation), typeof(IPlayer), typeof(float), typeof(float), typeof(float))]
    // public static void World_PlaySoundFor(ClientMain __instance, AssetLocation? location, IPlayer? atPlayer, float pitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundFor(location={location}, atPlayer={atPlayer}, pitch={pitch}, range={range}, volume={volume})");
    // }

    // public int PlaySoundFor(SoundAttributes sound, IPlayer? forPlayer, float volumeMultiplier = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundFor), typeof(SoundAttributes), typeof(IPlayer), typeof(float))]
    // public static void World_PlaySoundFor(ClientMain __instance, int __result, SoundAttributes sound, IPlayer? forPlayer, float volumeMultiplier)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundFor(sound={sound.ToString().Escape()}, forPlayer={forPlayer}, volumeMultiplier={volumeMultiplier})");
    // }

    // public void PlaySoundFor(AssetLocation? location, IPlayer? forPlayer, bool randomizePitch = true, float range = 32, float volume = 1);
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(ClientMain), nameof(ClientMain.PlaySoundFor), typeof(AssetLocation), typeof(IPlayer), typeof(bool), typeof(float), typeof(float))]
    // public static void World_PlaySoundFor(ClientMain __instance, AssetLocation? location, IPlayer? forPlayer, bool randomizePitch, float range, float volume)
    // {
    //     // Api.Logger.Debug($"[ClosedCaptions] World_PlaySoundFor(location={location}, atPlayer={forPlayer}, randomizePitch={randomizePitch}, range={range}, volume={volume})");
    // }

    [HarmonyPostfix()]
    [HarmonyPatch(typeof(LoadedSoundNative), "Start")]
    public static void Sound_Start(LoadedSoundNative __instance)
    {
        Api.Event.EnqueueMainThreadTask(() =>
        {
            if (Instance._captions.TryGetValue(__instance.GetSourceID(), out Caption? oldCaption))
            {
                // Orphan the old caption. We don't have its underlying
                // source ID anymore, so we will let it complete gracefully.
                var oldId = oldCaption.ID;
                oldCaption.Orphan();
                Instance._captions.Remove(oldId);
                Instance._captions.Add(oldCaption.ID, oldCaption);
            }

            Instance._matchConfig.BuildCaptionForSound(__instance, out Caption? caption, out var wasIgnored);
            if (wasIgnored)
            {
                //Api.Logger.Debug($"[ClosedCaptions] sound.Start() ignored '{__instance.Params.Location}'");
                return;
            }

            if (caption == null)
            {
                Api.Logger.Error($"[ClosedCaptions] sound.Start() failed to generate caption for '{__instance.Params.Location}'");
                return;
            }

            Instance.AddCaption(caption);
        }, "cc_soundstart");
    }

    [HarmonyPostfix()]
    [HarmonyPatch(typeof(LoadedSoundNative), "SetVolume", [])]
    [HarmonyPatch(typeof(LoadedSoundNative), "SetVolume", typeof(float))]
    public static void Sound_SetVolume(LoadedSoundNative __instance)
    {
        if (__instance.Params.Volume == 0f)
            return;

        Api.Event.EnqueueMainThreadTask(() =>
        {
            if (!Instance._captions.ContainsKey(__instance.GetSourceID()))
                Sound_Start(__instance);
        }, "cc_soundsetvolume");
    }

    [HarmonyPostfix()]
    [HarmonyPatch(typeof(LoadedSoundNative), "Stop")]
    public static void Sound_Stop(LoadedSoundNative __instance)
    {
        Api.Event.EnqueueMainThreadTask(() =>
        {
            if (!Instance._captions.TryGetValue(__instance.GetSourceID(), out Caption? caption))
            {
                //Api.Logger.Debug($"[ClosedCaptions] sound.Stop() for untracked sound. [{__instance.GetSourceID()}] '{__instance.Params.Location}'");
                return;
            }

            if (!caption.IsFading)
                caption.BeginFade();
        }, "cc_soundstop");
    }
#endregion

    private void AddCaption(Caption caption)
    {
        if (caption == null)
        {
            Api.Logger.Error($"[ClosedCaptions] Attemping to add null caption.");
            return;
        }

        if (_captions.ContainsKey(caption.ID))
        {
            Api.Logger.Warning($"[ClosedCaptions] Attempting to add duplicate caption. [{caption.ID}] '{caption.AssetLocation}'");
            return;
        }

        _captions.Add(caption.ID, caption);
        AddOrUpdateDisplayedCaption(caption);
    }

    private void RemoveCaption(Caption caption)
    {
        if (!_captions.ContainsKey(caption.ID))
        {
            Api.Logger.Warning($"[ClosedCaptions] Attempting to remove untracked caption. [{caption.ID}] '{caption.AssetLocation}'");
            return;
        }

        _captions.Remove(caption.ID);
        _displayedCaptions.RemoveAll(match => match.ID == caption.ID);

        // This might have been suppressing another caption, let's just rebuild the display list.
        _needsRefresh = true;
    }

    private void RebuildDisplayCaptions()
    {
        _displayedCaptions.Clear();

        if (_captions.Values.Contains((Caption?)null))
        {
            Api.Logger.Error($"[ClosedCaptions] Captions list has a null caption. Correcting, but something is wrong.");

            List<int> toRemove = [];
            foreach (var kv in _captions)
            {
                if (kv.Value == null)
                {
                    toRemove.Add(kv.Key);
                }

            }

            foreach (var id in toRemove)
            {
                _captions.Remove(id);
            }
        }

        foreach (var kv in _captions)
        {
            AddOrUpdateDisplayedCaption(kv.Value);
        }
    }

    private void AddOrUpdateDisplayedCaption(Caption caption)
    {
        if (caption == null)
        {
            Api.Logger.Error($"[ClosedCaptions] AddOrUpdateDisplayedCaption on null caption.");
            return;
        }

        int removed = _displayedCaptions.RemoveAll(c => c == caption);

        // We don't show filtered or out of range captions.
        float distance = caption.IsRelative ? 0f : (caption.Position - Api.World.Player.Entity.Pos.XYZFloat).Length();
        if (IsFiltered(caption) ||
            !caption.IsMusic && !caption.IsRelative && distance >= caption.Range ||
            caption.IsMusic && caption.Volume == 0f)
        {
            if (removed > 0)
            {
                _needsRefresh = true;
            }
            return;
        }

        bool shouldAdd = true;
        for (int i = _displayedCaptions.Count - 1; i >= 0; --i)
        {
            var comp = _displayedCaptions[i];
            if (comp.Group != null && caption.Group != null &&
                comp.Group.Name == caption.Group.Name)
            {
                if (caption.Group.Priority > comp.Group.Priority)
                {
                    // Only allow the highest priority to stay.
                    _displayedCaptions.RemoveAt(i);
                    break;
                }
                else if (caption.Group.Priority == comp.Group.Priority)
                {
                    // If we are the same priority we'll try to take the closest.
                    float compDistance = comp.IsRelative ? 0f : (comp.Position - Api.World.Player.Entity.Pos.XYZFloat).Length();

                    if (!caption.IsRelative && !comp.IsRelative &&
                        distance < compDistance)
                    {
                        // New caption is closer!
                        _displayedCaptions.RemoveAt(i);
                        break;
                    }
                    else if (caption.Volume > comp.Volume)
                    {
                        // New caption is louder!
                        _displayedCaptions.RemoveAt(i);
                        break;
                    }
                    else
                    {
                        shouldAdd = false;
                    }
                }
                else
                {
                    // Lower priority! Not a chance!
                    shouldAdd = false;
                    break;
                }
            }

            // Are we close enough that we should be grouped anyway?
            if (caption.Text == comp.Text)
            {
                var distTime = Math.Abs(caption.StartTime - comp.StartTime);
                var checkSpace = !caption.IsRelative && !comp.IsRelative;
                var distSpace = (comp.Position - caption.Position).Length();
                var keepNew = false;
                if (checkSpace && distSpace <= ClosedCaptionsModSystem.UserConfig.GroupingRange &&
                    distTime <= ClosedCaptionsModSystem.UserConfig.GroupingMaxTime ||
                    !checkSpace && distTime <= ClosedCaptionsModSystem.UserConfig.GroupingMaxTime)
                {
                    // Keep the most recent as long as it isn't fading.
                    if (caption.StartTime > comp.StartTime &&
                        !caption.IsFading ||
                        comp.IsFading)
                    {
                        keepNew = true;
                    }
                    else if (caption.StartTime < comp.StartTime &&
                        !comp.IsFading ||
                        caption.IsFading)
                    {
                        keepNew = false;
                    }
                    else
                    {
                        // Too many things going on, just keep the new one.
                        keepNew = true;
                    }
                }

                if (keepNew)
                {
                    _displayedCaptions.RemoveAt(i);
                    shouldAdd = true;
                }
                else
                {
                    shouldAdd = false;
                }
            }
        }

        if (shouldAdd)
        {
            _displayedCaptions.Add(caption);
            if (removed == 0)
                _needsRefresh = true;
        }
        else if (removed > 0)
        {
            _needsRefresh = true;
        }
    }

    private void UpdateSoundsStatus()
    {
        List<Caption> toRemove = [];
        foreach (var caption in _captions.Values)
        {
            if (caption == null)
            {
                Api.Logger.Error("[ClosedCaptions] There is a null caption! This shouldn't be able to happen.");
                continue;
            }
            if (caption.IsFading)
            {
                if (Api.ElapsedMilliseconds - caption.FadeOutStartTime >= ClosedCaptionsModSystem.UserConfig.FadeOutDuration)
                {
                    //RemoveCaption(caption);
                    toRemove.Add(caption);
                }
                continue;
            }

            if (AL.IsSource(caption.ID))
            {
                AL.GetSource(caption.ID, ALGetSourcei.SourceState, out var statei);
                var state = (ALSourceState)statei;
                if (state == ALSourceState.Playing)
                {
                    AL.GetSource(caption.ID, ALSourcef.Gain, out var value);
                    caption.Volume = value;

                    AL.GetSource(caption.ID, ALSource3f.Position, out var position);
                    caption.Position = new Vec3f(position.X, position.Y, position.Z);

                    AddOrUpdateDisplayedCaption(caption);
                    continue;
                }
            }

            // Sound is no longer visible for one reason or another.
            caption.BeginFade();
        }

        toRemove.Foreach(RemoveCaption);
    }

    private bool IsFiltered(Caption caption)
    {
        // Music is a special case.
        if (caption.IsMusic)
        {
            if (ClosedCaptionsModSystem.UserConfig.ShowMusic == MusicOption.All ||
                ClosedCaptionsModSystem.UserConfig.ShowMusic == MusicOption.OnlyEvent && (caption.Tags & CaptionTags.Event) != 0)
                return false;

            return true;
        }

        // If any one of these passes, do not filter. This supercedes cases such as, for example
        // a nearby lightning strike being filtered due to ShowWeather, but should be shown because
        // it is tagged as Danger. We still want to let the case fall through if ShowDanger is unchecked
        // but ShowWeather is checked.
        if (caption.Tags != CaptionTags.None)
        {
            if ((caption.Tags & CaptionTags.Ambience) != 0 && ClosedCaptionsModSystem.UserConfig.ShowAmbience ||
                (caption.Tags & CaptionTags.Animal) != 0 && ClosedCaptionsModSystem.UserConfig.ShowAnimal ||
                (caption.Tags & CaptionTags.Block) != 0 && ClosedCaptionsModSystem.UserConfig.ShowBlock ||
                (caption.Tags & CaptionTags.Combat) != 0 && ClosedCaptionsModSystem.UserConfig.ShowCombat ||
                (caption.Tags & CaptionTags.Danger) != 0 && ClosedCaptionsModSystem.UserConfig.ShowDanger ||
                (caption.Tags & CaptionTags.Enemy) != 0 && ClosedCaptionsModSystem.UserConfig.ShowEnemy ||
                (caption.Tags & CaptionTags.Environment) != 0 && ClosedCaptionsModSystem.UserConfig.ShowEnvironment ||
                (caption.Tags & CaptionTags.Interaction) != 0 && ClosedCaptionsModSystem.UserConfig.ShowInteraction ||
                (caption.Tags & CaptionTags.Machinery) != 0 && ClosedCaptionsModSystem.UserConfig.ShowMachinery ||
                (caption.Tags & CaptionTags.Rust) != 0 && ClosedCaptionsModSystem.UserConfig.ShowRust ||
                (caption.Tags & CaptionTags.Temporal) != 0 && ClosedCaptionsModSystem.UserConfig.ShowTemporal ||
                (caption.Tags & CaptionTags.Tool) != 0 && ClosedCaptionsModSystem.UserConfig.ShowTool ||
                (caption.Tags & CaptionTags.Voice) != 0 && ClosedCaptionsModSystem.UserConfig.ShowVoice ||
                (caption.Tags & CaptionTags.Walk) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWalk ||
                (caption.Tags & CaptionTags.Wearable) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWearable ||
                (caption.Tags & CaptionTags.Weather) != 0 && ClosedCaptionsModSystem.UserConfig.ShowWeather)
                return false;
        }

        // It is a tagged sound and should not be shown.
        if (caption.Tags != CaptionTags.None)
            return true;

        return false;
    }
}