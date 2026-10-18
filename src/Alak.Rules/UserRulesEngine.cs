using Alak.Core.Capture;
using Alak.Core.Model;
using Alak.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Alak.Rules;

/// <summary>
/// Evaluates user rules before Tier 1 classification (spec §11.1, PER-001..003).
/// </summary>
public sealed class UserRulesEngine
{
    private readonly IUserRulesRepository _repository;

    public UserRulesEngine(IUserRulesRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Evaluates user rules against a raw captured notification.
    /// </summary>
    public Task<UserRuleAction?> EvaluateAsync(RawNotification raw)
    {
        return EvaluateAsync(
            raw.Package,
            raw.Title,
            raw.Text,
            raw.ConversationTitle ?? raw.Title,
            raw.ChannelId,
            null);
    }

    /// <summary>
    /// Evaluates user rules against a notification.
    /// Returns the first matching rule's action, or null if no rule matches.
    /// </summary>
    public async Task<UserRuleAction?> EvaluateAsync(string package, string? title, string? text, string? sender, string? channelId, string? accountEmail)
    {
        var rules = await _repository.GetAllRulesAsync();
        var enabledRules = rules.Where(r => r.Enabled).OrderBy(r => r.Priority).ThenBy(r => r.Id);

        foreach (var rule in enabledRules)
        {
            if (Matches(rule.Match, package, title, text, sender, channelId, accountEmail))
            {
                return rule.Action;
            }
        }
        return null;
    }

    public static bool Matches(UserRuleMatch match, string package, string? title, string? text, string? sender, string? channelId, string? accountEmail)
    {
        if (match.Package != null && !string.Equals(match.Package, package, StringComparison.OrdinalIgnoreCase))
            return false;
        if (match.ChannelId != null && !string.Equals(match.ChannelId, channelId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (match.AccountEmail != null && !string.Equals(match.AccountEmail, accountEmail, StringComparison.OrdinalIgnoreCase))
            return false;

        // Title pattern
        var titlePattern = match.TitleRegex ?? match.TitleContains;
        var titleMode = match.TitleRegex != null ? RuleMatchMode.Regex : match.MatchMode;
        if (!StringMatches(title, titlePattern, titleMode, match.CaseSensitive))
            return false;

        // Text pattern
        var textPattern = match.TextRegex ?? match.TextContains;
        var textMode = match.TextRegex != null ? RuleMatchMode.Regex : match.MatchMode;
        if (!StringMatches(text, textPattern, textMode, match.CaseSensitive))
            return false;

        // Sender pattern
        var senderPattern = match.SenderRegex ?? match.SenderContains;
        var senderMode = match.SenderRegex != null ? RuleMatchMode.Regex : match.MatchMode;
        if (!StringMatches(sender, senderPattern, senderMode, match.CaseSensitive))
            return false;

        return true;
    }

    public static bool StringMatches(string? input, string? pattern, RuleMatchMode mode, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        if (input == null)
            return false;

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return mode switch
        {
            RuleMatchMode.Contains => input.Contains(pattern, comparison),
            RuleMatchMode.Exact => string.Equals(input, pattern, comparison),
            RuleMatchMode.StartsWith => input.StartsWith(pattern, comparison),
            RuleMatchMode.EndsWith => input.EndsWith(pattern, comparison),
            RuleMatchMode.Regex => EvaluateRegex(input, pattern, caseSensitive),
            _ => input.Contains(pattern, comparison)
        };
    }

    private static bool EvaluateRegex(string input, string pattern, bool caseSensitive)
    {
        try
        {
            var options = RegexOptions.CultureInvariant;
            if (!caseSensitive)
            {
                options |= RegexOptions.IgnoreCase;
            }

            return Regex.IsMatch(input, pattern, options, TimeSpan.FromMilliseconds(50));
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}