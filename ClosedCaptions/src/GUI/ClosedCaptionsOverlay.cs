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
using Cairo;

namespace ClosedCaptions.GUI;

public class ClosedCaptionsOverlay : HudElement
{
	public override double DrawOrder => -0.5;
	public override bool ShouldReceiveMouseEvents() => false;

	private CairoFont? _font;
	private readonly Vec4f _fontColor = new(0.91f, 0.87f, 0.81f, 1f);

	private readonly MatchConfig _matchConfig;

	private class CaptionLabel(CaptionManager.Caption caption, GuiElementBox boxElement, GuiElementDynamicText leftArrow, GuiElementRichtext label, GuiElementDynamicText rightArrow)
	{
		public CaptionManager.Caption Caption = caption;

		public GuiElementBox BoxElement = boxElement;
		public GuiElementDynamicText LeftArrowElement = leftArrow;
		public GuiElementRichtext LabelElement = label;
		public GuiElementDynamicText RightArrowElement = rightArrow;
	}

	private readonly List<CaptionLabel> _captionLabels = [];

	public ClosedCaptionsOverlay(ICoreClientAPI capi, MatchConfig matchConfig) : base(capi)
	{
		_matchConfig = matchConfig;
		BuildDialog();
	}

	public void Refresh()
	{
		BuildDialog();
	}

	public void Tick()
	{
	}

	public override void OnRenderGUI(float deltaTime)
	{
		foreach (var captionLabel in _captionLabels)
		{
			string leftArrow = "";
			string rightArrow = "";
			float opacity = 1f;
			GetIndicators(captionLabel.Caption, ref leftArrow, ref rightArrow, ref opacity);

			// TODO: These have to fade too!
			if (captionLabel.LeftArrowElement.GetText() != leftArrow)
				captionLabel.LeftArrowElement.SetNewText(leftArrow);
			if (captionLabel.RightArrowElement.GetText() != rightArrow)
				captionLabel.RightArrowElement.SetNewText(rightArrow);

			// Ideally we would like this to adjust the alpha of the element instead of
			// using VTML but here we are.
			captionLabel.LabelElement.SetNewText($"<font opacity=\"{opacity}\">{captionLabel.Caption.Text}</font>", _font);

			captionLabel.BoxElement.Opacity = opacity * ClosedCaptionsModSystem.UserConfig.CaptionBackgroundOpacity;
		}

		base.OnRenderGUI(deltaTime);
	}

	private void InitFont()
	{
		_font = new CairoFont()
			.WithColor([_fontColor.R, _fontColor.G, _fontColor.B, _fontColor.A])
			.WithFont(GuiStyle.StandardFontName)
			.WithOrientation(EnumTextOrientation.Center)
			.WithFontSize(ClosedCaptionsModSystem.UserConfig.FontSize)
			.WithWeight(FontWeight.Normal);
	}

	private void GetIndicators(CaptionManager.Caption caption, ref string leftArrow, ref string rightArrow, ref float opacity)
	{
		leftArrow = "";
		rightArrow = "";
		
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

		var distance = relativePosition.Length();
		if ((caption.Flags & CaptionManager.Flags.Directionless) == 0 &&
			!caption.Params.RelativePosition &&
			relativePosition.Length() > ClosedCaptionsModSystem.UserConfig.MinimumDirectionDistance)
		{
			if (angle >= 150f || angle <= -150f)
			{
				leftArrow = "v";
				rightArrow = "v";
			}
			else if (angle >= 45f)
			{
				leftArrow = "<";
			}
			else if (angle <= -45f)
			{
				rightArrow = ">";
			}
		}
		
		opacity = 1f;

		// Modulate opacity by sound distance.
		if (distance > ClosedCaptionsModSystem.UserConfig.AttenuationRange)
		{
			var span = ClosedCaptionsModSystem.UserConfig.AttenuationRange;
			var percent = (distance - ClosedCaptionsModSystem.UserConfig.AttenuationRange) / span * (1f - ClosedCaptionsModSystem.UserConfig.MinimumAttenuationOpacity);
			percent = MathF.Max(0f, MathF.Min(percent, 1f - ClosedCaptionsModSystem.UserConfig.MinimumAttenuationOpacity));
			opacity *= 1f - percent;
		}
		// Modulate opacity if the caption is fading out.
		if (caption.FadeOutStartTime > 0 &&
			capi.ElapsedMilliseconds > caption.FadeOutStartTime)
		{
			var percent = 1f - (float)(capi.ElapsedMilliseconds - caption.FadeOutStartTime) / ClosedCaptionsModSystem.UserConfig.FadeOutDuration;
			percent = MathF.Max(0f, MathF.Min(percent, 1f));
			opacity *= percent;

			leftArrow = rightArrow = "";
		}
		
		// string debugInfo = string.Empty;
		// if (ClosedCaptionsModSystem.UserConfig.DebugMode)
		// {
		// 	debugInfo =
		// 		$"<font size=\"10\" color=\"#999933\">{caption.Params.Location.Path[..caption.Params.Location.Path.LastIndexOf('/')]}/</font>" +
		// 		$"<font size=\"10\" color=\"#ffff33\" weight=\"bold\">{caption.Params.Location.GetName()}</font>" +
		// 		$"<font size=\"10\" color=\"#33ffff\"> type:{caption.Params.SoundType}</font>" +
		// 		//$"<font size=\"10\" color=\"#3333ff\"> vol:{(int)(caption.Params.Volume * 100):D}%</font>" +
		// 		(!caption.Params.RelativePosition ?
		// 			$"<font size=\"10\" color=\"#33ff33\"> rel!</font>" +
		// 			$"<font size=\"10\" color=\"#ffffff\"> distance:{relativePosition.Length():F0}</font>" +
		// 			$"<font size=\"10\" color=\"#ffffff\"> range:{caption.Params.Range}</font>"
		// 			// $"<font size=\"10\" color=\"#33ff33\"> forward:({forward.X:F1}, {forward.Z:F1})</font>" +
		// 			// $"<font size=\"10\" color=\"#3399ff\"> rel:({relativePosition.X:F1}, {relativePosition.Z:F1})</font>" +
		// 			// $"<font size=\"10\" color=\"#33ff33\"> angle:{angle}°</font>"
		// 			: "");
		// }
	}

