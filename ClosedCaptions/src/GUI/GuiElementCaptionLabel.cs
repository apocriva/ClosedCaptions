using System;
using Cairo;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace ClosedCaptions.GUI;

public class GuiElementCaptionLabel : GuiElement
{
	private static readonly int IconTextureSize = 64;
	private static readonly double IconSize = IconTextureSize;
	private static readonly double IconRenderScale = 0.7;
	private static readonly string MusicNoteFilename = "textures/icons/musicnote.svg";

	public Caption Caption { get => _caption; }
	private float _lastGlitchStrength = 0f;
	private string _captionText;
	private readonly Caption _caption;
	private readonly CairoFont _font;
	private LoadedTexture _baseTexture;
	private LoadedTexture _textTexture;
	private LoadedTexture _arrowTexture;
	private LoadedTexture? _musicNoteTexture;
	private readonly DummySlot? _dummySlot;
	private readonly Vec4f _textColor = new(1f, 1f, 1f, 1f);

	private string _debugText = "";
	private LoadedTexture _debugTexture;

	public string DebugText
	{
		get => _debugText;
		set
		{
			_debugText = value;
			api.Gui.TextTexture.GenOrUpdateTextTexture(
				_debugText, CairoFont.WhiteSmallText().WithSlant(FontSlant.Italic),
				ref _debugTexture);
		}
	}

	private float _baseOpacity = 1f;
	private float _textOpacity = 1f;
	private float? _angle = null;

	public GuiElementCaptionLabel(ICoreClientAPI capi, Caption caption, CairoFont font, ElementBounds bounds) : base(capi, bounds)
	{
		_caption = caption;
		_captionText = caption.Text;
		_lastGlitchStrength = 0f;
		_font = font;
		_baseTexture = new(capi);
		_textTexture = new(capi);
		_arrowTexture = new(capi);

		_debugTexture = new(capi);

		if (caption.Icon != null)
		{
			CollectibleObject? cobj = caption.Icon.GetCollectibleObject(capi);

			var dummyInventory = new DummyInventory(capi);
			var dummyStack = new ItemStack(cobj);
			_dummySlot = new DummySlot(dummyStack, dummyInventory);
		}

		_textColor = ClosedCaptionsModSystem.UserConfig.Color;
		bool hasPriorityColor = false;
		if (_caption.IsMusic)
		{
			_textColor = ClosedCaptionsModSystem.UserConfig.MusicColor;
			hasPriorityColor = true;
		}
		else if ((_caption.Tags & CaptionTags.Temporal) != 0)
		{
			_textColor = ClosedCaptionsModSystem.UserConfig.TemporalColor;
			hasPriorityColor = true;
		}
		else if ((_caption.Tags & CaptionTags.Rust) != 0)
		{
			_textColor = ClosedCaptionsModSystem.UserConfig.RustColor;
			hasPriorityColor = true;
		}
		
		if ((_caption.Tags & CaptionTags.Danger) != 0)
		{
			if (!hasPriorityColor)
				_textColor = ClosedCaptionsModSystem.UserConfig.DangerColor;
			if (ClosedCaptionsModSystem.UserConfig.DangerBold)
				_font = _font.Clone().WithWeight(FontWeight.Bold);
		}
		else if ((_caption.Tags & CaptionTags.Ambience) != 0 ||
			(_caption.Tags & CaptionTags.Weather) != 0)
		{
			if (!hasPriorityColor)
				_textColor = ClosedCaptionsModSystem.UserConfig.PassiveColor;
			if (ClosedCaptionsModSystem.UserConfig.PassiveItalic)
				_font = _font.Clone().WithSlant(FontSlant.Italic);
		}

		_musicNoteTexture = null;
		if (_caption.IsMusic)
		{
			_musicNoteTexture = api.Gui.LoadSvgWithPadding(new AssetLocation("closedcaptions", MusicNoteFilename), IconTextureSize, IconTextureSize, 5, ColorUtil.WhiteArgb);
		}
	}

