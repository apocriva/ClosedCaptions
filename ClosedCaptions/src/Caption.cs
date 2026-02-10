using System;
using ClosedCaptions.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

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
	Temporal	= 1 << 8,
	Tool		= 1 << 9,
	Voice		= 1 << 10,
	Walk		= 1 << 11,
	Wearable	= 1 << 12,
	Weather		= 1 << 13,
}

[Flags]
public enum CaptionFlags
{
	None			= 0,
	Directionless	= 1 << 0,
}

public class Caption
{
	
	public readonly nint ID;
	public readonly AssetLocation AssetLocation;
	public readonly string Text;
	public long StartTime;
	public long FadeOutStartTime;
	public float Volume;
	public bool IsRelative;
	public Vec3f Position;
	public readonly CaptionTags Tags;
	public readonly CaptionFlags Flags;
	public readonly CaptionGroup? Group;
	public readonly CaptionIcon? Icon;

	public Caption(
		ILoadedSound loadedSound,
		string text,
		long startTime,
		float volume,
		bool isRelative,
		Vec3f position,
		CaptionTags tags,
		CaptionFlags flags,
		CaptionGroup? group,
		CaptionIcon? icon)
	{
		ID = loadedSound.ToIntPtr();
		AssetLocation = loadedSound.Params.Location;
		Text = text;
		StartTime = startTime;
		FadeOutStartTime = 0;
		Volume = volume;
		IsRelative = isRelative;
		Position = position;
		Tags = tags;
		Flags = flags;
		Group = group;
		Icon = icon;
	}

	public void UpdateFrom(ILoadedSound sound)
	{
		if (sound.ToIntPtr() != ID)
			CaptionManager.Api.Logger.Warning($"[ClosedCaptions] Updating sound '{AssetLocation}' from a different LoadedSound?");

		Volume = sound.Params.Volume;
		if (sound.Params.RelativePosition)
			Position = Vec3f.Zero;
		else
			Position = sound.Params.Position;

		CaptionManager.MarkDirty();
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