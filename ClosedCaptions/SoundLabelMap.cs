using System;
using System.Collections.Generic;
using Vintagestory.API.Config;

namespace ClosedCaptions;

public class SoundLabelMap
{
	public delegate string? MatchFunc(string assetName);

	private readonly List<MatchFunc> _mappings = [];

	public void AddMapping(MatchFunc matchFunc)
	{
		_mappings.Add(matchFunc);
	}

	public string? FindCaptionForSound(string assetName)
	{
		foreach (var mapping in _mappings)
		{
			var match = mapping(assetName);

			if (!string.IsNullOrEmpty(match))
				return Lang.Get(match);
		}
		return null;
	}
}