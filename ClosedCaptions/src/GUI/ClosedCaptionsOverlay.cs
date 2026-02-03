using System.Collections;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using ClosedCaptions.Extensions;
using Vintagestory.Client.NoObf;
using System;
using System.Linq;
using Vintagestory.API.Config;
using ClosedCaptions.Config;
using Microsoft.VisualBasic;

namespace ClosedCaptions.GUI;

public class ClosedCaptionsOverlay : HudElement
{
	public override double DrawOrder => 0.6;
	public override bool ShouldReceiveMouseEvents() => false;

	private static readonly float VolumeThreshold = 0.05f;
	private static readonly float FontSize = 20f;
	private static readonly double LineHeight = 24;

	private readonly CairoFont _font;
	private readonly Vec4f _fontColor = new(0.91f, 0.87f, 0.81f, 1f);

    private readonly MatchConfig _matchConfig;

	private class CaptionLabel(CaptionManager.Caption caption, GuiElementRichtext richtext)
	{
		public CaptionManager.Caption Caption = caption;
		public GuiElementRichtext Richtext = richtext;
	}

	private readonly List<CaptionLabel> _captionlabels = [];

	public ClosedCaptionsOverlay(ICoreClientAPI capi, MatchConfig matchConfig) : base(capi)
	{
		_font = InitFont();
        _matchConfig = matchConfig;
		BuildDialog();
	}

	public void Refresh()
	{
		BuildDialog();
	}

	public void Tick()
	{
		foreach (var captionLabel in _captionlabels)
		{
			var label = BuildCaptionLabel(captionLabel.Caption, false);
			captionLabel.Richtext.SetNewText(label, _font);
		}
	}

	private CairoFont InitFont()
	{
		return new CairoFont()
			.WithColor([_fontColor.R, _fontColor.G, _fontColor.B, _fontColor.A])
			.WithFont(GuiStyle.StandardFontName)
			.WithOrientation(EnumTextOrientation.Center)
			.WithFontSize(FontSize)
			.WithWeight(Cairo.FontWeight.Normal)
			.WithStroke([0, 0, 0, 0.5], 2);
	}

	private string? BuildCaptionLabel(CaptionManager.Caption caption, bool force)
	{
		var player = capi.World.Player;
		var relativePosition = caption.Position - player.Entity.Pos.XYZFloat;
		if (caption.Params.RelativePosition)
			relativePosition = caption.Position;
		relativePosition.Y = 0f;
		var relativeDirection = relativePosition.Clone();
		relativeDirection.Normalize();
		
		// Left or right?
		Vec3f forward = new(MathF.Sin(player.CameraYaw), 0f, MathF.Cos(player.CameraYaw));
		var dot = relativeDirection.Dot(forward);
		var angle = MathF.Acos(dot) * 180f / MathF.PI;
		var det = relativeDirection.X * forward.Z - relativeDirection.Z * forward.X;
		if (det < 0)
			angle = -angle;

		string leftArrow = "";
		string rightArrow = "";
		if (!caption.Params.RelativePosition)
		{
			if (angle >= 150f || angle <= -150f)
			{
				leftArrow = "v";
				rightArrow = "v";
			}
			else if (angle >= 45f)
			{
				leftArrow = "&lt;";
			}
			else if (angle <= -45f)
			{
				rightArrow = "&gt;";
			}
		}

		// Is the caption fading?
		float opacity = 1f;
		if (caption.FadeOutStartTime > 0)
		{
			var percent = 1f - (float)(capi.ElapsedMilliseconds - caption.FadeOutStartTime) / ClosedCaptionsModSystem.UserConfig.FadeOutDuration;
			opacity *= percent;
			opacity = MathF.Max(0f, MathF.Min(opacity, 1f));
		}

		string debugInfo = string.Empty;
		if (ClosedCaptionsModSystem.UserConfig.DebugMode)
		{
			debugInfo =
				$"<font size=\"10\" color=\"#999933\">{caption.Params.Location.Path[..caption.Params.Location.Path.LastIndexOf('/')]}/</font>" +
				$"<font size=\"10\" color=\"#ffff33\" weight=\"bold\">{caption.Params.Location.GetName()}</font>" +
				$"<font size=\"10\" color=\"#33ffff\"> type:{caption.Params.SoundType}</font>" +
				//$"<font size=\"10\" color=\"#3333ff\"> vol:{(int)(caption.Params.Volume * 100):D}%</font>" +
				(!caption.Params.RelativePosition ?
					$"<font size=\"10\" color=\"#33ff33\"> rel!</font>" +
					$"<font size=\"10\" color=\"#ffffff\"> distance:{relativePosition.Length():F0}</font>"
					// $"<font size=\"10\" color=\"#33ff33\"> forward:({forward.X:F1}, {forward.Z:F1})</font>" +
					// $"<font size=\"10\" color=\"#3399ff\"> rel:({relativePosition.X:F1}, {relativePosition.Z:F1})</font>" +
					// $"<font size=\"10\" color=\"#33ff33\"> angle:{angle}°</font>"
					: "");
		}

		var label = string.Format("<font opacity=\"{1}\">{2} {0} {3}</font>{4}",
			caption.Text,
			opacity,
			leftArrow,
			rightArrow,
			debugInfo
			);

		return label;
	}

	private void BuildDialog()
	{
		var captions = CaptionManager.GetSortedCaptions();
		if (!captions.Any())
		{
			if (IsOpened())
				TryClose();
			return;
		}
			
		ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
			.WithAlignment(EnumDialogArea.CenterBottom)
			.WithFixedOffset(0, -200);
		ElementBounds bgBounds = ElementBounds.Fill.WithSizing(ElementSizing.FitToChildren);
		var bgColor = new double[] { 0.0, 0.0, 0.0, 0.2 };

		SingleComposer = capi.Gui.CreateCompo("closedCaptions", dialogBounds)
			.AddGameOverlay(bgBounds, bgColor)
			.BeginChildElements();

		_captionlabels.Clear();
		double currentY = 0;
		foreach (var caption in captions)
		{
			ElementBounds bounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0, currentY, 600, LineHeight);
			
			var text = BuildCaptionLabel(caption, true);
			var captionKey = "caption" + caption.ID.ToString();
			SingleComposer.AddRichtext(text, _font, bounds, captionKey);

			var richtext = SingleComposer.GetRichtext(captionKey);
			_captionlabels.Add(new(caption, richtext));

			currentY += LineHeight;
		}

		SingleComposer.EndChildElements();

        try
        {
            SingleComposer.Compose();
        }
		catch (Exception e)
		{
            capi.Logger.Error($"Caption exception, no captions? {e}");
        }

        TryOpen();
	}
}