using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using CommunityToolkit.Maui;
using Alak.App.Design;
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

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Vazirmatn-Regular.ttf", "Vazirmatn");
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

        // Storage
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "alak.db");
        builder.Services.AddSingleton(new StorageConnection($"Data Source={dbPath};Mode=ReadWriteCreate;Password=alak_secure_key_2024"));

        return builder.Build();
    }
}