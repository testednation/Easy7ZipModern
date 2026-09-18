using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Easy7ZipModern.Models;

namespace Easy7ZipModern.Services;

public class ThemeService
{
    public sealed class Theme
    {
        public string Name { get; set; }
        public string Icon { get; set; }

        // chrome
        public string WindowBackground { get; set; }
        public string TitleBarBackground { get; set; }
        public string NavBackground { get; set; }
        public string PageBackground { get; set; }
        public string SurfaceBackground { get; set; }
        public string SurfaceAltBackground { get; set; }
        public string CardBackground { get; set; }
        public string DropZoneBackground { get; set; }
        public string LogBackground { get; set; }
        public string ListBackground { get; set; }

        // lines
        public string BorderBrush { get; set; }
        public string DividerBrush { get; set; }

        // text
        public string TextPrimary { get; set; }
        public string TextSecondary { get; set; }
        public string TextMuted { get; set; }

        // accents
        public string Accent { get; set; }
        public string AccentHover { get; set; }
        public string AccentText { get; set; }
        public string ButtonBackground { get; set; }
        public string ButtonHover { get; set; }
        public string ButtonPressed { get; set; }
        public string RowBackground { get; set; }
        public string RowAlternateBackground { get; set; }
        public string ColumnHeaderBackground { get; set; }
        public string StatusbarBackground { get; set; }
        public string TabActiveBackground { get; set; }
        public string TabHoverBackground { get; set; }
        public string Success { get; set; }
        public string Danger { get; set; }
    }

    private static Theme Dark() => new Theme
    {
        Name = "Dark",
        Icon = "🌙",
        WindowBackground = "#181818",
        TitleBarBackground = "#1E1E1E",
        NavBackground = "#1C1C1C",
        PageBackground = "#181818",
        SurfaceBackground = "#202020",
        SurfaceAltBackground = "#252525",
        CardBackground = "#1E1E1E",
        DropZoneBackground = "#1E1E1E",
        LogBackground = "#141414",
        ListBackground = "#141414",
        BorderBrush = "#2D2D30",
        DividerBrush = "#252525",
        TextPrimary = "#F0F0F0",
        TextSecondary = "#CCCCCC",
        TextMuted = "#888888",
        Accent = "#0091FF",
        AccentHover = "#33A7FF",
        AccentText = "#FFFFFF",
        ButtonBackground = "#2D2D30",
        ButtonHover = "#333336",
        ButtonPressed = "#0078D4",
        RowBackground = "#1C1C1C",
        RowAlternateBackground = "#202020",
        ColumnHeaderBackground = "#252526",
        StatusbarBackground = "#141414",
        TabActiveBackground = "#2A2D34",
        TabHoverBackground = "#252526",
        Success = "#00FF66",
        Danger = "#FF5555"
    };

    private static Theme Light() => new Theme
    {
        Name = "Light",
        Icon = "☀️",
        WindowBackground = "#F5F6F8",
        TitleBarBackground = "#E9EBEF",
        NavBackground = "#E4E7EC",
        PageBackground = "#F5F6F8",
        SurfaceBackground = "#FFFFFF",
        SurfaceAltBackground = "#FAFBFC",
        CardBackground = "#FFFFFF",
        DropZoneBackground = "#EEF3FA",
        LogBackground = "#0F1115",
        ListBackground = "#FFFFFF",
        BorderBrush = "#C9CED6",
        DividerBrush = "#E1E4E9",
        TextPrimary = "#1A1D23",
        TextSecondary = "#3A3F4A",
        TextMuted = "#6A7280",
        Accent = "#0067C0",
        AccentHover = "#0052A3",
        AccentText = "#FFFFFF",
        ButtonBackground = "#FFFFFF",
        ButtonHover = "#E8EBF0",
        ButtonPressed = "#C7D6E8",
        RowBackground = "#FFFFFF",
        RowAlternateBackground = "#F3F5F8",
        ColumnHeaderBackground = "#EDEFF3",
        StatusbarBackground = "#E9EBEF",
        TabActiveBackground = "#FFFFFF",
        TabHoverBackground = "#DDE1E8",
        Success = "#0E8A16",
        Danger = "#C42B1C"
    };

    private static Theme Midnight() => new Theme
    {
        Name = "Midnight",
        Icon = "🌌",
        WindowBackground = "#0D1220",
        TitleBarBackground = "#0A0E1A",
        NavBackground = "#0B101C",
        PageBackground = "#0D1220",
        SurfaceBackground = "#141A2C",
        SurfaceAltBackground = "#182036",
        CardBackground = "#141A2C",
        DropZoneBackground = "#101728",
        LogBackground = "#080B14",
        ListBackground = "#080B14",
        BorderBrush = "#24304C",
        DividerBrush = "#1B2338",
        TextPrimary = "#E8ECF6",
        TextSecondary = "#B9C2D8",
        TextMuted = "#6E7A96",
        Accent = "#4DA3FF",
        AccentHover = "#78BBFF",
        AccentText = "#0A0E1A",
        ButtonBackground = "#1C2438",
        ButtonHover = "#253050",
        ButtonPressed = "#4DA3FF",
        RowBackground = "#0F1526",
        RowAlternateBackground = "#131B2E",
        ColumnHeaderBackground = "#182036",
        StatusbarBackground = "#0A0E1A",
        TabActiveBackground = "#1B2745",
        TabHoverBackground = "#141C30",
        Success = "#3DDC97",
        Danger = "#FF6B7A"
    };

