using System.Text.RegularExpressions;

namespace Alak.Core.Linking;

/// <summary>
/// Extracts and normalizes link domains from notification text without any I/O.
/// Only http/https links are accepted; IP literals, credentials, and malformed
/// hosts are rejected. Output is the registrable domain (e.g. www.shop.digikala.com
/// becomes digikala.com) so attribution can match links across subdomains.
/// </summary>
public static class SafeLinkParser
{
    public const int MaxLinksPerNotification = 10;
    private const int MaxCandidateLength = 2048;

    private static readonly Regex s_candidate = new(
        "(?:https?://|www\\.)[^\\s<>\"'`\\u0000-\\u001F]{2,2048}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly char[] s_trimChars =
        { '.', ',', ';', ':', '!', '?', ')', ']', '}', '>', '»', '"', '\'', '،', '؛', '؟', '…' };

    private static readonly HashSet<string> s_secondLevelSuffixes = new(StringComparer.Ordinal)
    {
        "co", "com", "net", "org", "gov", "ac", "sch", "id"
    };

    /// <summary>
    /// Scans the supplied fields for links and returns distinct registrable domains
    /// in first-seen order. Bounded to <see cref="MaxLinksPerNotification"/>.
    /// </summary>
    public static IReadOnlyList<string> ExtractDomains(
        string? title,
        string? text,
        string? bigText = null,
        IReadOnlyList<string>? lines = null)
    {
        var domains = new List<string>(MaxLinksPerNotification);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        Scan(title);
        Scan(text);
        Scan(bigText);
        if (lines != null)
        {
            foreach (var line in lines) Scan(line);
        }

        return domains;

        void Scan(string? value)
        {
            if (string.IsNullOrEmpty(value) || domains.Count >= MaxLinksPerNotification) return;

            foreach (Match match in s_candidate.Matches(value))
            {
                if (domains.Count >= MaxLinksPerNotification) return;

                var candidate = match.Value.Trim().TrimStart('(', '[', '{').TrimEnd(s_trimChars);
                if (candidate.Length > MaxCandidateLength) continue;
                if (TryGetDomain(candidate, out var domain) && seen.Add(domain))
                {
                    domains.Add(domain);
                }
            }
        }
    }

    /// <summary>
    /// Normalizes one candidate link to its registrable domain.
    /// </summary>
    public static bool TryGetDomain(string candidate, out string domain)
    {
        domain = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        var raw = candidate.Trim();
        if (raw.Length > MaxCandidateLength) return false;
        if (raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase) &&
            !raw.Contains("://", StringComparison.Ordinal))
        {
            raw = "http://" + raw;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;

        string host;
        try
        {
            host = uri.IdnHost;
        }
        catch
        {
            return false;
        }

        host = host.TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
        if (host.Length is < 4 or > 253) return false;

        try
        {
            if (Uri.CheckHostName(host) != UriHostNameType.Dns) return false;
        }
        catch
        {
            return false;
        }

        var labels = host.Split('.');
        if (labels.Length < 2) return false;
        foreach (var label in labels)
        {
            if (!IsValidLabel(label)) return false;
        }

        domain = GetRegistrableDomain(labels);
        return domain.Length > 0;
    }

    internal static string GetRegistrableDomain(string[] labels)
    {
        if (labels.Length <= 2) return string.Join('.', labels);

        var tld = labels[^1];
        var secondLevel = labels[^2];
        if (tld.Length == 2 && s_secondLevelSuffixes.Contains(secondLevel))
        {
            return string.Join('.', labels[^3..]);
        }

        return $"{secondLevel}.{tld}";
    }

    private static bool IsValidLabel(string label)
    {
        if (label.Length is 0 or > 63) return false;
        if (label[0] == '-' || label[^1] == '-') return false;

        foreach (var c in label)
        {
            var ok = c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
            if (!ok) return false;
        }

        return true;
    }
}
