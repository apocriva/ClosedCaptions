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

	public void Rebuild()
	{
		BuildDialog();
	}

	public override void OnRenderGUI(float deltaTime)
	{
		foreach (var captionLabel in _captionLabels)
		{
			float? angle = null;
			float baseOpacity = 1f;
			float textOpacity = 1f;
			GetIndicators(captionLabel.Caption, ref baseOpacity, ref textOpacity, ref angle);
			captionLabel.Update(baseOpacity, textOpacity, angle);
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

	private void GetIndicators(Caption caption, ref float baseOpacity, ref float textOpacity, ref float? angle)
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
		if (ClosedCaptionsModSystem.UserConfig.DirectionIndicators == CaptionDirectionIndicators.None ||
			(caption.Flags & CaptionFlags.Directionless) != 0 ||
			caption.IsRelative ||
			(!caption.IsRelative &&
			relativePosition.Length() < ClosedCaptionsModSystem.UserConfig.MinimumDirectionDistance))
		{
			angle = null;
		}
		
		baseOpacity = 1f;

		// Modulate opacity if the caption is fading out.
		if (caption.FadeOutStartTime > 0 &&
			capi.ElapsedMilliseconds > caption.FadeOutStartTime)
		{
			var percent = 1f - (float)(capi.ElapsedMilliseconds - caption.FadeOutStartTime) / ClosedCaptionsModSystem.UserConfig.FadeOutDuration;
			percent = MathF.Max(0f, MathF.Min(percent, 1f));
			baseOpacity *= percent;
		}

		textOpacity = 1f;

		// Sounds that have been playing a while may be dimmed.
		if (capi.ElapsedMilliseconds - caption.StartTime > ClosedCaptionsModSystem.UserConfig.DimTime)
		{
			textOpacity *= ClosedCaptionsModSystem.UserConfig.DimPercent;
		}

		// Modulate opacity by sound distance.
		if (distance > caption.AttenuationRange)
		{
			var span = caption.Range - caption.AttenuationRange;
			var percent = (distance - caption.AttenuationRange) / span * (1f - ClosedCaptionsModSystem.UserConfig.MinimumAttenuationOpacity);
			percent = MathF.Max(0f, MathF.Min(percent, 1f - ClosedCaptionsModSystem.UserConfig.MinimumAttenuationOpacity));
			textOpacity *= 1f - percent;
		}
	}

	private void BuildDialog()
	{
		// TODO: Only re-init font if settings have changed.
		InitFont();

		_captionLabels.Clear();

		var captions = CaptionManager.GetDisplayedCaptions();
		if (!captions.Any())
		{
			if (IsOpened())
				TryClose();
			return;
		}

		captions.Sort(Caption.CompareByDistance);

		ElementBounds dialogBounds =
			ElementStdBounds.AutosizedMainDialog
			.WithAlignment(ClosedCaptionsModSystem.UserConfig.ScreenAnchor.ToEnumDialogArea())
			.WithFixedAlignmentOffset(ClosedCaptionsModSystem.UserConfig.DisplayOffset.X, ClosedCaptionsModSystem.UserConfig.DisplayOffset.Y);

		SingleComposer = capi.Gui.CreateCompo("closedCaptions", dialogBounds);

		double fontHeight = _font!.GetFontExtents().Height * _font!.LineHeightMultiplier;
		double lineHeight = _font!.GetFontExtents().Height * _font!.LineHeightMultiplier + ClosedCaptionsModSystem.UserConfig.CaptionPaddingV * 2;
		int lineY = 0;
		foreach (var caption in captions)
		{
			ElementBounds textBounds = ElementBounds.Fixed(0, lineY)
				.WithAlignment(ClosedCaptionsModSystem.UserConfig.CaptionAnchor.ToEnumDialogArea())
				.WithFixedSize(600, fontHeight);
			_font.AutoBoxSize(caption.Text, textBounds);
			textBounds.fixedWidth += fontHeight * 2 + ClosedCaptionsModSystem.UserConfig.CaptionPaddingH * 2;
			textBounds.fixedHeight = fontHeight + ClosedCaptionsModSystem.UserConfig.CaptionPaddingV * 2;
			SingleComposer.AddCaptionLabel(caption, _font, textBounds, $"label{caption.ID}");
			var label = SingleComposer.GetCaptionLabel($"label{caption.ID}");
			label.DebugText = $"[id={caption.ID}] asset={caption.AssetLocation}";
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