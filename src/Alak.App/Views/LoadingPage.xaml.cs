using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Alak.App.Services;

namespace Alak.App.Views;

public partial class LoadingPage : ContentPage
{
    private readonly CancellationTokenSource _cts = new();
    private bool _hasTransitioned;

    public LoadingPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RunAnimationsAsync(_cts.Token);
    }

    private async Task RunAnimationsAsync(CancellationToken ct)
    {
        try
        {
            // 1. Start laser and particle loops immediately on Frame 1
            _ = RunLaserLoopAsync(ct);
            _ = RunParticleStreamsAsync(ct);

            // 2. Sequential card emergence from throat
            await Task.Delay(180, ct);
            _ = CardWallet.FadeTo(1, 280);
            _ = CardWallet.TranslateTo(0, 0, 380, Easing.CubicOut);

            await Task.Delay(220, ct);
            _ = CardOffers.FadeTo(1, 280);
            _ = CardOffers.TranslateTo(0, 0, 380, Easing.CubicOut);

            await Task.Delay(220, ct);
            _ = CardMoney.FadeTo(1, 280);
            _ = CardMoney.TranslateTo(0, 0, 380, Easing.CubicOut);

            // 3. Ample dwell time: cards remain visible and docked for ~2.4 seconds
            await Task.Delay(2400, ct);

            // 4. Smooth transition to main app
            await TransitionToMainAppAsync();
        }
        catch (OperationCanceledException)
        {
            // Canceled by user tap
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadingPage] Animation error: {ex.Message}");
            await TransitionToMainAppAsync();
        }
    }

    private async Task RunLaserLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await LaserBeam.TranslateTo(0, -22, 550, Easing.SinInOut);
                if (ct.IsCancellationRequested) break;
                await LaserBeam.TranslateTo(0, -32, 550, Easing.SinInOut);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunParticleStreamsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Symmetrically fall into the sieve throat (X=0, Y=140)
                var p1 = AnimateParticleAsync(ParticleCenter, 0, 0, 0, 138, 1600, ct);
                var p2 = AnimateParticleAsync(ParticleLeftMid, -35, 5, 0, 138, 1600, ct);
                var p3 = AnimateParticleAsync(ParticleRightMid, 35, 5, 0, 138, 1600, ct);
                var p4 = AnimateParticleAsync(ParticleOuterLeft, -65, 12, 0, 138, 1600, ct);
                var p5 = AnimateParticleAsync(ParticleOuterRight, 65, 12, 0, 138, 1600, ct);

                await Task.WhenAll(p1, p2, p3, p4, p5);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task AnimateParticleAsync(VisualElement el, double startX, double startY, double endX, double endY, uint duration, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        el.TranslationX = startX;
        el.TranslationY = startY;
        el.Opacity = 0;
        el.Scale = 0.6;

        // Fade in & scale up
        _ = el.FadeTo(1, duration / 4);
        _ = el.ScaleTo(0.9, duration / 2);

        // Move into throat
        await el.TranslateTo(endX, endY, duration, Easing.CubicIn);

        if (!ct.IsCancellationRequested)
        {
            // Sift through the mesh grill
            await Task.WhenAll(
                el.FadeTo(0, 180),
                el.ScaleTo(0.2, 180)
            );
        }
    }

    private void OnTapped(object? sender, EventArgs e)
    {
        _ = TransitionToMainAppAsync();
    }

    private async Task TransitionToMainAppAsync()
    {
        if (_hasTransitioned) return;
        _hasTransitioned = true;
        _cts.Cancel();

        try
        {
            await this.FadeTo(0, 180);
        }
        catch
        {
            // Ignore animation cancellation
        }

        var lang = Preferences.Get("selected_language", "fa");
        var isRtl = Preferences.Get("rtl_layout_enabled", lang == "fa");
        var shell = new AppShell
        {
            FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
        };

        if (Application.Current != null)
        {
            Application.Current.MainPage = shell;
        }

        // Check if biometric app lock is enabled
        if (Preferences.Get("app_lock_enabled", false))
        {
            _ = shell.Dispatcher.DispatchAsync(async () =>
            {
                var bioService = AppServices.Current?.GetService<IBiometricAuthService>();
                if (bioService != null && bioService.IsAvailable())
                {
                    var ok = await bioService.AuthenticateAsync("Alak Locked", "Authenticate to unlock Alak");
                    if (!ok)
                    {
#if ANDROID
                        Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
#endif
                    }
                }
            });
        }

        // Route pending intent or first-run onboarding
        if (!string.IsNullOrEmpty(App.PendingRoute))
        {
            var route = App.PendingRoute;
            App.PendingRoute = null;
            _ = shell.Dispatcher.DispatchAsync(async () =>
            {
                await Shell.Current.GoToAsync(route);
            });
        }
        else if (!Preferences.Get("onboarding_completed", false))
        {
            _ = shell.Dispatcher.DispatchAsync(async () =>
            {
                await Shell.Current.GoToAsync("//inbox");
                await Shell.Current.GoToAsync("onboarding");
            });
        }
    }
}
