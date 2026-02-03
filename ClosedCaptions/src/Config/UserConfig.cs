namespace ClosedCaptions.Config;

public class UserConfig
{
	public static readonly string Filename = "closedcaptions.json";

	public bool FilterWeather { get; set; } = false;
	public bool FilterSelf { get; set; } = false;
    public bool FilterWalk { get; set; } = false;

	public long MinimumDisplayDuration { get; set; } = 200;
	public long FadeOutDuration { get; set; } = 500;

	public bool DebugMode { get; set; } = false;
}