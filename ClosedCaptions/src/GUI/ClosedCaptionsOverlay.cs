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

	private readonly SoundLabelMap _soundLabelMap;

	public ClosedCaptionsOverlay(ICoreClientAPI capi, SoundLabelMap soundLabelMap) : base(capi)
	{
		_font = InitFont();
		_soundLabelMap = soundLabelMap;
		BuildDialog();
	}

	public void Refresh()
	{
		BuildDialog();
	}

	public void Tick()
	{
		foreach (var caption in CaptionManager.GetCaptions())
		{
			var captionKey = "caption" + caption.ID.ToString();
			var element = SingleComposer.GetRichtext(captionKey);

			// if (element == null)
			// {
			// 	capi.Logger.Log(EnumLogType.Warning,
			// 		string.Format("Can't find caption element for {0} ({1})",
			// 		captionKey,
			// 		caption.LoadedSound.Params.Location));
			// 	continue;
			// }

			var label = BuildCaptionLabel(caption);
			element.SetNewText(label, _font);
		}
	}

	private CairoFont InitFont()
	{
		return new CairoFont()
			.WithColor(new double[] { _fontColor.R, _fontColor.G, _fontColor.B, _fontColor.A })
			.WithFont(GuiStyle.StandardFontName)
			.WithOrientation(EnumTextOrientation.Center)
			.WithFontSize(FontSize)
			.WithWeight(Cairo.FontWeight.Normal)
			.WithStroke(new double[] { 0, 0, 0, 0.5 }, 2);
	}

	private string BuildCaptionLabel(CaptionManager.Caption caption)
	{
		var sound = caption.LoadedSound;
		var player = capi.World.Player;
		var relativePosition = sound.Params.Position - player.Entity.Pos.XYZFloat;
		if (sound.Params.RelativePosition)
			relativePosition = sound.Params.Position;
		
		// Left or right?
		Vec3d forward = new(Math.Cos(-player.CameraYaw), 0, Math.Sin(-player.CameraYaw));
		relativePosition.Normalize();

		var dot = relativePosition.Dot(forward);
		string leftArrow = "";
		string rightArrow = "";
		if (dot >= 0.5 && dot < 0.9)
		{
			leftArrow = "&lt;";
		}
		else if (dot <= -0.5 && dot > -0.9)
		{
			rightArrow = "&gt;";
		}
		else if (dot >= 0.9 || dot >= 0.9)
		{
			leftArrow = "v";
			rightArrow = "v";
		}

		var label = string.Format("{1} {0} {2} <font size=\"10\"><i>{3} {4:F2} {5:F0} {6}</i></font>",
			caption.Text,
			leftArrow,
			rightArrow,
			caption.LoadedSound.Params.SoundType,
			caption.LoadedSound.PlaybackPosition,
			caption.LoadedSound.Params.Volume,
			caption.LoadedSound.Params.Location
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

		double currentY = 0;
		foreach (var caption in captions)
		{
			ElementBounds bounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0, currentY, 600, LineHeight);
			
			var text = BuildCaptionLabel(caption);
			var captionKey = "caption" + caption.ID.ToString();
			SingleComposer.AddRichtext(text, _font, bounds, captionKey);

			currentY += LineHeight;
		}

		SingleComposer.EndChildElements();
		SingleComposer.Compose();

		TryOpen();
	}
}