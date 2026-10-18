using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using CommunityToolkit.Maui;
using Alak.App.Design;
using Alak.App.Services;
using Alak.App.ViewModels;
using Alak.App.Views;
using Alak.Rules;
using Alak.Storage;
using Alak.Adaptive;
using Alak.Inference.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Microsoft.Maui.Storage;

namespace Alak.App;

public static class AppServices
{
    private static IServiceProvider? s_services;

    public static IServiceProvider? Current
    {
        get
        {
            if (s_services != null) return s_services;
            if (IPlatformApplication.Current?.Services != null)
                return s_services = IPlatformApplication.Current.Services;
#if ANDROID
            if (MauiApplication.Current?.Services != null)
                return s_services = MauiApplication.Current.Services;
#endif
            return null;
        }
        set => s_services = value;
    }
}

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureMauiHandlers(handlers =>
            {
                // ZXing.Net.MAUI 0.10.x has no Use-extension; register handlers manually.
                handlers.AddHandler(
                    typeof(ZXing.Net.Maui.Controls.CameraBarcodeReaderView),
                    typeof(ZXing.Net.Maui.CameraBarcodeReaderViewHandler));
                handlers.AddHandler(
                    typeof(ZXing.Net.Maui.Controls.BarcodeGeneratorView),
                    typeof(ZXing.Net.Maui.BarcodeGeneratorViewHandler));
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Vazirmatn-Regular.ttf", "Vazirmatn");
                fonts.AddFont("Vazirmatn-Bold.ttf", "VazirmatnBold");
                fonts.AddFont("Vazirmatn-Medium.ttf", "VazirmatnMedium");
                fonts.AddFont("Vazirmatn-SemiBold.ttf", "VazirmatnSemiBold");
                fonts.AddFont("JetBrainsMono-Medium.ttf", "JetBrainsMono");
            });

        // Design system resources (§12)
        builder.Services.AddSingleton<DesignTokens>();

        // Core services (inward dependency order)
        builder.Services.AddSingleton<IPackLoader, PackLoader>();
        builder.Services.AddSingleton<IRuleCompiler, RuleCompiler>();
        builder.Services.AddSingleton<INotificationRepository, NotificationRepository>();
        builder.Services.AddSingleton<IUserRulesRepository, UserRulesRepository>();
        builder.Services.AddSingleton<UserRulesEngine>();
        builder.Services.AddSingleton<INotificationIngestionService, NotificationIngestionService>();
        builder.Services.AddSingleton<IAdaptiveEngine, AdaptiveEngine>();
        builder.Services.AddSingleton<IInferenceEngine, InferenceEngineStub>(); // M8 will swap
        builder.Services.AddSingleton<IPromoCodeRepository, PromoCodeRepository>();

        // ViewModels
        builder.Services.AddTransient<InboxViewModel>();
        builder.Services.AddTransient<DetailViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<RulesViewModel>();
        builder.Services.AddTransient<StubDetailViewModel>();
        builder.Services.AddTransient<WalletViewModel>();
        builder.Services.AddTransient<ScanViewModel>();
        builder.Services.AddTransient<SourcesViewModel>();
        builder.Services.AddTransient<SourcesPage>();

        // Storage
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "alak.db");
        builder.Services.AddSingleton(new StorageConnection($"Data Source={dbPath};Mode=ReadWriteCreate;Password=alak_secure_key_2024"));

        var app = builder.Build();
        AppServices.Current = app.Services;
        return app;
    }
}