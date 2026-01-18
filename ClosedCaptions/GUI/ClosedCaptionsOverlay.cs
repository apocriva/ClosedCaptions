using System.Collections;
using System.Collections.Generic;
using System.Text;
using ApacheTech.Common.Extensions.Harmony;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ClosedCaptions.GUI;

public class ClosedCaptionsOverlay : HudElement
{

	private readonly CairoFont _font;
	private readonly Vec4f _fontColor = new(0.91f, 0.87f, 0.81f, 1f);

	public ClosedCaptionsOverlay(ICoreClientAPI capi) : base(capi)
	{
		_font = InitFont();
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
			.WithAlignment(EnumDialogArea.CenterMiddle)
			.WithFixedOffset(0, 0);
		ElementBounds bgBounds = ElementBounds.Fill.WithSizing(ElementSizing.FitToChildren);
		var bgColor = new double[] { 0.0, 0.0, 0.0, 0.2 };

		var guiComposer = capi.Gui.CreateCompo("closedCaptions", dialogBounds)
			.AddGameOverlay(bgBounds, bgColor)
			.BeginChildElements();
		
		ElementBounds textBounds = ElementBounds.FixedSize(200, 200);

		var activeSounds = Gantry.Core.ApiEx.ClientMain.GetField<Queue<ILoadedSound>>("ActiveSounds");
		guiComposer.AddStaticText(string.Join(" ", activeSounds), _font, textBounds);

		guiComposer.EndChildElements();

		SingleComposer = guiComposer.Compose();
	}
}