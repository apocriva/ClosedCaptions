using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace ClosedCaptions.Config;

public class MatchConfig
{
    public static readonly string Filename = "config/matchconfig.json";

    public class MatchGroup
	{
		public string Group = "";
		public string DefaultKey = "";
        public Mapping[] Mappings = [];
	}

	public class Unique
	{
		public string Group = "";
		public int Priority = 0;
	}

	public class Icon
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

	public class Mapping
	{
		public string Match = "";
        public string CaptionKey = "";
		[JsonConverter(typeof(FlagsConverter))]
		public CaptionManager.Tags Tags = CaptionManager.Tags.None;
		[JsonConverter(typeof(FlagsConverter))]
		public CaptionManager.Flags Flags = CaptionManager.Flags.None;
		public Unique? Unique = null;
		public Icon? Icon = null;

		private class FlagsConverter : JsonConverter
		{
			public override bool CanConvert(Type objectType)
			{
				return Attribute.GetCustomAttribute(objectType, typeof(FlagsAttribute)) != null;
			}

			public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
			{
				if (string.IsNullOrEmpty((string?)reader.Value))
					return 0;

				try
				{
					var ret = Enum.Parse(objectType, (string)reader.Value, true);
					return ret;
				}
				catch
				{
					return CaptionManager.Tags.None;
				}
			}

			public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
			{
			}
		}
	}

	public string[] Ignore = [];
	public MatchGroup[] SoundMap = [];

	public ICoreClientAPI? Api { get; set; }

	public bool FindCaptionForSound(
		AssetLocation location,
		ref string? text,
		ref CaptionManager.Tags tags,
		ref CaptionManager.Flags flags,
		ref Unique? unique,
		ref Icon? icon)
	{
		text = null;
		tags = CaptionManager.Tags.None;
		flags = CaptionManager.Flags.None;
		unique = null;
		icon = null;

		// Check if this is an outright ignored sound.
		foreach (var ignore in Ignore)
		{
			if (WildcardUtil.Match(new AssetLocation(ignore), location))
				return false;
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
						tags = mapping.Tags;
						flags = mapping.Flags;
						unique = mapping.Unique;
						icon = mapping.Icon;
						text = Lang.Get(mapping.CaptionKey);
						return true;
					}
				}

				// It did not have a better match. Store the group's default
				// key for now in case for some reason there's another match
				// group that has a better match.
				text ??= Lang.Get(matchGroup.DefaultKey);
			}
		}

		// Text can be null, if the sound was not matched, in which case it is an unknown sound.
		if (text == null)
		{
			text = Lang.Get("closedcaptions:unknown-sound");
			return false;
		}

		return true;
	}
}