using Alak.Core.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Storage;

/// <summary>
/// Repository interface for user rules and app settings (spec §11).
/// </summary>
public interface IUserRulesRepository
{
    // User rules
    Task<List<UserRule>> GetAllRulesAsync();
    Task<UserRule?> GetRuleAsync(long id);
    Task<long> InsertRuleAsync(UserRule rule);
    Task UpdateRuleAsync(UserRule rule);
    Task DeleteRuleAsync(long id);
    Task ReorderRulesAsync(IReadOnlyList<long> ruleIdsInOrder);
    
    // App settings
    Task<List<AppSettings>> GetAllAppSettingsAsync();
    Task<AppSettings?> GetAppSettingsAsync(string package);
    Task UpsertAppSettingsAsync(AppSettings settings);
    
    // Global settings
    Task<GlobalSettings> GetGlobalSettingsAsync();
    Task UpdateGlobalSettingsAsync(GlobalSettings settings);
    
    // Sources (package + account/channel groups)
    Task<List<SourceGroup>> GetSourceGroupsAsync(int days = 30);
    Task UpsertSourceGroupAsync(string package, string? displayName, string? defaultCategory);
}

/// <summary>Group of notifications by (package, account|channel) for Sources screen (PER-010).</summary>
public sealed record SourceGroup(
    string Package,
    string DisplayName,
    long Count,
    string DefaultCategory);