	private void BuildDialog()
	{
		// TODO: Only re-init font if settings have changed.
		InitFont();

		_captionLabels.Clear();

		var captions = CaptionManager.GetSortedCaptions();
		if (!captions.Any())
		{
			if (IsOpened())
				TryClose();
			return;
		}

		ElementBounds dialogBounds =
			ElementStdBounds.AutosizedMainDialog
			.WithAlignment(EnumDialogArea.CenterBottom)
			.WithFixedAlignmentOffset(0.0, -ClosedCaptionsModSystem.UserConfig.DisplayOffset);

		SingleComposer = capi.Gui.CreateCompo("closedCaptions", dialogBounds);

		double fontHeight = _font!.GetFontExtents().Height * _font!.LineHeightMultiplier;
		double lineHeight = _font!.GetFontExtents().Height * _font!.LineHeightMultiplier + ClosedCaptionsModSystem.UserConfig.CaptionPaddingV * 2;
		int lineY = 0;
		foreach (var caption in captions)
		{
			ElementBounds textBounds = ElementBounds.Fixed(0, lineY)
				.WithAlignment(EnumDialogArea.CenterTop)
				.WithFixedSize(600, fontHeight);
			SingleComposer.AddRichtext(caption.Text, _font, textBounds, $"label{caption.ID}");
			var labelElement = SingleComposer.GetRichtext($"label{caption.ID}");
			labelElement.BeforeCalcBounds();
			textBounds.fixedWidth = labelElement.MaxLineWidth / RuntimeEnv.GUIScale + 1.0;

			ElementBounds boxBounds = ElementBounds.Fixed(0, lineY - ClosedCaptionsModSystem.UserConfig.CaptionPaddingV)
				.WithAlignment(EnumDialogArea.CenterTop)
				.WithFixedSize(600 + ClosedCaptionsModSystem.UserConfig.CaptionPaddingH * 2, lineHeight);
			SingleComposer.AddInteractiveElement(new GuiElementBox(capi, boxBounds), $"box{caption.ID}");
			var boxElement = (GuiElementBox)SingleComposer.GetElement($"box{caption.ID}");
			boxBounds.fixedWidth = textBounds.fixedWidth + ClosedCaptionsModSystem.UserConfig.CaptionPaddingH * 2;
			boxBounds.fixedHeight = textBounds.fixedHeight + ClosedCaptionsModSystem.UserConfig.CaptionPaddingV * 2;

			ElementBounds arrowBoundsL = ElementBounds.Fixed(0, lineY)
				.WithAlignment(EnumDialogArea.CenterTop)
				.WithFixedSize(lineHeight, lineHeight);
			SingleComposer.AddDynamicText("", _font, arrowBoundsL, $"arrowl{caption.ID}");
			var leftArrowElement = SingleComposer.GetDynamicText($"arrowl{caption.ID}");
			arrowBoundsL.fixedX = -textBounds.fixedWidth / 2 - lineHeight / 2;

			ElementBounds arrowBoundsR = ElementBounds.Fixed(0, lineY)
				.WithAlignment(EnumDialogArea.CenterTop)
				.WithFixedSize(lineHeight, lineHeight);
			SingleComposer.AddDynamicText("", _font, arrowBoundsR, $"arrowr{caption.ID}");
			var rightArrowElement = SingleComposer.GetDynamicText($"arrowr{caption.ID}");
			arrowBoundsR.fixedX = textBounds.fixedWidth / 2 + lineHeight / 2;

			lineY += (int)lineHeight + ClosedCaptionsModSystem.UserConfig.CaptionSpacing + ClosedCaptionsModSystem.UserConfig.CaptionPaddingV;

			_captionLabels.Add(new(caption, boxElement, leftArrowElement, labelElement, rightArrowElement));
		}

		try
		{
			SingleComposer.Compose();
			TryOpen();
		}
		catch (Exception e)
		{
			capi.Logger.Error(e);
		}
	}
}