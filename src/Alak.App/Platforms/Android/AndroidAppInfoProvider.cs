using System.Collections.Concurrent;
using Alak.App.Services;
using Alak.Core.Linking;
using Alak.Core.Model;
using Android.App;
using Android.Content.PM;

namespace Alak.App.Platforms.Android;

/// <summary>
/// CAP-013 resolution order: PackageManager label/icon, then a humanized package name.
/// No QUERY_ALL_PACKAGES is declared, so unresolved packages fall back gracefully.
/// Icons are downscaled to ≤ 96 px and cached in memory.
/// </summary>
public sealed class AndroidAppInfoProvider : IAppInfoProvider
{
    private readonly ConcurrentDictionary<string, AppIdentity?> _cache = new(StringComparer.Ordinal);

    public AppIdentity? Resolve(string package)
    {
        if (string.IsNullOrWhiteSpace(package)) return null;
        return _cache.GetOrAdd(package, ResolveCore);
    }

    private static AppIdentity? ResolveCore(string package)
    {
        string? label = null;
        byte[]? iconPng = null;

        try
        {
            var context = global::Android.App.Application.Context;
            var pm = context.PackageManager;
            if (pm != null)
            {
                var info = pm.GetApplicationInfo(package, 0);
                if (info != null)
                {
                    label = pm.GetApplicationLabel(info)?.ToString();

                    try
                    {
                        iconPng = AndroidIconEncoder.Encode(info.LoadIcon(pm));
                    }
                    catch
                    {
                        iconPng = null;
                    }
                }
            }
        }
        catch
        {
            // NameNotFoundException / visibility restrictions: fall through to humanized name.
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            label = PackageNameHumanizer.Humanize(package);
        }

        return string.IsNullOrWhiteSpace(label) && iconPng == null
            ? null
            : new AppIdentity(package, label, iconPng);
    }
}
