using Android.App;
using Android.Content.PM;
using Microsoft.Maui;

namespace Alak.App;

[Activity(
    Label = "Alak",
    MainLauncher = true,
    Exported = true,
    Theme = "@style/Maui.SplashTheme",
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
    }
}
