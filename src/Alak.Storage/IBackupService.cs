using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Alak.Storage;

public enum RestoreMode
{
    Merge = 0,
    Replace = 1
}

public sealed record BackupMetadata(
    int Version,
    DateTimeOffset CreatedAt,
    int NotificationCount,
    int UserRuleCount,
    int PromoCodeCount,
    bool HasAdaptiveWeights);

public interface IBackupService
{
    Task ExportBackupAsync(Stream destination, string passphrase, CancellationToken ct = default);
    Task<BackupMetadata> InspectBackupAsync(Stream source, string passphrase, CancellationToken ct = default);
    Task RestoreBackupAsync(Stream source, string passphrase, RestoreMode mode = RestoreMode.Replace, CancellationToken ct = default);
}
