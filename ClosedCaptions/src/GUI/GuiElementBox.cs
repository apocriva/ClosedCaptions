using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ClosedCaptions.GUI;

public class GuiElementBox : GuiElement
{
	public float Opacity = 1f;

	private LoadedTexture _texture;

	public GuiElementBox(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds)
	{
		_texture = new LoadedTexture(api);
	}

	public override void ComposeElements(Context context, ImageSurface surface)
	{
		Bounds.CalcWorldBounds();
		api.Render.GetOrLoadTexture(new AssetLocation("textures/gui/backgrounds/soil.png"), ref _texture);
	}

	public override void RenderInteractiveElements(float deltaTime)
	{
		base.RenderInteractiveElements(deltaTime);
		var pattern = getPattern(api, dirtTextureName);
		api.Render.RenderTexture(
			_texture.TextureId,
			Bounds.renderX, Bounds.renderY,
			Bounds.InnerWidth, Bounds.InnerHeight,
			0,
			new Vec4f(0, 0, 0, Opacity));
	}
}