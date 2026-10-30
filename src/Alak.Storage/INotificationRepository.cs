using Alak.Core.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Storage;

/// <summary>
/// Repository interface for notification CRUD operations (spec §6.6/§13).
/// </summary>
public interface INotificationRepository
{
    Task<IEnumerable<Notification>> GetAllAsync();
    Task<Notification?> GetByIdAsync(string id);
    Task AddAsync(Notification notification);
    Task MarkReadAsync(string id);
    Task DeleteAsync(string id);
    Task ClearAllAsync();

    /// <summary>
    /// Gets distinct (package, account|channel) sources from the last N days with counts.
    /// Used by the Sources screen (§13.6, PER-010).
    /// </summary>
    Task<List<SourceItem>> GetSourcesAsync(int days);

    /// <summary>
    /// Update category for a single notification.
    /// </summary>
    Task UpdateCategoryAsync(string id, string category);

    /// <summary>
    /// Batch update category for existing notifications of a package.
    /// </summary>
    Task<int> UpdateCategoryForPackageAsync(string package, string category);

    /// <summary>
    /// Keyset pagination ordered by posted_at DESC (STO-004 / WBP-01).
    /// </summary>
    Task<IReadOnlyList<Notification>> GetPageAsync(long? beforeUtcMs, int limit, string? category = null, CancellationToken ct = default);

    /// <summary>
    /// Full-text search querying notification_fts (PF-10 / WBP-01).
    /// </summary>
    Task<IReadOnlyList<Notification>> SearchFtsAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>
    /// Prune non-pinned notifications older than maxAge (STO-001 / WBP-01).
    /// </summary>
    Task<int> PruneExpiredAsync(TimeSpan maxAge, CancellationToken ct = default);

    /// <summary>
    /// Cache the resolved label and small icon for a package (CAP-013).
    /// </summary>
    Task UpsertAppInfoAsync(string package, string? label, byte[]? iconPng);

    /// <summary>
    /// Labels/icons for the requested packages (missing packages are omitted).
    /// </summary>
    Task<IReadOnlyDictionary<string, AppIdentity>> GetAppInfosAsync(IReadOnlyCollection<string> packages);

    /// <summary>
    /// Packages that still lack a resolved label, for the startup backfill.
    /// </summary>
    Task<IReadOnlyList<string>> GetPackagesWithoutLabelAsync(int limit);
}