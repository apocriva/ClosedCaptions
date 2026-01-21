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

	public void Update()
	{
		BuildDialog();
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

	private void BuildDialog()
	{
		ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
			.WithAlignment(EnumDialogArea.CenterBottom)
			.WithFixedOffset(0, -200);
		ElementBounds bgBounds = ElementBounds.Fill.WithSizing(ElementSizing.FitToChildren);
		var bgColor = new double[] { 0.0, 0.0, 0.0, 0.2 };

		// Get the player's position.
		var player = capi.World.Player;

		var activeSounds = capi.GetActiveSounds();
		if (activeSounds != null && player != null)
		{
			var orderedSounds = activeSounds
				.Where(sound => !sound.IsPaused && sound.IsPlaying && sound.Params.Volume >= VolumeThreshold)
				.OrderBy(sound =>
				{
					if (sound.Params.Position == null)
						return 0f;

					var relativePosition = sound.Params.Position - player.Entity.Pos.XYZFloat;
					if (sound.Params.RelativePosition)
						relativePosition = sound.Params.Position;
					return -relativePosition.Length();
				});

			if (!orderedSounds.Any())
				return;
			
			var guiComposer = capi.Gui.CreateCompo("closedCaptions", dialogBounds)
				.AddGameOverlay(bgBounds, bgColor)
				.BeginChildElements();

			double currentY = 0;

			ElementBounds bounds;

			int captionIndex = 0;
			foreach (var sound in orderedSounds)
			{
				if (!sound.IsPlaying ||
					sound.IsPaused ||
					sound.Params.Volume < VolumeThreshold ||
					sound.Params.Location == null)
					continue;

				var captionText = _soundLabelMap.FindCaptionForSound(sound.Params.Location);
				if (string.IsNullOrEmpty(captionText))
					captionText = "[...]";
				captionText = string.Format("{0} <font size=\"10\" opacity=\"0.5\"><i>{1}</i></font>",
					captionText,
					sound.Params.Location
					);
				bounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0, currentY, 600, LineHeight);
				guiComposer.AddRichtext(captionText, _font, bounds);
				//bounds = ElementBounds.FixedOffseted(EnumDialogArea.LeftTop, 300, currentY, 100, LineHeight);
				//guiComposer.AddStaticText(sound.Params.SoundType.ToString(), _font, bounds);

				//var relativePosition = sound.Params.Position - player.Entity.Pos.XYZFloat;
				//if (sound.Params.RelativePosition)
				//	relativePosition = sound.Params.Position;
				//bounds = ElementBounds.FixedOffseted(EnumDialogArea.LeftTop, 400, currentY, 200, LineHeight);
				//guiComposer.AddStaticText(MathF.Floor(relativePosition.Length()).ToString(), _font, bounds);

				currentY += LineHeight;
				captionIndex++;
			}

			guiComposer.EndChildElements();
			SingleComposer = guiComposer.Compose();
		}
	}
}