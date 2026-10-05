namespace Alak.App.Design;

/// <summary>
/// Design tokens per SPEC.md §12.2 Color tokens + §12.3 Typography.
/// Single source of truth for the design system.
/// </summary>
public sealed class DesignTokens
{
    // ===== LIGHT THEME TOKENS (spec §12.2) =====
    public static class Light
    {
        public const string Salt = "#F3F5F6";        // Page background
        public const string Paper = "#FFFFFF";       // Card / Stub / sheet surface
        public const string Ink = "#12252B";         // Primary text/icons (14.5:1 on salt, 15.9:1 on paper)
        public const string Ink2 = "#4A5D64";        // Secondary text (6.3:1 / 6.9:1)
        public const string Ink3 = "#5B6B72";        // Tertiary text, placeholders, timestamps (5.1:1 / 5.5:1)
        public const string Hairline = "#DCE2E5";    // Dividers, Stub tear-edge
        public const string AccentInk = "#12252B";   // Primary CTA fill (white text on ink = 15.8:1)
        public const string Error = "#B3261E";       // Destructive actions (6.0:1 / 6.5:1)

        // Category marks (light tints) — spec §12.2 "ink-on-tint" ≥ 7.4:1
        public const string PromoTint = "#E8F4FD";
        public const string WorkTint = "#E8F5E9";
        public const string PeopleTint = "#FFF3E0";
        public const string MoneyTint = "#E0F2F1";
        public const string OrdersTint = "#F3E5F5";
        public const string SecurityTint = "#FFEBEE";
        public const string SocialTint = "#E8EAF6";
        public const string UpdatesTint = "#ECEFF1";
        public const string OtherTint = "#F5F5F5";
    }

    // ===== DARK THEME TOKENS (spec §12.2) =====
    public static class Dark
    {
        public const string Night = "#0C1417";       // Page background
        public const string Surface = "#131E22";     // Raised surface, one step up
        public const string Raised = "#1B2A2F";      // Card / Stub / sheet, two steps up
        public const string Txt = "#E7EEF0";         // Primary text/icons (15.9:1 / 13.0:1)
        public const string Txt2 = "#A3B5BB";        // Secondary text (8.8:1 / 7.0:1)
        public const string Txt3 = "#81959B";        // Tertiary text
        public const string Hairline = "#243339";    // Dividers
        public const string Error = "#FF8A80";       // Destructive on dark (8.2:1 / 7.4:1)

        // Category marks (dark tints) — spec §12.2
        public const string PromoTint = "#152A3A";
        public const string WorkTint = "#152A20";
        public const string PeopleTint = "#332815";
        public const string MoneyTint = "#152A27";
        public const string OrdersTint = "#2A152A";
        public const string SecurityTint = "#3A1518";
        public const string SocialTint = "#1A1A2E";
        public const string UpdatesTint = "#1A2228";
        public const string OtherTint = "#242424";
    }

    // ===== CATEGORY MARK COLORS (spec §12.2 "mark" row) =====
    public static class CategoryMark
    {
        public const string Promo = "#1E88E5";       // Blue
        public const string Work = "#2E7D32";        // Green
        public const string People = "#EF6C00";      // Orange
        public const string Money = "#00897B";       // Teal
        public const string Orders = "#7B1FA2";      // Purple
        public const string Security = "#C62828";    // Red
        public const string Social = "#3949AB";      // Indigo
        public const string Updates = "#546E7A";     // Blue-grey
        public const string Other = "#616161";       // Grey
    }

    // ===== TYPOGRAPHY TOKENS (spec §12.3) =====
    public static class Typography
    {
        // Font families
        public const string Primary = "Vazirmatn";
        public const string Mono = "JetBrainsMono";

        // Font weights
        public const int Regular = 400;
        public const int Medium = 500;
        public const int SemiBold = 600;

        // Size/line-height scale (base 16px)
        // Display (empty states, onboarding headline): 28/36, Medium
        public static (double Size, double LineHeight) Display => (28, 36);
        // Title (screen header, section header): 20/28, Medium
        public static (double Size, double LineHeight) Title => (20, 28);
        // Body: 16/24 (Latin), 16/26 (Persian)
        public static (double Size, double LineHeight) BodyLatin => (16, 24);
        public static (double Size, double LineHeight) BodyPersian => (16, 26);
        // Body secondary: 14/22
        public static (double Size, double LineHeight) BodySecondary => (14, 22);
        // Label (button, tab, chip): 14/20, SemiBold
        public static (double Size, double LineHeight) Label => (14, 20);
        // Code / mono: 18/24, letter-spacing +0.5px, Medium
        public static (double Size, double LineHeight) Code => (18, 24);
        public const double CodeLetterSpacing = 0.5;
        // Caption: 12/18
        public static (double Size, double LineHeight) Caption => (12, 18);

        // Numerals: tabular lining figures for amounts/codes
        public const string TabularFigures = "tnum";
    }

    // ===== SPACING, RADIUS, ELEVATION (spec §12.5) =====
    public static class Spacing
    {
        public const int XS = 4;
        public const int SM = 8;
        public const int MD = 16;
        public const int LG = 24;
        public const int XL = 32;
        public const int XXL = 48;
    }

    public static class Radius
    {
        public const int SM = 8;    // Chip, badge
        public const int MD = 12;   // Card, Stub, sheet
        public const int LG = 16;   // Modal, bottom sheet
        public const int Full = 999; // Pill
    }

    public static class Elevation
    {
        public const int Level0 = 0;    // Flat (divider)
        public const int Level1 = 1;    // Raised surface
        public const int Level2 = 2;    // Card / Stub
        public const int Level3 = 3;    // Modal, FAB
        public const int Level4 = 4;    // Bottom sheet, dialog
    }

    // ===== MOTION (spec §12.6) =====
    public static class Motion
    {
        public const int Fast = 120;    // Tap feedback, chip toggle
        public const int Normal = 200;  // Page push, list item expand
        public const int Slow = 300;    // Modal enter, bottom sheet
        public const string Easing = "cubic-bezier(0.2, 0, 0, 1)"; // Material standard
    }

    // ===== STUB COMPONENT (spec §12.7) =====
    public static class Stub
    {
        public const int MinWidth = 280;
        public const int MaxWidth = 420;
        public const int Height = 140;
        public const int TearEdgeHeight = 8; // Torn paper effect
        public const int CornerRadius = 12;
    }

    // ===== RTL/BIDI (spec §12.8) =====
    public static class Bidi
    {
        // Layout mirroring happens automatically via FlowDirection
        // Text alignment: logical (start/end) not physical (left/right)
        // The design system uses logical properties throughout
    }

    // ===== ACCESSIBILITY (spec §12.9) =====
    public static class A11y
    {
        public const double MinTouchTarget = 48; // dp
        public const double MinContrastAA = 4.5; // body text
        public const double MinContrastAALarge = 3.0; // large text
        public const double MinContrastNonText = 3.0; // UI components
    }
}