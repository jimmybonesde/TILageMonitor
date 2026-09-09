namespace TILageMonitor;

public static class ThemeService
{
    public static bool IsDark { get; private set; }

    public static void ApplyTheme(bool dark)
    {
        IsDark = dark;
        var resources = System.Windows.Application.Current.Resources;

        if (dark)
        {
            // Fluent dark gray surfaces
            SetBrush(resources, "WindowBackground", "#1A1D21");
            SetBrush(resources, "CardBackground", "#25282D");
            SetBrush(resources, "TextMain", "#F3F4F6");
            SetBrush(resources, "TextMuted", "#9CA3AF");
            SetBrush(resources, "BorderSubtle", "#3A3F47");
            SetBrush(resources, "ChipBackground", "#32363C");
            SetBrush(resources, "AccentButtonBackground", "#3B82F6");
            SetBrush(resources, "AccentButtonForeground", "#FFFFFF");
            SetBrush(resources, "AccentButtonHoverBackground", "#60A5FA");
            SetBrush(resources, "AccentButtonPressedBackground", "#2563EB");
            SetBrush(resources, "MessageChipBackground", "#3D2E1A");
            SetBrush(resources, "MessageChipForeground", "#FBBF24");
            SetBrush(resources, "ButtonBackground", "#2A2E34");
            SetBrush(resources, "ButtonForeground", "#E5E7EB");
            SetBrush(resources, "ButtonBorder", "#4B5563");
            SetBrush(resources, "ButtonHoverBackground", "#3A3F47");
            SetBrush(resources, "ButtonPressedBackground", "#4B5563");
            SetBrush(resources, "ScrollBarTrackBackground", "#1A1D21");
            SetBrush(resources, "ScrollBarThumbBackground", "#4B5563");
            SetBrush(resources, "ScrollBarThumbHoverBackground", "#6B7280");
            SetBrush(resources, "ScrollBarThumbPressedBackground", "#9CA3AF");
            // Ampel / status (heatmap-aligned)
            SetBrush(resources, "StatusOk", "#34D399");
            SetBrush(resources, "StatusPartial", "#FBBF24");
            SetBrush(resources, "StatusOutage", "#F87171");
            SetBrush(resources, "StatusMaintenance", "#38BDF8");
        }
        else
        {
            SetBrush(resources, "WindowBackground", "#F3F6FB");
            SetBrush(resources, "CardBackground", "#FFFFFF");
            SetBrush(resources, "TextMain", "#0F172A");
            SetBrush(resources, "TextMuted", "#64748B");
            SetBrush(resources, "BorderSubtle", "#E2E8F0");
            SetBrush(resources, "ChipBackground", "#F1F5F9");
            SetBrush(resources, "AccentButtonBackground", "#2563EB");
            SetBrush(resources, "AccentButtonForeground", "#FFFFFF");
            SetBrush(resources, "AccentButtonHoverBackground", "#1D4ED8");
            SetBrush(resources, "AccentButtonPressedBackground", "#1E40AF");
            SetBrush(resources, "MessageChipBackground", "#FFF7ED");
            SetBrush(resources, "MessageChipForeground", "#EA580C");
            SetBrush(resources, "ButtonBackground", "#FFFFFF");
            SetBrush(resources, "ButtonForeground", "#334155");
            SetBrush(resources, "ButtonBorder", "#CBD5E1");
            SetBrush(resources, "ButtonHoverBackground", "#F1F5F9");
            SetBrush(resources, "ButtonPressedBackground", "#E2E8F0");
            SetBrush(resources, "ScrollBarTrackBackground", "#F1F5F9");
            SetBrush(resources, "ScrollBarThumbBackground", "#CBD5E1");
            SetBrush(resources, "ScrollBarThumbHoverBackground", "#94A3B8");
            SetBrush(resources, "ScrollBarThumbPressedBackground", "#64748B");
            // Ampel / status (heatmap-aligned)
            SetBrush(resources, "StatusOk", "#228B22");
            SetBrush(resources, "StatusPartial", "#FF8C00");
            SetBrush(resources, "StatusOutage", "#B22222");
            SetBrush(resources, "StatusMaintenance", "#0284C8");
        }
    }

    private static void SetBrush(
        System.Windows.ResourceDictionary resources,
        string key,
        string hex)
    {
        var color = (System.Windows.Media.Color)
            System.Windows.Media.ColorConverter.ConvertFromString(hex)!;

        if (resources[key] is System.Windows.Media.SolidColorBrush existing &&
            !existing.IsFrozen)
        {
            existing.Color = color;
        }
        else
        {
            resources[key] = new System.Windows.Media.SolidColorBrush(color);
        }
    }
}
