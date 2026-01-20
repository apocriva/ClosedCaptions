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

namespace ClosedCaptions.GUI;

public class ClosedCaptionsOverlay : HudElement
{
	public override double DrawOrder => 0.6;
	public override bool ShouldReceiveMouseEvents() => false;

	private static readonly float VOLUME_THRESHOLD = 0.05f;
	private static readonly double LINE_HEIGHT = 24;

	private readonly CairoFont _font;
	private readonly Vec4f _fontColor = new(0.91f, 0.87f, 0.81f, 1f);

	public ClosedCaptionsOverlay(ICoreClientAPI capi) : base(capi)
	{
		_font = InitFont();
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
			.WithOrientation(EnumTextOrientation.Left)
			.WithFontSize(16f)
			.WithWeight(Cairo.FontWeight.Normal)
			.WithStroke(new double[] { 0, 0, 0, 0.5 }, 2);
	}

	private void BuildDialog()
	{
		ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
			.WithAlignment(EnumDialogArea.LeftTop)
			.WithFixedOffset(0, 0);
		ElementBounds bgBounds = ElementBounds.Fill.WithSizing(ElementSizing.FitToChildren);
		var bgColor = new double[] { 0.0, 0.0, 0.0, 0.2 };

		// Get the player's position.
		var player = capi.World.Player;

		var activeSounds = capi.GetActiveSounds();
		if (activeSounds != null && player != null)
		{
			var guiComposer = capi.Gui.CreateCompo("closedCaptions", dialogBounds)
				.AddGameOverlay(bgBounds, bgColor)
				.BeginChildElements();

			double currentY = 0;

			ElementBounds bounds = ElementBounds.FixedOffseted(EnumDialogArea.LeftTop, 0, currentY, 600, LINE_HEIGHT);
			guiComposer.AddStaticText("Active Sounds:", _font, bounds);
			currentY += LINE_HEIGHT;

			var orderedSounds = activeSounds
				.Where(sound => !sound.IsPaused && sound.IsPlaying && sound.Params.Volume >= VOLUME_THRESHOLD)
				.OrderBy(sound =>
				{
					var relativePosition = sound.Params.Position - player.Entity.Pos.XYZFloat;
					if (sound.Params.RelativePosition)
						relativePosition = sound.Params.Position;
					return relativePosition.Length();
				});

			foreach (var sound in orderedSounds)
			{
				if (!sound.IsPlaying ||
					sound.IsPaused ||
					sound.Params.Volume < VOLUME_THRESHOLD)
					continue;

				bounds = ElementBounds.FixedOffseted(EnumDialogArea.LeftTop, 0, currentY, 300, LINE_HEIGHT);
				guiComposer.AddStaticText(sound.Params.Location, _font, bounds);
				bounds = ElementBounds.FixedOffseted(EnumDialogArea.LeftTop, 300, currentY, 100, LINE_HEIGHT);
				guiComposer.AddStaticText(sound.Params.SoundType.ToString(), _font, bounds);

				var relativePosition = sound.Params.Position - player.Entity.Pos.XYZFloat;
				if (sound.Params.RelativePosition)
					relativePosition = sound.Params.Position;
				bounds = ElementBounds.FixedOffseted(EnumDialogArea.LeftTop, 400, currentY, 200, LINE_HEIGHT);
				guiComposer.AddStaticText(MathF.Floor(relativePosition.Length()).ToString(), _font, bounds);

				currentY += LINE_HEIGHT;
			}

			guiComposer.EndChildElements();

			guiComposer.zDepth = 149f;
			SingleComposer = guiComposer.Compose();
		}
	}
}