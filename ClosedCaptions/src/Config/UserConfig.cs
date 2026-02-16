using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace ClosedCaptions.Config;

public enum CaptionAnchor
{
	LeftTop,
	CenterTop,
	RightTop,
	Left,
	Center,
	Right,
	LeftBottom,
	CenterBottom,
	RightBottom
}

[Flags]
public enum CaptionDirectionIndicators
{
	None = 0,
	Left = 1,
	Right = 2,
	Both = Left | Right,
}

public enum CaptionIconIndicator
{
	None,
	Left,
	Right,
}

public class UserConfig
{
	public static readonly string Filename = "closedcaptions.json";

	public bool ShowAmbience { get; set; } = true;
	public bool ShowAnimal { get; set; } = true;
	public bool ShowBlock { get; set; } = false;
	public bool ShowCombat { get; set; } = false;
	public bool ShowDanger { get; set; } = true;
	public bool ShowEnemy { get; set; } = true;
	public bool ShowEnvironment { get; set; } = false;
	public bool ShowInteraction { get; set; } = false;
	public bool ShowMachinery { get; set; } = true;
	public bool ShowRust { get; set; } = true;
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
	public float MinimumAttenuationOpacity { get; set; } = 0.5f;
	public int GroupingRange { get; set; } = 5;
	public int GroupingMaxTime { get; set; } = 1500;

	public CaptionAnchor ScreenAnchor { get; set; } = CaptionAnchor.RightBottom;
	public CaptionAnchor CaptionAnchor { get; set; } = CaptionAnchor.RightTop;

	public Vec2i DisplayOffset { get; set; } = new(-400, -300);
	public int FontSize { get; set; } = 20;
	public float CaptionBackgroundOpacity { get; set; } = 0.5f;
	public int CaptionPaddingH { get; set; } = 0;
	public int CaptionPaddingV { get; set; } = 0;
	public int CaptionSpacing { get; set; } = 1;
	public CaptionDirectionIndicators DirectionIndicators { get; set; } = CaptionDirectionIndicators.Right;
	public CaptionIconIndicator Icon { get; set; } = CaptionIconIndicator.Right;
	public Vec4f Color { get; set; } = new(1f, 1f, 1f, 1f);
	public Vec4f DangerColor { get; set; } = new(1f, 0.75f, 0.25f, 1f);
	public bool DangerBold { get; set; } = true;
	public Vec4f RustColor { get; set; } = new(1.0f, 0.4f, 0.4f, 1f);
	public Vec4f TemporalColor { get; set; } = new(0.0f, 1.0f, 0.7f, 1f);
	public Vec4f PassiveColor { get; set; } = new(1f, 1f, 1f, 0.7f);
	public bool PassiveItalic { get; set; } = true;
	public bool ShowGlitch { get; set; } = true;

	public bool DebugMode { get; set; } = false;
}

public static class ConfigHelpers
{
	public static EnumDialogArea ToEnumDialogArea(this CaptionAnchor anchor)
	{
		return anchor switch
		{
			CaptionAnchor.LeftTop => EnumDialogArea.LeftTop,
			CaptionAnchor.CenterTop => EnumDialogArea.CenterMiddle,
			CaptionAnchor.RightTop => EnumDialogArea.RightTop,
			CaptionAnchor.Left => EnumDialogArea.LeftMiddle,
			CaptionAnchor.Center => EnumDialogArea.CenterMiddle,
			CaptionAnchor.Right => EnumDialogArea.RightMiddle,
			CaptionAnchor.LeftBottom => EnumDialogArea.LeftBottom,
			CaptionAnchor.CenterBottom => EnumDialogArea.CenterBottom,
			CaptionAnchor.RightBottom => EnumDialogArea.RightBottom,
			_ => EnumDialogArea.CenterMiddle,
		};
	}
}