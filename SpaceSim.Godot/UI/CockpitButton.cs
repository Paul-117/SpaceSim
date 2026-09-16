using Godot;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

internal static class CockpitButton
{
    public static Button Create(string text)
    {
        // Space remains the weapon key and must never activate a focused Jump button.
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeColorOverride("font_color", ViewSettings.Text);
        button.AddThemeColorOverride("font_hover_color", ViewSettings.Cyan);
        button.AddThemeColorOverride("font_disabled_color", ViewSettings.Muted);
        button.AddThemeStyleboxOverride("normal", Style(new Color("102432"), ViewSettings.Line));
        button.AddThemeStyleboxOverride("hover", Style(new Color("173b48"), ViewSettings.Cyan));
        button.AddThemeStyleboxOverride("pressed", Style(new Color("205062"), ViewSettings.Cyan));
        button.AddThemeStyleboxOverride("disabled", Style(new Color("0b151f"), new Color("192936")));
        return button;
    }

    private static StyleBoxFlat Style(Color background, Color border) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5
    };
}
