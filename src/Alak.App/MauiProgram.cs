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
        builder.Services.AddSingleton<IAppDomainRepository, AppDomainRepository>();
#if ANDROID
        builder.Services.AddSingleton<IAppInfoProvider, Platforms.Android.AndroidAppInfoProvider>();
#else
        builder.Services.AddSingleton<IAppInfoProvider, FallbackAppInfoProvider>();
#endif
        builder.Services.AddSingleton<SourceMetadataBackfillService>();
        builder.Services.AddSingleton<IUserRulesRepository, UserRulesRepository>();
        builder.Services.AddSingleton<UserRulesEngine>();
        builder.Services.AddSingleton<INotificationIngestionService, NotificationIngestionService>();
        builder.Services.AddSingleton<IAdaptiveEngine, AdaptiveEngine>();
        builder.Services.AddSingleton<IInferenceEngine, LLamaInferenceEngine>();
        builder.Services.AddSingleton<IModelService, ModelService>();
        builder.Services.AddSingleton<IBiometricAuthService, BiometricAuthService>();
        builder.Services.AddSingleton<IPromoCodeRepository, PromoCodeRepository>();
        builder.Services.AddSingleton<IBackupService>(sp =>
        {
            var conn = sp.GetRequiredService<StorageConnection>();
            var modelPath = Path.Combine(FileSystem.AppDataDirectory, "adaptive-v1.bin");
            return new BackupService(conn, modelPath);
        });

        // Main Tab ViewModels & Pages (Singletons to eliminate tab switching lag and preserve state)
        builder.Services.AddSingleton<InboxViewModel>();
        builder.Services.AddSingleton<InboxPage>();
        builder.Services.AddSingleton<WalletViewModel>();
        builder.Services.AddSingleton<WalletPage>();
        builder.Services.AddSingleton<DigestViewModel>();
        builder.Services.AddSingleton<DigestPage>();
        builder.Services.AddSingleton<RulesViewModel>();
        builder.Services.AddSingleton<RulesPage>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<SettingsPage>();

        // Modal / Navigation ViewModels & Pages
        builder.Services.AddTransient<DetailViewModel>();
        builder.Services.AddTransient<DetailPage>();
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<StubDetailViewModel>();
        builder.Services.AddTransient<StubDetailPage>();
        builder.Services.AddTransient<SourcesViewModel>();
        builder.Services.AddTransient<SourcesPage>();
        builder.Services.AddTransient<ModelsViewModel>();
        builder.Services.AddTransient<ModelsPage>();

        // Security & Key Provider (SEC-002)
        builder.Services.AddSingleton<ISecureKeyProvider, SecureKeyProvider>();

        // Storage
        builder.Services.AddSingleton<StorageConnection>(sp =>
        {
            var keyProvider = sp.GetRequiredService<ISecureKeyProvider>();
            string password;
            try
            {
                password = keyProvider.GetDatabasePassword();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] Initial KeyStore retrieval failed: {ex.Message}. Retrying via re-initialization.");
                if (keyProvider.ReinitializeKey())
                {
                    password = keyProvider.GetDatabasePassword();
                }
                else
                {
                    throw;
                }
            }

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "alak.db");
            var mainConnStr = $"Data Source={dbPath};Mode=ReadWriteCreate;Password={password}";

            try
            {
                return new StorageConnection(mainConnStr);
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 26)
            {
                // Attempt legacy migration: if the DB was created with the fallback dev key "alak_secure_key_2024"
                const string fallbackPassword = "alak_secure_key_2024";
                if (password != fallbackPassword)
                {
                    try
                    {
                        var legacyConnStr = $"Data Source={dbPath};Mode=ReadWrite;Password={fallbackPassword}";
                        using (var legacyConn = new Microsoft.Data.Sqlite.SqliteConnection(legacyConnStr))
                        {
                            legacyConn.Open();
                            using var testCmd = legacyConn.CreateCommand();
                            testCmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                            testCmd.ExecuteScalar();

                            using var rekeyCmd = legacyConn.CreateCommand();
                            rekeyCmd.CommandText = $"PRAGMA rekey = '{password.Replace("'", "''")}';";
                            rekeyCmd.ExecuteNonQuery();
                        }
                        // Successfully rekeyed to the hardware-backed password
                        return new StorageConnection(mainConnStr);
                    }
                    catch
                    {
                        // Fall through to retry or error handling
                    }
                }

                // Never wipe or rename without first retrying KeyStore re-initialization
                System.Diagnostics.Debug.WriteLine("[MauiProgram] SQLite Error 26: Attempting KeyStore re-initialization before taking recovery action.");
                if (keyProvider.ReinitializeKey())
                {
                    try
                    {
                        var retriedPassword = keyProvider.GetDatabasePassword();
                        var retriedConnStr = $"Data Source={dbPath};Mode=ReadWriteCreate;Password={retriedPassword}";
                        return new StorageConnection(retriedConnStr);
                    }
                    catch (Exception retryEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MauiProgram] Re-initialized KeyStore connection failed: {retryEx.Message}");
                    }
                }

                // If DB truly cannot be decrypted by any means (hardware key lost/corrupted after reinstall),
                // safely preserve the corrupt DB as backup and create a fresh one to allow app recovery.
                try
                {
                    var backupPath = dbPath + $".corrupt.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                    if (File.Exists(dbPath)) File.Move(dbPath, backupPath);
                    if (File.Exists(dbPath + "-wal")) File.Move(dbPath + "-wal", backupPath + "-wal");
                    if (File.Exists(dbPath + "-shm")) File.Move(dbPath + "-shm", backupPath + "-shm");
                }
                catch { }

                return new StorageConnection(mainConnStr);
            }
        });

        var app = builder.Build();
        AppServices.Current = app.Services;

        // Tab Warming: warm primary tab singletons in background to eliminate ~1.5s first-tap lag
        Task.Run(() =>
        {
            try
            {
                _ = app.Services.GetService<InboxViewModel>();
                _ = app.Services.GetService<WalletViewModel>();
                _ = app.Services.GetService<DigestViewModel>();
                _ = app.Services.GetService<SettingsViewModel>();
            }
            catch { }
        });

        return app;
    }
}