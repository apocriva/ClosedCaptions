using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ClosedCaptions.GUI;

public class GuiElementCaptionLabel : GuiElement
{
	private static readonly int ArrowTextureSize = 64;
	private static readonly double ArrowSize = ArrowTextureSize;
	private static readonly double ArrowRenderScale = 0.7;

	public CaptionManager.Caption Caption { get => _caption; }
	private CaptionManager.Caption _caption;
	private CairoFont _font;
	private LoadedTexture _baseTexture;
	private LoadedTexture _textTexture;
	private LoadedTexture _arrowTexture;

	private float _opacity = 1f;
	private float? _angle = null;

	public GuiElementCaptionLabel(ICoreClientAPI capi, CaptionManager.Caption caption, CairoFont font, ElementBounds bounds) : base(capi, bounds)
	{
		_caption = caption;
		_font = font;
		_baseTexture = new LoadedTexture(capi);
		_textTexture = new LoadedTexture(capi);
		_arrowTexture = new LoadedTexture(capi);
	}

	public void Update(float opacity, float? angle)
	{
		_opacity = opacity;
		_angle = angle;
	}

	public override void ComposeElements(Context context, ImageSurface surface)
	{
		Bounds.CalcWorldBounds();

		// Background
		surface = new ImageSurface(Format.Argb32, 16, 16);
		context = new Context(surface);
		context.SetSourceRGBA(0, 0, 0, ClosedCaptionsModSystem.UserConfig.CaptionBackgroundOpacity);
		context.Rectangle(0, 0, 16, 16);
		context.Fill();
		generateTexture(surface, ref _baseTexture);
		surface.Dispose();
		context.Dispose();

		// Arrow indicator
		surface = new ImageSurface(Format.Argb32, ArrowTextureSize, ArrowTextureSize);
		context = new Context(surface);
		context.SetSourceRGBA(1, 1, 1, _font.Color[3]);
		context.MoveTo(ArrowSize / 2, 0);
		context.LineTo(ArrowSize * 0.8, ArrowSize * 0.7); 
		context.LineTo(ArrowSize * 0.2, ArrowSize * 0.7);
		context.LineTo(ArrowSize / 2, 0);
		context.Fill();
		generateTexture(surface, ref _arrowTexture);
		surface.Dispose();
		context.Dispose();

		// Text
		api.Gui.TextTexture.GenOrUpdateTextTexture(
			_caption.Text, _font,
			ref _baseTexture);
	}

	public override void RenderInteractiveElements(float deltaTime)
	{
		var renderColor = new Vec4f(1f, 1f, 1f, _opacity);

		api.Render.RenderTexture(
			_baseTexture.TextureId,
			Bounds.renderX, Bounds.renderY,
			Bounds.OuterWidth, Bounds.OuterHeight, 50,
			renderColor);

		api.Render.RenderTexture(
			_textTexture.TextureId,
			Bounds.renderX + (Bounds.OuterWidth - _textTexture.Width) / 2,
			Bounds.renderY + (Bounds.OuterHeight - _textTexture.Height) / 2,
			_textTexture.Width, _textTexture.Height, 55,
			renderColor);

		if (_angle != null)
		{
			var arrowRenderSize = _font.GetFontExtents().Height * ArrowRenderScale;
			api.Render.GlPushMatrix();
			api.Render.GlTranslate(
				Bounds.renderX + Bounds.OuterHeight / 2,
				Bounds.renderY + Bounds.OuterHeight / 2,
				0);
			api.Render.GlRotate(_angle.Value, 0, 0, 1);
			api.Render.GlTranslate(-arrowRenderSize / 2, -arrowRenderSize / 2, 0);
			api.Render.RenderTexture(
				_arrowTexture.TextureId,
				0, 0,
				arrowRenderSize, arrowRenderSize, 55,
				renderColor);
			api.Render.GlPopMatrix();
		}
	}

	public override void Dispose()
	{
		base.Dispose();
		_arrowTexture?.Dispose();
		_textTexture?.Dispose();
		_baseTexture?.Dispose();
	}
}

public static partial class GuiComposerHelpers
{
	public static GuiComposer AddCaptionLabel(this GuiComposer composer, CaptionManager.Caption caption, CairoFont font, ElementBounds bounds, string? key = null)
	{
		if (!composer.Composed)
		{
			composer.AddInteractiveElement(new GuiElementCaptionLabel(composer.Api, caption, font, bounds), key);
		}

		return composer;
	}

	public static GuiElementCaptionLabel GetCaptionLabel(this GuiComposer composer, string key)
	{
		return (GuiElementCaptionLabel)composer.GetElement(key);
	}
}