	public void Update(float baseOpacity, float textOpacity, float? angle)
	{
		_baseOpacity = baseOpacity;
		_textOpacity = textOpacity;
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

		surface = new ImageSurface(Format.Argb32, IconTextureSize, IconTextureSize);
		context = new Context(surface);
		context.SetSourceRGBA(1, 1, 1, _font.Color[3]);
		context.MoveTo(IconSize / 2, 0);
		context.LineTo(IconSize * 0.8, IconSize * 0.8);
		context.LineTo(IconSize * 0.5, IconSize * 0.6);
		context.LineTo(IconSize * 0.2, IconSize * 0.8);
		context.LineTo(IconSize / 2, 0);
		context.Fill();
		generateTexture(surface, ref _arrowTexture);
		surface.Dispose();
		context.Dispose();
		
		// Text
		api.Gui.TextTexture.GenOrUpdateTextTexture(
			_captionText, _font,
			ref _textTexture);

		// Debug text
		api.Gui.TextTexture.GenOrUpdateTextTexture(
			_debugText, CairoFont.WhiteSmallText().WithSlant(FontSlant.Italic),
			ref _debugTexture);
	}

	public override void RenderInteractiveElements(float deltaTime)
	{
		var whiteColor = new Vec4f(1f, 1f, 1f, _baseOpacity);
		var textColor = _textColor.Clone();
		textColor.A *= _textOpacity * _baseOpacity;

		api.Render.RenderTexture(
			_baseTexture.TextureId,
			Bounds.renderX, Bounds.renderY,
			Bounds.OuterWidth, Bounds.OuterHeight, 50,
			whiteColor);

		if (ClosedCaptionsModSystem.UserConfig.ShowGlitch)
			UpdateGlitchText();
		api.Render.RenderTexture(
			_textTexture.TextureId,
			Bounds.renderX + (Bounds.OuterWidth - _textTexture.Width) / 2,
			Bounds.renderY + (Bounds.OuterHeight - _textTexture.Height) / 2,
			_textTexture.Width, _textTexture.Height, 55,
			textColor);

		if (_angle != null && ClosedCaptionsModSystem.UserConfig.DirectionIndicators != Config.CaptionDirectionIndicators.None)
		{
			var arrowRenderSize = _font.GetFontExtents().Height * IconRenderScale;
			if ((ClosedCaptionsModSystem.UserConfig.DirectionIndicators & Config.CaptionDirectionIndicators.Left) != 0)
			{
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
					whiteColor);
				api.Render.GlPopMatrix();
			}

			if ((ClosedCaptionsModSystem.UserConfig.DirectionIndicators & Config.CaptionDirectionIndicators.Right) != 0)
			{
				api.Render.GlPushMatrix();
				api.Render.GlTranslate(
					Bounds.renderX + Bounds.OuterWidth - Bounds.OuterHeight / 2,
					Bounds.renderY + Bounds.OuterHeight / 2,
					0);
				api.Render.GlRotate(_angle.Value, 0, 0, 1);
				api.Render.GlTranslate(-arrowRenderSize / 2, -arrowRenderSize / 2, 0);
				api.Render.RenderTexture(
					_arrowTexture.TextureId,
					0, 0,
					arrowRenderSize, arrowRenderSize, 55,
					whiteColor);
				api.Render.GlPopMatrix();
			}
		}

		if (_caption.IsMusic && _musicNoteTexture != null)
		{
			float iconRenderSize = (float)_font.GetFontExtents().Height;
			api.Render.RenderTexture(_musicNoteTexture.TextureId,
				Bounds.renderX + Bounds.OuterHeight / 2 - iconRenderSize / 2,
				Bounds.renderY + Bounds.OuterHeight / 2 - iconRenderSize / 2,
				iconRenderSize, iconRenderSize, 70,
				textColor);
			api.Render.RenderTexture(_musicNoteTexture.TextureId,
				Bounds.renderX + Bounds.OuterWidth - Bounds.OuterHeight / 2 - iconRenderSize / 2,
				Bounds.renderY + Bounds.OuterHeight / 2 - iconRenderSize / 2,
				iconRenderSize, iconRenderSize, 70,
				textColor);
		}
		else if (ClosedCaptionsModSystem.UserConfig.Icon != Config.CaptionIconIndicator.None && _dummySlot != null)
		{
			float iconRenderSize = (float)_font.GetFontExtents().Height;
			var iconX = ClosedCaptionsModSystem.UserConfig.Icon == Config.CaptionIconIndicator.Left
				? Bounds.renderX - iconRenderSize
				: Bounds.renderX + Bounds.OuterWidth + iconRenderSize;
			api.Render.RenderItemstackToGui(
				_dummySlot,
				iconX, Bounds.renderY + iconRenderSize / 2, 70,
				iconRenderSize, ColorUtil.ColorFromRgba(whiteColor), true, false, false);
		}

