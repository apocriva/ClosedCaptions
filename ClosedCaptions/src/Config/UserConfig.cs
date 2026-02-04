namespace ClosedCaptions.Config;

public class UserConfig
{
	public static readonly string Filename = "closedcaptions.json";

	public bool FilterWeather { get; set; } = false;
	public bool FilterSelf { get; set; } = false;
    public bool FilterWalk { get; set; } = false;

	public bool ShowIcons { get; set; } = true;
	public long MinimumDisplayDuration { get; set; } = 200;
	public long FadeOutDuration { get; set; } = 500;
	public int AttenuationRange { get; set; } = 16;
	public float MinimumAttenuationOpacity { get; set; } = 0.3f;
	public int GroupingRange { get; set; } = 10;
	public int GroupingMaxTime { get; set; } = 1500;

	public bool DebugMode { get; set; } = false;
}