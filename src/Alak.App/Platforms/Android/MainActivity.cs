using Android.App;
using Android.Content.PM;
using Microsoft.Maui;

namespace Alak.App;

[Activity(
    Label = "Alak",
    MainLauncher = true,
    Exported = true,
    Theme = "@style/AppTheme",
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.Orientation |
        ConfigChanges.ScreenSize |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.ScreenLayout |
        ConfigChanges.Density |
        ConfigChanges.UiMode |
        ConfigChanges.KeyboardHidden |
        ConfigChanges.Keyboard)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleIntent(Intent);
    }

    protected override void OnNewIntent(Android.Content.Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    private void HandleIntent(Android.Content.Intent? intent)
    {
        var route = intent?.GetStringExtra("route");
        if (!string.IsNullOrEmpty(route))
        {
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(async () =>
            {
                if (Microsoft.Maui.Controls.Shell.Current != null)
                {
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync(route);
                }
            });
        }
    }
}
