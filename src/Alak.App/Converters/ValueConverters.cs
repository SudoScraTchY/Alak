using System;
using System.Globalization;
using Alak.Core.Model;
using Microsoft.Maui.Controls;

namespace Alak.App.Converters;

/// <summary>Converts an int count (or bool) to visibility: visible when > 0 (or true), unless parameter="invert".</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            int i => i > 0,
            bool b => b,
            _ => false
        };
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            visible = !visible;
        return visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Maps a package name to an app icon resource; unknown packages yield null (Image shows placeholder).</summary>
public class PackageToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string pkg && !string.IsNullOrWhiteSpace(pkg) ? $"app://{pkg}" : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRead)
        {
            // If isRead = true, use Regular (400); if false (unread), use SemiBold (600)
            return isRead ? FontAttributes.None : FontAttributes.Bold;
        }
        return FontAttributes.None;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto)
        {
            var diff = DateTimeOffset.Now - dto;
            
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            
            return dto.ToString("MMM d", culture);
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class CategoryLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string cat && !string.IsNullOrEmpty(cat))
        {
            var loc = Services.LocalizationManager.Instance;
            return cat.ToLowerInvariant() switch
            {
                "promo" or "promotions" or "offers" => loc["Cat_Promo"],
                "work" => loc["Cat_Work"],
                "people" => loc["Cat_People"],
                "money" => loc["Cat_Money"],
                "orders" => loc["Cat_Orders"],
                "security" => loc["Cat_Security"],
                "social" => loc["Cat_Social"],
                "updates" => loc["Cat_Updates"],
                "other" => loc["Cat_Other"],
                _ => char.ToUpperInvariant(cat[0]) + cat[1..]
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TintConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hexColor && hexColor.StartsWith("#"))
        {
            // Convert hex to 10% opacity tint
            if (uint.TryParse(hexColor.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out uint color))
            {
                var r = (color >> 16) & 0xFF;
                var g = (color >> 8) & 0xFF;
                var b = color & 0xFF;
                return Color.FromRgba(r, g, b, 0.1f).ToHex();
            }
        }
        return Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Maps a PromoCodeKind (or category name) to a solid color for code-kind chips.
/// Spec §12.2 palette; the wallet speaks in the gold accent family.</summary>
public class CategoryToColorConverter : IValueConverter
{
    private static readonly Color Promo = Color.FromArgb("#B77900");   // wallet gold
    private static readonly Color Gift = Color.FromArgb("#12895B");    // people green
    private static readonly Color Referral = Color.FromArgb("#8248D6");// offers violet
    private static readonly Color Voucher = Color.FromArgb("#C4581A");// orders orange
    private static readonly Color Unknown = Color.FromArgb("#6B7280");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            PromoCodeKind kind => kind switch
            {
                PromoCodeKind.Promo => Promo,
                PromoCodeKind.Gift => Gift,
                PromoCodeKind.Referral => Referral,
                PromoCodeKind.Voucher => Voucher,
                _ => Unknown
            },
            string s when Enum.TryParse<PromoCodeKind>(s, true, out var k) => Convert(k, targetType, parameter, culture),
            int i when Enum.IsDefined(typeof(PromoCodeKind), i) => Convert((PromoCodeKind)i, targetType, parameter, culture),
            _ => Unknown
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StepToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepParam && int.TryParse(stepParam, out int stepIndex))
        {
            // Completed/active step = People green (spec §12.2), inactive = hairline.
            if (stepIndex <= currentStep)
                return Color.FromArgb("#12895B");
            return Color.FromArgb("#DCE2E5");
        }
        return Color.FromArgb("#DCE2E5");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StepEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepParam && int.TryParse(stepParam, out int stepIndex))
        {
            return currentStep == stepIndex;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StepGreaterThanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepParam && int.TryParse(stepParam, out int stepIndex))
        {
            return currentStep > stepIndex;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToActiveTabColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = value is bool b && b;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            isActive = !isActive;

        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        var mode = parameter as string;

        if (mode is "stroke" or "stroke_invert")
        {
            if (isActive)
            {
                return theme == AppTheme.Dark ? Color.FromArgb("#7C9CFF") : Color.FromArgb("#2F5BE0"); // Mark.Work
            }
            return theme == AppTheme.Dark ? Color.FromArgb("#243339") : Color.FromArgb("#DCE2E5"); // Hairline
        }

        if (mode is "text" or "text_invert")
        {
            if (isActive)
            {
                return theme == AppTheme.Dark ? Color.FromArgb("#7C9CFF") : Color.FromArgb("#2F5BE0"); // Mark.Work
            }
            return theme == AppTheme.Dark ? Color.FromArgb("#A3B5BB") : Color.FromArgb("#4A5D64"); // Ink2/Txt2
        }

        // Background color
        if (isActive)
        {
            return theme == AppTheme.Dark ? Color.FromArgb("#26354A") : Color.FromArgb("#E3EAFD"); // Tint.Work
        }
        return Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}