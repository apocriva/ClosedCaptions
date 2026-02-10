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

	private readonly List<GuiElementCaptionLabel> _captionLabels = [];

	public ClosedCaptionsOverlay(ICoreClientAPI capi) : base(capi)
	{
		BuildDialog();
	}

	public void Refresh()
	{
		BuildDialog();
	}

	public override void OnRenderGUI(float deltaTime)
	{
		foreach (var captionLabel in _captionLabels)
		{
			float? angle = null;
			float opacity = 1f;
			GetIndicators(captionLabel.Caption, ref opacity, ref angle);
			captionLabel.Update(opacity, angle);
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
			.WithWeight(FontWeight.Normal)
			.WithStroke([0, 0, 0, 0.5], 1);
	}

	private void GetIndicators(Caption caption, ref float opacity, ref float? angle)
	{
		var player = capi.World.Player;
		var relativePosition = caption.Position - player.Entity.Pos.XYZFloat;
		if (caption.IsRelative)
			relativePosition = caption.Position;
		relativePosition.Y = 0f;
		var relativeDirection = relativePosition.Clone();
		relativeDirection.Normalize();
		
		// Left or right?
		Vec3f forward = new(MathF.Sin(player.CameraYaw), 0f, MathF.Cos(player.CameraYaw));
		var dot = relativeDirection.Dot(forward);
		angle = MathF.Acos(dot) * 180f / MathF.PI;
		var det = relativeDirection.X * forward.Z - relativeDirection.Z * forward.X;
		if (det > 0)
			angle = -angle;

		var distance = relativePosition.Length();
		if (!ClosedCaptionsModSystem.UserConfig.ShowDirection ||
			(caption.Flags & CaptionFlags.Directionless) != 0 ||
			caption.IsRelative ||
			(!caption.IsRelative &&
			relativePosition.Length() < ClosedCaptionsModSystem.UserConfig.MinimumDirectionDistance))
		{
			angle = null;
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

		// Sounds that have been playing a while may be dimmed.
		if (capi.ElapsedMilliseconds - caption.StartTime > ClosedCaptionsModSystem.UserConfig.DimTime)
		{
			opacity *= ClosedCaptionsModSystem.UserConfig.DimPercent;
		}

		// Modulate opacity if the caption is fading out.
		if (caption.FadeOutStartTime > 0 &&
			capi.ElapsedMilliseconds > caption.FadeOutStartTime)
		{
			var percent = 1f - (float)(capi.ElapsedMilliseconds - caption.FadeOutStartTime) / ClosedCaptionsModSystem.UserConfig.FadeOutDuration;
			percent = MathF.Max(0f, MathF.Min(percent, 1f));
			opacity *= percent;
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
			_font.AutoBoxSize(caption.Text, textBounds);
			textBounds.fixedWidth += fontHeight * 2 + ClosedCaptionsModSystem.UserConfig.CaptionPaddingH * 2;
			textBounds.fixedHeight = fontHeight + ClosedCaptionsModSystem.UserConfig.CaptionPaddingV * 2;
			SingleComposer.AddCaptionLabel(caption, _font, textBounds, $"label{caption.ID}");
			var label = SingleComposer.GetCaptionLabel($"label{caption.ID}");
			label.DebugText = $"{caption.AssetLocation}";
			_captionLabels.Add(label);
			
			lineY += (int)lineHeight + ClosedCaptionsModSystem.UserConfig.CaptionSpacing;
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