    private static Theme HighContrast() => new Theme
    {
        Name = "High Contrast",
        Icon = "⚡",
        WindowBackground = "#000000",
        TitleBarBackground = "#000000",
        NavBackground = "#000000",
        PageBackground = "#000000",
        SurfaceBackground = "#000000",
        SurfaceAltBackground = "#101010",
        CardBackground = "#000000",
        DropZoneBackground = "#000000",
        LogBackground = "#000000",
        ListBackground = "#000000",
        BorderBrush = "#FFFFFF",
        DividerBrush = "#FFFFFF",
        TextPrimary = "#FFFFFF",
        TextSecondary = "#FFFFFF",
        TextMuted = "#C8C8C8",
        Accent = "#FFFF00",
        AccentHover = "#FFFF99",
        AccentText = "#000000",
        ButtonBackground = "#000000",
        ButtonHover = "#1A1A1A",
        ButtonPressed = "#FFFF00",
        RowBackground = "#000000",
        RowAlternateBackground = "#1A1A1A",
        ColumnHeaderBackground = "#000000",
        StatusbarBackground = "#000000",
        TabActiveBackground = "#000000",
        TabHoverBackground = "#1A1A1A",
        Success = "#00FF00",
        Danger = "#FF0000"
    };

    private static readonly List<Theme> _themes = new List<Theme>
    {
        Dark(), Light(), Midnight(), HighContrast()
    };

    public static IReadOnlyList<Theme> All => _themes.AsReadOnly();

    public static Theme GetByName(string name)
    {
        string normalized = (name ?? "").Trim().ToLowerInvariant();
        return _themes.FirstOrDefault(t => t.Name.ToLowerInvariant() == normalized)
            ?? Dark();
    }

    public static Theme GetNext(Theme current)
    {
        int index = _themes.FindIndex(t => t.Name == current.Name);
        return _themes[(index + 1) % _themes.Count];
    }

    /// <summary>
    /// Applies the palette to the application-wide merged resources so every
    /// open window (and any window opened later) re-styles instantly.
    /// </summary>
    public static void Apply(Theme theme)
    {
        ResourceDictionary dict = Application.Current.Resources;
        SetBrush(dict, "Bg.Window", theme.WindowBackground);
        SetBrush(dict, "Bg.TitleBar", theme.TitleBarBackground);
        SetBrush(dict, "Bg.Nav", theme.NavBackground);
        SetBrush(dict, "Bg.Page", theme.PageBackground);
        SetBrush(dict, "Bg.Surface", theme.SurfaceBackground);
        SetBrush(dict, "Bg.SurfaceAlt", theme.SurfaceAltBackground);
        SetBrush(dict, "Bg.Card", theme.CardBackground);
        SetBrush(dict, "Bg.DropZone", theme.DropZoneBackground);
        SetBrush(dict, "Bg.Log", theme.LogBackground);
        SetBrush(dict, "Bg.List", theme.ListBackground);
        SetBrush(dict, "Brush.Border", theme.BorderBrush);
        SetBrush(dict, "Brush.Divider", theme.DividerBrush);
        SetBrush(dict, "Text.Primary", theme.TextPrimary);
        SetBrush(dict, "Text.Secondary", theme.TextSecondary);
        SetBrush(dict, "Text.Muted", theme.TextMuted);
        SetBrush(dict, "Accent", theme.Accent);
        SetBrush(dict, "Accent.Hover", theme.AccentHover);
        SetBrush(dict, "Accent.Text", theme.AccentText);
        SetBrush(dict, "Btn.Background", theme.ButtonBackground);
        SetBrush(dict, "Btn.Hover", theme.ButtonHover);
        SetBrush(dict, "Btn.Pressed", theme.ButtonPressed);
        SetBrush(dict, "Row.Background", theme.RowBackground);
        SetBrush(dict, "Row.Alt", theme.RowAlternateBackground);
        SetBrush(dict, "Col.Header", theme.ColumnHeaderBackground);
        SetBrush(dict, "Bg.Statusbar", theme.StatusbarBackground);
        SetBrush(dict, "Tab.Active", theme.TabActiveBackground);
        SetBrush(dict, "Tab.Hover", theme.TabHoverBackground);
        SetBrush(dict, "Brush.Success", theme.Success);
        SetBrush(dict, "Brush.Danger", theme.Danger);

        // Convenience: keeps non-converting code-behind (DataGrid defaults etc.)
        // roughly in sync with the active theme.
        ThemeBrushes.TextPrimary = new BrushConverter().ConvertFromString(theme.TextPrimary) as SolidColorBrush;
        ThemeBrushes.TextMuted = new BrushConverter().ConvertFromString(theme.TextMuted) as SolidColorBrush;
        ThemeBrushes.Accent = new BrushConverter().ConvertFromString(theme.Accent) as SolidColorBrush;

        // remember + broadcast
        try
        {
            AppSettings settings = AppSettings.Load();
            settings.ThemeName = theme.Name;
            settings.Save();
        }
        catch
        {
        }
    }

    private static void SetBrush(ResourceDictionary dict, string key, string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        if (dict.Contains(key))
        {
            dict[key] = brush;
        }
        else
        {
            dict.Add(key, brush);
        }
    }

    public static class ThemeBrushes
    {
        public static Brush TextPrimary;
        public static Brush TextMuted;
        public static Brush Accent;
    }
}
