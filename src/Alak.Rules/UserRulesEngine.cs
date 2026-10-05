using Alak.Core.Model;
using Alak.Storage;
using System.Collections.Generic;
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

    private static bool Matches(UserRuleMatch match, string package, string? title, string? text, string? sender, string? channelId, string? accountEmail)
    {
        if (match.Package != null && !string.Equals(match.Package, package, StringComparison.OrdinalIgnoreCase))
            return false;
        if (match.TitleContains != null && !title?.Contains(match.TitleContains, StringComparison.OrdinalIgnoreCase) == true)
            return false;
        if (match.TextContains != null && !text?.Contains(match.TextContains, StringComparison.OrdinalIgnoreCase) == true)
            return false;
        if (match.SenderContains != null && !sender?.Contains(match.SenderContains, StringComparison.OrdinalIgnoreCase) == true)
            return false;
        if (match.ChannelId != null && !string.Equals(match.ChannelId, channelId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (match.AccountEmail != null && !string.Equals(match.AccountEmail, accountEmail, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }
}