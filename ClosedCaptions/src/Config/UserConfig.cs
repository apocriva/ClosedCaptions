using Vintagestory.API.MathTools;

namespace ClosedCaptions.Config;

public class UserConfig
{
	public static readonly string Filename = "closedcaptions.json";

	public bool ShowAmbience { get; set; } = false;
	public bool ShowAnimal { get; set; } = true;
	public bool ShowBlock { get; set; } = false;
	public bool ShowCombat { get; set; } = false;
	public bool ShowDanger { get; set; } = true;
	public bool ShowEnemy { get; set; } = true;
	public bool ShowEnvironment { get; set; } = false;
	public bool ShowInteraction { get; set; } = false;
	public bool ShowTemporal { get; set; } = true;
	public bool ShowTool { get; set; } = false;
	public bool ShowVoice { get; set; } = true;
	public bool ShowWalk { get; set; } = false;
	public bool ShowWearable { get; set; } = false;
	public bool ShowWeather { get; set; } = true;
	public bool ShowUnknown { get; set; } = false;

	public float MinimumDirectionDistance { get; set; } = 1.5f;
	public long MinimumDisplayDuration { get; set; } = 1000;
	public long DimTime { get; set; } = 5000;
	public float DimPercent { get; set; } = 0.7f;
	public long FadeOutDuration { get; set; } = 500;
	public int AttenuationRange { get; set; } = 16;
	public float MinimumAttenuationOpacity { get; set; } = 0.3f;
	public int GroupingRange { get; set; } = 5;
	public int GroupingMaxTime { get; set; } = 1500;

	public int DisplayOffset { get; set; } = 300;
	public int FontSize { get; set; } = 20;
	public float CaptionBackgroundOpacity { get; set; } = 0.5f;
	public int CaptionPaddingH { get; set; } = 0;
	public int CaptionPaddingV { get; set; } = 0;
	public int CaptionSpacing { get; set; } = 1;
	public bool ShowDirection { get; set; } = true;
	public bool ShowIcons { get; set; } = true;
	public Vec4f Color { get; set; } = new(1f, 1f, 1f, 1f);
	public bool DangerBold { get; set; } = true;
	public Vec4f DangerColor { get; set; } = new(1f, 0.75f, 0.25f, 1f);

	public bool DebugMode { get; set; } = false;
}