using Godot;

namespace DualOntologyWorldEngine.Game.Reasoning;

/// <summary>
/// Shared restrained theme for the investigation and reconstruction surfaces.
/// Set a control's ThemeTypeVariation to <c>PrimaryButton</c> for the single
/// emphasized action in a view.
/// </summary>
public static class ReasoningTheme
{
	public static Theme Create(Font font, bool light = false)
	{
		var background = new Color(light ? "e8e6df" : "141b20");
		var panel = new Color(light ? "f0eee7" : "202a30");
		var text = new Color(light ? "293a3d" : "e5ebe8");
		var muted = new Color(light ? "6f7b76" : "9caea9");
		var accent = new Color(light ? "6f8f80" : "a9c4b6");
		var accentStrong = new Color(light ? "587667" : "c1d9ca");
		var border = new Color(light ? "c9cbc4" : "34434a");
		var selected = new Color(accent, 0.28f);

		var theme = new Theme
		{
			DefaultFont = font,
			DefaultFontSize = 16,
		};

		SetLabel(theme, font, text, muted);
		SetPanel(theme, panel, border);
		SetRichText(theme, font, text, muted);
		SetButtons(theme, font, panel, text, muted, accent, accentStrong, border);
		SetEditors(theme, font, panel, text, muted, accent, selected, border);
		SetPopup(theme, font, panel, text, muted, accent, border);
		SetScrollbars(theme, panel, accent, border);

		theme.SetConstant("separation", "BoxContainer", 12);
		theme.SetConstant("separation", "HBoxContainer", 12);
		theme.SetConstant("separation", "VBoxContainer", 12);
		theme.SetConstant("separation", "GridContainer", 12);

		// A variation keeps the primary action explicit at the call site while
		// inheriting the same spacing and focus treatment as every other button.
		theme.SetTypeVariation("PrimaryButton", "Button");
		var primaryNormal = ButtonBox(accent, accentStrong, accentStrong, text, border);
		var primaryHover = ButtonBox(accentStrong, accentStrong, accentStrong, text, accentStrong);
		theme.SetStylebox("normal", "PrimaryButton", primaryNormal);
		theme.SetStylebox("hover", "PrimaryButton", primaryHover);
		theme.SetStylebox("pressed", "PrimaryButton", ButtonBox(accentStrong, accentStrong, accentStrong, text, accentStrong));
		theme.SetStylebox("hover_pressed", "PrimaryButton", ButtonBox(accentStrong, accentStrong, accentStrong, text, accentStrong));
		theme.SetStylebox("disabled", "PrimaryButton", ButtonBox(panel, panel, panel, muted, border));
		theme.SetStylebox("focus", "PrimaryButton", FocusBox(accent));
		var primaryText = new Color(light ? "ffffff" : "172b25");
		theme.SetColor("font_color", "PrimaryButton", primaryText);
		theme.SetColor("font_hover_color", "PrimaryButton", primaryText);
		theme.SetColor("font_pressed_color", "PrimaryButton", primaryText);
		theme.SetColor("font_hover_pressed_color", "PrimaryButton", primaryText);
		theme.SetColor("font_disabled_color", "PrimaryButton", muted);
		theme.SetColor("font_focus_color", "PrimaryButton", primaryText);

		// Kept in the factory so every scene gets the same neutral canvas even
		// when it does not add a background control of its own.
		theme.SetColor("background_color", "TextEdit", background);
		theme.SetColor("background_color", "CodeEdit", background);
		return theme;
	}

	private static void SetLabel(Theme theme, Font font, Color text, Color muted)
	{
		foreach (var type in new[] { "Label", "Button", "OptionButton", "RichTextLabel", "TextEdit", "CodeEdit", "PopupMenu" })
		{
			theme.SetFont("font", type, font);
			theme.SetColor("font_color", type, text);
		}

		theme.SetColor("font_shadow_color", "Label", new Color(0, 0, 0, 0));
		theme.SetColor("font_outline_color", "Label", new Color(0, 0, 0, 0));
		theme.SetColor("font_color", "Label", text);
		theme.SetColor("font_disabled_color", "Label", muted);
		theme.SetColor("default_color", "RichTextLabel", text);
		theme.SetColor("font_selected_color", "RichTextLabel", text);
	}

	private static void SetPanel(Theme theme, Color panel, Color border)
	{
		theme.SetStylebox("panel", "PanelContainer", PanelBox(panel, border));
		theme.SetStylebox("panel", "Panel", PanelBox(panel, border));
	}

	private static void SetRichText(Theme theme, Font font, Color text, Color muted)
	{
		theme.SetFont("normal_font", "RichTextLabel", font);
		theme.SetColor("default_color", "RichTextLabel", text);
		theme.SetColor("font_color", "RichTextLabel", text);
		theme.SetColor("font_selected_color", "RichTextLabel", text);
		theme.SetColor("font_placeholder_color", "RichTextLabel", muted);
		theme.SetStylebox("normal", "RichTextLabel", FlatBox(new Color(0, 0, 0, 0), 0));
	}

	private static void SetButtons(Theme theme, Font font, Color panel, Color text, Color muted, Color accent, Color accentStrong, Color border)
	{
		foreach (var type in new[] { "Button", "OptionButton" })
		{
			theme.SetFont("font", type, font);
			theme.SetColor("font_color", type, text);
			theme.SetColor("font_hover_color", type, text);
			theme.SetColor("font_pressed_color", type, text);
			theme.SetColor("font_hover_pressed_color", type, text);
			theme.SetColor("font_disabled_color", type, muted);
			theme.SetColor("font_focus_color", type, text);
			theme.SetStylebox("normal", type, ButtonBox(panel, panel, panel, text, border));
			theme.SetStylebox("hover", type, ButtonBox(new Color(panel, 1f), accent, panel, text, accent));
			theme.SetStylebox("pressed", type, ButtonBox(accent, accent, accent, text, accentStrong));
			theme.SetStylebox("hover_pressed", type, ButtonBox(accent, accent, accent, text, accentStrong));
			theme.SetStylebox("disabled", type, ButtonBox(panel, panel, panel, muted, border));
			theme.SetStylebox("focus", type, FocusBox(accent));
			theme.SetConstant("h_separation", type, 8);
			theme.SetConstant("outline_size", type, 0);
		}
	}

