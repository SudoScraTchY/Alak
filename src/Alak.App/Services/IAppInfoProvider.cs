using Alak.Core.Model;

namespace Alak.App.Services;

/// <summary>
/// Resolves an app's display label and a small icon (CAP-013). Implementations must
/// be safe to call off the UI thread and must never perform network access.
/// </summary>
public interface IAppInfoProvider
{
    /// <summary>Returns null only when the package is blank; label/icon may still be null.</summary>
    AppIdentity? Resolve(string package);
}
