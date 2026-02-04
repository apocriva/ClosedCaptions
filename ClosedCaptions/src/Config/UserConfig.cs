namespace ClosedCaptions.Config;

public class UserConfig
{
	public static readonly string Filename = "closedcaptions.json";

	public bool ShowAmbience { get; set; } = true;
	public bool ShowAnimal { get; set; } = true;
	public bool ShowBlock { get; set; } = true;
	public bool ShowCombat { get; set; } = true;
	public bool ShowDanger { get; set; } = true;
	public bool ShowEnemy { get; set; } = true;
	public bool ShowEnvironment { get; set; } = true;
	public bool ShowInteraction { get; set; } = true;
	public bool ShowTemporal { get; set; } = true;
	public bool ShowTool { get; set; } = true;
	public bool ShowVoice { get; set; } = true;
	public bool ShowWalk { get; set; } = true;
	public bool ShowWearable { get; set; } = true;
	public bool ShowWeather { get; set; } = true;

	public bool IncludeUntagged { get; set; } = false;

	// public bool ShowIcons { get; set; } = true;
	public long MinimumDisplayDuration { get; set; } = 200;
	public long FadeOutDuration { get; set; } = 500;
	public int AttenuationRange { get; set; } = 16;
	public float MinimumAttenuationOpacity { get; set; } = 0.3f;
	public int GroupingRange { get; set; } = 10;
	public int GroupingMaxTime { get; set; } = 1500;

	public bool DebugMode { get; set; } = false;
}