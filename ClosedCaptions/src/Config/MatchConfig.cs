using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
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

	public class Mapping
	{
		public string Match = "";
        public string CaptionKey = "";
		[JsonConverter(typeof(FlagsConverter))]
		public CaptionTags Tags = CaptionTags.None;
		[JsonConverter(typeof(FlagsConverter))]
		public CaptionFlags Flags = CaptionFlags.None;
		public CaptionGroup? Group = null;
		public CaptionIcon? Icon = null;

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
					return CaptionTags.None;
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

	public MatchConfig() { }
	public MatchConfig(ICoreClientAPI capi)
	{
		Api = capi;
	}

	public void BuildCaptionForSound(ILoadedSound sound, out Caption? caption, out bool wasIgnored)
	{
		caption = null;

		// Check if this is an outright ignored sound.
		foreach (var ignore in Ignore)
		{
			if (WildcardUtil.Match(new AssetLocation(ignore), sound.Params.Location))
			{
				wasIgnored = true;
				return;
			}
		}

		wasIgnored = false;
		MatchGroup? partialMatch = null;

		// Iterate the mappings in reverse, to prioritize more recently-added
		// entries. This way if new entries are added by a mod, they will always
		// be prioritized over the base-level categories.
		for (int i = SoundMap.Length - 1; i >= 0; --i)
		{
			var matchGroup = SoundMap[i];
			if (WildcardUtil.Match(new AssetLocation(matchGroup.Group), sound.Params.Location))
			{
				// Sound is in this group! Does it have a better match?
				for (int j = matchGroup.Mappings.Length - 1; j >= 0; --j)
				{
					var mapping = matchGroup.Mappings[j];
					if (WildcardUtil.Match(new AssetLocation(mapping.Match), sound.Params.Location))
					{
						var text = Lang.Get(mapping.CaptionKey);
						if (text == mapping.CaptionKey)
							Api?.Logger.Warning($"[ClosedCaptions] Text not found for sound '{sound.Params.Location}' ({mapping.CaptionKey})");

						var position = Vec3f.Zero;
						if (!sound.Params.RelativePosition)
							position = sound.Params.Position;

						caption = new Caption(
							sound,
							text,
							CaptionManager.Api.ElapsedMilliseconds,
							sound.Params.Volume,
							sound.Params.RelativePosition,
							position,
							mapping.Tags,
							mapping.Flags,
							mapping.Group,
							mapping.Icon);

						return;
					}
				}

				// It did not have a better match. Store the group for now
				// in case for some reason there's another match group that
				// has a better match.
				partialMatch ??= matchGroup;
			}
		}

		// If partialMatch is null, the sound was not matched, in which case it is an unknown sound.
		caption = new Caption(
			sound,
			partialMatch == null ? Lang.Get("closedcaptions:unknown-sound") : Lang.Get(partialMatch.DefaultKey),
			CaptionManager.Api.ElapsedMilliseconds,
			1f,
			true,
			Vec3f.Zero,
			CaptionTags.None,
			CaptionFlags.None,
			null,
			null);
	}
}