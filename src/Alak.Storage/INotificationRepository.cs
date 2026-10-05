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
}