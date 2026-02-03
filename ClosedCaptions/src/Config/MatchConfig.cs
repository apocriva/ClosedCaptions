using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace ClosedCaptions.Config;

public class MatchConfig
{
    public static readonly string Filename = "config/matchconfig.json";

    public class MatchGroup
	{
		public string Group;
		public string DefaultKey;
        public Mapping[] Mappings;
	}

	public class Mapping
	{
		public string Match;
        public string CaptionKey;
		public string IconType;
		public string IconCode;
	}

	public string[] Ignore;
	public MatchGroup[] SoundMap;

	public string? FindCaptionForSound(AssetLocation location, ref string iconType, ref string iconCode)
	{
		string? ret = null;
		iconType = null;
		iconCode = null;

		// Check if this is an outright ignored sound.
		foreach (var ignore in Ignore)
		{
			if (WildcardUtil.Match(new AssetLocation(ignore), location))
				return null;
		}

		// Iterate the mappings in reverse, to prioritize more recently-added
		// entries. This way if new entries are added by a mod, they will always
		// be prioritized over the base-level categories.
		for (int i = SoundMap.Length - 1; i >= 0; --i)
		{
			var matchGroup = SoundMap[i];
			if (WildcardUtil.Match(new AssetLocation(matchGroup.Group), location))
			{
				// Sound is in this group! Does it have a better match?
				for (int j = matchGroup.Mappings.Length - 1; j >= 0; --j)
				{
					var mapping = matchGroup.Mappings[j];
					if (WildcardUtil.Match(new AssetLocation(mapping.Match), location))
					{
						if (!string.IsNullOrEmpty(mapping.IconType))
							iconType = mapping.IconType;
						if (!string.IsNullOrEmpty(mapping.IconCode))
							iconCode = mapping.IconCode;
						return Lang.Get(mapping.CaptionKey);
					}
				}

				// It did not have a better match. Store the group's default
				// key for now in case for some reason there's another match
				// group that has a better match.
				ret ??= Lang.Get(matchGroup.DefaultKey);
			}
		}

		return ret;
	}
}