		if (ClosedCaptionsModSystem.UserConfig.DebugMode && !string.IsNullOrEmpty(_debugText))
		{
			api.Render.RenderTexture(
				_baseTexture.TextureId,
				Bounds.renderX + Bounds.OuterWidth + 10, Bounds.renderY,
				_debugTexture.Width, _debugTexture.Height, 50);
			api.Render.RenderTexture(
				_debugTexture.TextureId,
				Bounds.renderX + Bounds.OuterWidth + 10, Bounds.renderY,
				_debugTexture.Width, _debugTexture.Height, 60);
		}
	}

	public override void Dispose()
	{
		base.Dispose();
		_musicNoteTexture?.Dispose();
		_arrowTexture?.Dispose();
		_debugTexture?.Dispose();
		_textTexture?.Dispose();
		_baseTexture?.Dispose();
	}

	private void UpdateGlitchText()
	{
		bool changed = false;
		var strength = GetGlitchStrength();

		if (strength > 0f)
		{
			if (_lastGlitchStrength == 0f ||
				Math.Abs(strength - _lastGlitchStrength) > 0.1f)
			{
				_captionText = destabilizeText(_caption.Text, strength);
				_lastGlitchStrength = strength;
				changed = true;
			}
		}
		else if (strength == 0f && _lastGlitchStrength > 0f)
		{
			_captionText = _caption.Text;
			_lastGlitchStrength = 0f;
			changed = true;
		}

		if (changed)
		{
			api.Gui.TextTexture.GenOrUpdateTextTexture(
				_captionText, _font,
				ref _textTexture);
		}
	}

	private float GetGlitchStrength()
	{
		float strength = (api.Render.ShaderUniforms.GlitchStrength - 0.5f) * 2f;
		return Math.Max(0f, Math.Min(strength, 1f));
	}

	// From BehaviorTemporalStabilityAffected in vssurvivalmod
	private string destabilizeText(string text, float str)
	{
		//those always stay in the middle
		char[] zalgo_mid = new char[] {
				'\u0315', /*     ̕     */		'\u031b', /*     ̛     */		'\u0340', /*     ̀     */		'\u0341', /*     ́     */
				'\u0358', /*     ͘     */		'\u0321', /*     ̡     */		'\u0322', /*     ̢     */		'\u0327', /*     ̧     */
				'\u0328', /*     ̨     */		'\u0334', /*     ̴     */		'\u0335', /*     ̵     */		'\u0336', /*     ̶     */
				'\u034f', /*     ͏     */		'\u035c', /*     ͜     */		'\u035d', /*     ͝     */		'\u035e', /*     ͞     */
				'\u035f', /*     ͟     */		'\u0360', /*     ͠     */		'\u0362', /*     ͢     */		'\u0338', /*     ̸     */
				'\u0337', /*     ̷     */		'\u0361', /*     ͡     */		'\u0489' /*     ҉_     */
			};

		string text3 = "";
		for (int i = 0; i < text.Length; i++)
		{
			text3 += text[i];

			if (i < text.Length - 1 && zalgo_mid.Contains(text[i + 1]))
			{
				text3 += text[i + 1];
				i++;
				continue;
			}

			if (zalgo_mid.Contains(text[i]))
			{
				continue;
			}

			if (api.World.Rand.NextDouble() < str)
			{
				text3 += zalgo_mid[api.World.Rand.Next(zalgo_mid.Length)];
			}
		}

		return text3;
	}
}

public static partial class GuiComposerHelpers
{
	public static GuiComposer AddCaptionLabel(this GuiComposer composer, Caption caption, CairoFont font, ElementBounds bounds, string? key = null)
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