using System;
using ClosedCaptions.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client;

namespace ClosedCaptions;

[Flags]
public enum CaptionTags
{
	None		= 0,
	Ambience	= 1 << 0,
	Animal		= 1 << 1,
	Block		= 1 << 2,
	Combat		= 1 << 3,
	Danger		= 1 << 4,
	Enemy		= 1 << 5,
	Environment	= 1 << 6,
	Interaction = 1 << 7,
	Machinery	= 1 << 8,
	Rust		= 1 << 9,
	Temporal	= 1 << 10,
	Tool		= 1 << 11,
	Voice		= 1 << 12,
	Walk		= 1 << 13,
	Wearable	= 1 << 14,
	Weather		= 1 << 15,

	// Music-specific tags
	Event		= 1 << 16,
}

[Flags]
public enum CaptionFlags
{
	None			= 0,
	Directionless	= 1 << 0,
}

public static class LoadedSoundExtensions
{
	public static int GetSourceID(this LoadedSoundNative sound)
	{
		var ret = typeof(LoadedSoundNative).GetField("sourceId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(sound);

		if (ret != null)
			return (int)ret;

		return 0;
	}

	public static int GetSourceID(this ILoadedSound sound)
	{
		if (sound is not LoadedSoundNative)
			return 0;

		return (sound as LoadedSoundNative)!.GetSourceID();
	}
}

public class Caption
{
	
	public int ID { get; private set; }
	public readonly AssetLocation AssetLocation;
	public readonly string Text;
	public long StartTime;
	public long FadeOutStartTime;
	public float Volume;
	public bool IsRelative;
	public Vec3f Position;
	public float Range;
	public float AttenuationRange;
	public bool IsMusic;
	public readonly CaptionTags Tags;
	public readonly CaptionFlags Flags;
	public readonly CaptionGroup? Group;
	public readonly CaptionIcon? Icon;

	private static int NextOrphanId = -1;

	public bool IsFading => FadeOutStartTime > 0;

	public Caption(
		ILoadedSound loadedSound,
		string text,
		long startTime,
		float volume,
		bool isRelative,
		Vec3f position,
		float range,
		float attenuationRange,
		CaptionTags tags,
		CaptionFlags flags,
		CaptionGroup? group,
		CaptionIcon? icon)
	{
		ID = loadedSound.GetSourceID();
		if (ID == 0)
		{
			ID = NextOrphanId--;
			//CaptionManager.Api.Logger.Debug($"[ClosedCaptions] Creating caption with no source ID. Assigning orphan ID. [{ID}] '{loadedSound.Params.Location}'");
		}
		AssetLocation = loadedSound.Params.Location;
		Text = text;
		StartTime = startTime;
		FadeOutStartTime = 0;
		Volume = volume;
		IsRelative = isRelative;
		Position = position;
		Range = range;
		AttenuationRange = attenuationRange;
		Tags = tags;
		Flags = flags;
		Group = group;
		Icon = icon;

		IsMusic = loadedSound.Params.SoundType == EnumSoundType.Music ||
			loadedSound.Params.SoundType == EnumSoundType.MusicGlitchunaffected;

		if (IsMusic)
		{
			// This probably doesn't work for resonator tracks.
			IsRelative = true;
			Position = Vec3f.Zero;
		}
	}

	public void Orphan()
	{
		ID = NextOrphanId--;
		if (!IsFading)
			BeginFade();
	}

	public void BeginFade()
	{
		FadeOutStartTime = CaptionManager.Api.ElapsedMilliseconds;
		if (FadeOutStartTime < StartTime + ClosedCaptionsModSystem.UserConfig.FadeOutDuration)
			FadeOutStartTime = StartTime + ClosedCaptionsModSystem.UserConfig.FadeOutDuration;
	}

	public static int CompareByDistance(Caption a, Caption b)
	{
		if (a.IsRelative && b.IsRelative)
			return 0;
		
		if (a.IsRelative && !b.IsRelative)
			return 1;

		if (!a.IsRelative && b.IsRelative)
			return -1;

		var playerPos = CaptionManager.Api.World.Player.Entity.Pos.XYZFloat;
		return (a.Position - playerPos).Length().CompareTo((b.Position - playerPos).Length());
	}
}