using Alak.Core.Linking;
using Alak.Core.Model;

namespace Alak.App.Services;

/// <summary>Non-Android fallback: humanized package name, no icon.</summary>
public sealed class FallbackAppInfoProvider : IAppInfoProvider
{
    public AppIdentity? Resolve(string package)
        => string.IsNullOrWhiteSpace(package)
            ? null
            : new AppIdentity(package, PackageNameHumanizer.Humanize(package), null);
}