	private static void SetEditors(Theme theme, Font font, Color panel, Color text, Color muted, Color accent, Color selected, Color border)
	{
		foreach (var type in new[] { "TextEdit", "CodeEdit" })
		{
			theme.SetFont("font", type, font);
			theme.SetColor("font_color", type, text);
			theme.SetColor("font_readonly_color", type, muted);
			theme.SetColor("font_selected_color", type, text);
			theme.SetColor("font_placeholder_color", type, muted);
			theme.SetColor("caret_color", type, accent);
			theme.SetColor("caret_background_color", type, panel);
			theme.SetColor("selection_color", type, selected);
			theme.SetColor("current_line_color", type, new Color(accent, 0.08f));
			theme.SetColor("background_color", type, panel);
			theme.SetStylebox("normal", type, EditorBox(panel, border));
			theme.SetStylebox("read_only", type, EditorBox(panel, border));
			theme.SetStylebox("focus", type, FocusBox(accent));
			theme.SetConstant("outline_size", type, 0);
		}
	}

	private static void SetPopup(Theme theme, Font font, Color panel, Color text, Color muted, Color accent, Color border)
	{
		theme.SetFont("font", "PopupMenu", font);
		theme.SetColor("font_color", "PopupMenu", text);
		theme.SetColor("font_hover_color", "PopupMenu", text);
		theme.SetColor("font_disabled_color", "PopupMenu", muted);
		theme.SetColor("font_accelerator_color", "PopupMenu", muted);
		theme.SetStylebox("panel", "PopupMenu", PanelBox(panel, border));
		theme.SetStylebox("hover", "PopupMenu", FlatBox(new Color(accent, 0.22f), 6));
	}

	private static void SetScrollbars(Theme theme, Color panel, Color accent, Color border)
	{
		var grabber = GrabberTexture(accent);
		foreach (var type in new[] { "HScrollBar", "VScrollBar" })
		{
			theme.SetStylebox("scroll", type, FlatBox(new Color(border, 0.55f), 4));
			theme.SetStylebox("scroll_focus", type, FlatBox(new Color(border, 0.75f), 4));
			theme.SetStylebox("grabber", type, FlatBox(accent, 5));
			theme.SetStylebox("grabber_highlight", type, FlatBox(accent, 5));
			theme.SetStylebox("grabber_pressed", type, FlatBox(accent, 5));
			theme.SetIcon("grabber", type, grabber);
			theme.SetIcon("grabber_highlight", type, grabber);
		}

		var rail = FlatBox(new Color(border, 0.7f), 2); rail.ContentMarginTop = 2; rail.ContentMarginBottom = 2;
        theme.SetStylebox("slider", "HSlider", rail);
		theme.SetStylebox("grabber_area", "HSlider", FlatBox(new Color(accent, 0.30f), 4));
		theme.SetStylebox("grabber_area_highlight", "HSlider", FlatBox(new Color(accent, 0.55f), 4));
		theme.SetIcon("grabber", "HSlider", grabber);
		theme.SetIcon("grabber_highlight", "HSlider", grabber);
		theme.SetIcon("grabber_disabled", "HSlider", GrabberTexture(new Color(border, 0.7f)));
	}

	private static StyleBoxFlat ButtonBox(Color fill, Color hoverBorder, Color pressedFill, Color text, Color border)
	{
		var box = FlatBox(fill, 8);
		box.ContentMarginLeft = 12;
		box.ContentMarginRight = 12;
		box.ContentMarginTop = 9;
		box.ContentMarginBottom = 9;
		box.BorderColor = hoverBorder == fill ? border : hoverBorder;
		box.SetBorderWidthAll(1);
		return box;
	}

	private static StyleBoxFlat EditorBox(Color fill, Color border)
	{
		var box = FlatBox(fill, 8);
		box.BorderColor = border;
		box.SetBorderWidthAll(1);
		box.ContentMarginLeft = 12;
		box.ContentMarginRight = 12;
		box.ContentMarginTop = 9;
		box.ContentMarginBottom = 9;
		return box;
	}

	private static StyleBoxFlat PanelBox(Color fill, Color border)
	{
		var box = FlatBox(fill, 8);
		box.BorderColor = border;
		box.SetBorderWidthAll(1);
        box.ContentMarginLeft = 24; box.ContentMarginRight = 24; box.ContentMarginTop = 24; box.ContentMarginBottom = 24;
		return box;
	}

	private static StyleBoxFlat FocusBox(Color accent)
	{
		var box = FlatBox(new Color(0, 0, 0, 0), 8);
		box.BorderColor = accent;
		box.SetBorderWidthAll(1);
		return box;
	}

	private static StyleBoxFlat FlatBox(Color fill, int radius)
	{
		var box = new StyleBoxFlat { BgColor = fill };
		box.SetCornerRadiusAll(radius);
		return box;
	}

	private static ImageTexture GrabberTexture(Color color)
	{
		const int size = 11;
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		var center = (size - 1) / 2f;
		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var distance = (new Vector2(x, y) - new Vector2(center, center)).Length();
				image.SetPixel(x, y, distance <= center - 0.25f ? color : new Color(0, 0, 0, 0));
			}
		}
		return ImageTexture.CreateFromImage(image);
	}
}
