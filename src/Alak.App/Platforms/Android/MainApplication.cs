using Android.App;
using Android.Runtime;

namespace Alak.App;

[Application(
    Icon = "@mipmap/appicon",
    RoundIcon = "@mipmap/appicon_round",
    Theme = "@style/AppTheme",
    SupportsRtl = true)]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
