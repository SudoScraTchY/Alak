namespace Alak.Core.Linking;

/// <summary>
/// Fallback display name for a package when the platform cannot resolve an app label
/// (CAP-013 step 3). Deterministic and locale-agnostic.
/// </summary>
public static class PackageNameHumanizer
{
    private static readonly HashSet<string> s_genericSegments = new(StringComparer.Ordinal)
    {
        "com", "org", "net", "ir", "io", "co", "app", "apps", "android", "mobile",
        "client", "phone", "www", "release", "debug", "beta", "stable", "ui", "main",
        "frontend", "backend", "service", "services"
    };

    private static readonly Dictionary<string, string> s_known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["com.google.android.apps.messaging"] = "Messages",
        ["com.android.mms"] = "Messages",
        ["com.samsung.android.messaging"] = "Messages",
        ["com.google.android.gm"] = "Gmail",
        ["com.google.android.apps.photos"] = "Photos",
        ["com.instagram.android"] = "Instagram",
        ["com.spotify.music"] = "Spotify"
    };

    public static string Humanize(string? package)
    {
        if (string.IsNullOrWhiteSpace(package)) return string.Empty;
        if (s_known.TryGetValue(package, out var known)) return known;

        var segments = package.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s.Length >= 3 && !s_genericSegments.Contains(s))
            .ToArray();

        var candidate = segments
            .OrderByDescending(s => s.Length)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(candidate))
        {
            candidate = package.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        if (string.IsNullOrEmpty(candidate)) return package;

        return TitleCase(candidate);
    }

    private static string TitleCase(string value)
    {
        var cleaned = value.Replace('_', ' ').Replace('-', ' ');
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return value;

        var chars = new char[cleaned.Length];
        var index = 0;
        foreach (var word in words)
        {
            if (index > 0) chars[index++] = ' ';
            for (var i = 0; i < word.Length && index < chars.Length; i++)
            {
                chars[index++] = i == 0 ? char.ToUpperInvariant(word[i]) : word[i];
            }
        }

        return new string(chars, 0, index);
    }
}
