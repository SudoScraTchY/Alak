using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Alak.App.Services;

public class LocalizationManager : INotifyPropertyChanged
{
    private static LocalizationManager? _instance;
    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _currentLanguage = "en";

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                ApplyCulture();
                OnPropertyChanged(string.Empty); // Notifies all properties/indexers changed
                OnPropertyChanged(nameof(IsRtl));
                OnPropertyChanged(nameof(CurrentFlowDirection));
            }
        }
    }

    public bool IsRtl => _currentLanguage == "fa";

    public FlowDirection CurrentFlowDirection => IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public string this[string key] => GetString(key);

    public LocalizationManager()
    {
        var savedLang = Preferences.Get("selected_language", "fa");
        _currentLanguage = savedLang;
        ApplyCulture();
    }

    public void SetLanguage(string lang)
    {
        if (lang == "system")
        {
            var systemLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            CurrentLanguage = systemLang == "fa" ? "fa" : "en";
        }
        else
        {
            CurrentLanguage = lang;
        }
        Preferences.Set("selected_language", _currentLanguage);
    }

    private void ApplyCulture()
    {
        try
        {
            var culture = new CultureInfo(_currentLanguage == "fa" ? "fa-IR" : "en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
        catch
        {
            // Fallback gracefully
        }
    }

    public string GetString(string key)
    {
        var isFa = _currentLanguage == "fa";
        if (isFa && StringsFa.TryGetValue(key, out var valFa)) return valFa;
        if (StringsEn.TryGetValue(key, out var valEn)) return valEn;
        return key;
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static readonly Dictionary<string, string> StringsEn = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation / Tabs
        { "Tab_Inbox", "Inbox" },
        { "Tab_Codes", "Codes" },
        { "Tab_Digest", "Digest" },
        { "Tab_Rules", "Rules" },
        { "Tab_Settings", "Settings" },

        // Common
        { "Common_Save", "Save" },
        { "Common_Cancel", "Cancel" },
        { "Common_Delete", "Delete" },
        { "Common_Done", "Done" },
        { "Common_Yes", "Yes" },
        { "Common_No", "No" },
        { "Common_OK", "OK" },
        { "Common_Enabled", "Enabled" },
        { "Common_Disabled", "Disabled" },

        // Inbox
        { "Inbox_Title", "Inbox" },
        { "Inbox_UnreadFormat", "· {0} unread" },
        { "Inbox_SearchPlaceholder", "Search notifications" },
        { "Inbox_NoNotifications", "No notifications" },
        { "Inbox_EmptyDesc", "Captured notifications will appear here" },
        { "Inbox_DailyDigest", "Daily Digest" },
        { "Inbox_Today", "Today" },
        { "Inbox_Yesterday", "Yesterday" },
        { "Inbox_ViaSms", "via SMS" },

        // Categories
        { "Cat_All", "All" },
        { "Cat_Promo", "Promotions" },
        { "Cat_Work", "Work" },
        { "Cat_People", "People" },
        { "Cat_Money", "Money" },
        { "Cat_Orders", "Orders" },
        { "Cat_Security", "Security" },
        { "Cat_Social", "Social" },
        { "Cat_Updates", "Updates" },
        { "Cat_Other", "Other" },

        // Detail Page & AI Explanations
        { "Detail_Back", "Back" },
        { "Detail_ViewInWallet", "View in Wallet" },
        { "Detail_ChangeCategory", "Change Category" },
        { "Detail_CategoryUpdated", "Category updated & learned for future notifications." },
        { "Detail_ExplainAi", "Explain (AI)" },
        { "Detail_AiSummaryTitle", "AI Summary & Insights" },
        { "Detail_GeneratingAiSummary", "Analyzing notification with local SLM..." },
        { "Detail_NoModelTitle", "No Local Model Installed" },
        { "Detail_NoModelDesc", "To generate local AI summaries, download SmolLM2 or import a GGUF model in Settings." },
        { "Detail_GoToSettings", "Go to Settings" },
        { "Detail_AiFailed", "Unable to generate summary. Please try again." },
        { "Detail_Importance", "Importance" },
        { "Detail_RecommendedAction", "Recommended Action" },
        { "Detail_Tags", "Tags" },
        { "Detail_Facts", "Facts" },
        { "Inbox_DigestTitle", "Today's Notification Digest" },
        { "Inbox_GeneratingDigest", "Generating daily summary..." },

        // Rules
        { "Rules_Title", "Rules" },
        { "Rules_AddNew", "Add New Rule" },
        { "Rules_AppPackage", "App / Package" },
        { "Rules_PackagePlaceholder", "App / Package (e.g. reddit, com.reddit.frontpage)" },
        { "Rules_PatternPlaceholder", "Text pattern (optional if app specified)" },
        { "Rules_Category", "Category" },
        { "Rules_Action", "Action" },
        { "Rules_Match", "Match Mode" },
        { "Rules_Priority", "Priority (0=first)" },
        { "Rules_CaseSensitive", "Case Sensitive" },
        { "Rules_AddButton", "Add Rule" },
        { "Rules_YourRules", "Your Rules" },
        { "Rules_NoRulesYet", "No rules yet" },
        { "Rules_NoRulesDesc", "Add a rule above to override how Alak sorts matching notifications." },

        // Settings
        { "Settings_Title", "Settings" },
        { "Settings_General", "General" },
        { "Settings_Notifications", "Notifications" },
        { "Settings_NotificationsDesc", "Receive notification alerts" },
        { "Settings_AdaptiveLearning", "Adaptive Learning" },
        { "Settings_AdaptiveLearningDesc", "Personalize classification from dismissals" },
        { "Settings_RtlLayout", "RTL Layout" },
        { "Settings_RtlLayoutDesc", "Right-to-left layout direction" },
        { "Settings_Global", "Global Settings" },
        { "Settings_Theme", "Theme" },
        { "Settings_Language", "UI Language" },
        { "Settings_Calendar", "Calendar" },
        { "Settings_Digits", "Digits" },
        { "Settings_PerformanceProfile", "SLM Performance Profile" },
        { "Settings_Tier2", "Tier 2 (SLM Engine)" },
        { "Settings_Tier2Desc", "Enable on-device SLM reranker / categorizer" },
        { "Settings_AppLock", "App Lock (Biometric/PIN)" },
        { "Settings_AppLockDesc", "Require biometric or PIN to unlock app" },
        { "Settings_DisplayPrivacy", "Display & Privacy" },
        { "Settings_ExcludeComms", "Exclude Communications" },
        { "Settings_ExcludeCommsDesc", "Do not capture SMS, email, and chats (Telegram, WhatsApp, etc.)" },
        { "Settings_HideAmounts", "Hide Amounts in Lists" },
        { "Settings_HideAmountsDesc", "Mask currency amounts in notification list" },
        { "Settings_NotificationReminders", "Notification Reminders" },
        { "Settings_NotificationRemindersDesc", "Show reminders for unread notifications" },
        { "Settings_GlobalSizeCap", "Global Size Cap (MB)" },
        { "Settings_GlobalSizeCapDesc", "0 = unlimited, max DB size" },
        { "Settings_ExportBackup", "Export Encrypted Backup (.alakbackup)" },
        { "Settings_RestoreBackup", "Restore Backup (.alakbackup)" },
        { "Settings_ImportModel", "Import Model (.gguf)" },
        { "Settings_ImportModelDesc", "Select a local .gguf model file from storage" },
        { "Backup_ExportTitle", "Export Encrypted Backup" },
        { "Backup_RestoreTitle", "Restore Encrypted Backup" },
        { "Backup_PassphrasePrompt", "Enter a passphrase to protect your backup. A lost passphrase means a lost backup." },
        { "Backup_RestorePrompt", "Enter the passphrase used when creating this backup:" },
        { "Backup_ExportSuccess", "Encrypted backup saved successfully." },
        { "Backup_RestoreSuccess", "Backup restored successfully." },
        { "Backup_MetadataFound", "Backup verified" },
        { "Backup_ModeMerge", "Merge (Preserve existing notifications)" },
        { "Backup_ModeReplace", "Replace All (Wipe and restore)" },
        { "Backup_WrongPassphraseOrCorrupted", "Incorrect passphrase or corrupted backup file." },
        { "Backup_SelectFile", "Select .alakbackup file" },
        { "Model_ImportSuccess", "Model imported successfully and ready for inference." },
        { "Model_InvalidHeader", "Selected file is not a valid GGUF model." },
        { "Settings_DataManagement", "Data Management" },
        { "Settings_StoredNotifications", "Stored Notifications" },
        { "Settings_ManageSources", "Manage Sources" },
        { "Settings_ExportData", "Export Data (JSON)" },
        { "Settings_ClearAllData", "Clear All Data" },
        { "Settings_Onboarding", "Onboarding" },
        { "Settings_RerunOnboarding", "Re-run Onboarding" },
        { "Settings_About", "About" },
        { "Settings_Version", "Version" },
        { "Settings_Framework", "Framework" },
        { "Settings_AppDesc", "Alak — Smart Persian Notification Classifier" },
        { "Settings_BuiltWith", "Built with .NET MAUI, SQLite, and local llama.cpp SLM" },
        { "Settings_SlmModel", "On-Device SLM Model" },
        { "Settings_DownloadModel", "Download Model (SmolLM2-135M)" },
        { "Settings_TestInference", "Test SLM Inference" },
        { "Settings_ModelReady", "Model is ready for inference" },
        { "Settings_ModelReadyArmv8", "ARMv8.0 CPU lacks LSE atomics. Rule engine active." },
        { "Settings_NoModel", "No SLM model downloaded" },
        { "Settings_AppOpenPromoReminders", "App Open Promo Reminders" },
        { "Settings_AppOpenPromoRemindersDesc", "Notify with active coupons when opening apps like Digikala, Snapp, etc." },
        { "Settings_UsageAccessPermission", "Usage Access Permission" },
        { "Settings_GrantUsageAccess", "Grant Access" },
        { "Settings_UsageAccessGranted", "Access Granted" },
        { "Settings_UsageAccessNeeded", "Permission Required to detect opened apps" },

        // Wallet Details
        { "Wallet_OriginalPromoTitle", "Original Notification" },
        { "Wallet_Code", "Code" },
        { "Wallet_App", "Source App" },
        { "Wallet_OriginalNotFound", "The original notification has been cleared from history, but this promo code remains saved in your wallet." },

        // Digest Screen (§13.5)
        { "Digest_Title", "Daily Digest" },
        { "Digest_ModeRules", "Rules (Instant)" },
        { "Digest_ModeAi", "AI Digest (SLM)" },
        { "Digest_PeriodToday", "Today" },
        { "Digest_PeriodYesterday", "Yesterday" },
        { "Digest_Period7Days", "Past 7 Days" },
        { "Digest_Generate", "Generate Digest" },
        { "Digest_Regenerate", "Regenerate" },
        { "Digest_Cancel", "Cancel Generation" },
        { "Digest_Generating", "Analyzing notifications with local SLM..." },
        { "Digest_Streaming", "Generating summary..." },
        { "Digest_NoNotifications", "No notifications found for this period." },
        { "Digest_NoModel", "No SLM model installed. AI digest requires a local GGUF model." },
        { "Digest_ManageModels", "Manage Models" },
        { "Digest_CopySection", "Copy" },
        { "Digest_Copied", "Copied to clipboard" },
        { "Digest_CopyAll", "Copy Full Digest" },
        { "Digest_EmptyState", "Select a period and generate a digest of your notifications." },
        { "Digest_ActionItems", "Action Items & Recommendations" },
        { "Digest_QuickInsights", "Key Highlights" },
        { "Digest_FinancialActivity", "Financial Activity" },
        { "Digest_OrdersDeliveries", "Orders & Shipments" },
        { "Digest_PromoDeals", "Offers & Promos" },
        { "Digest_SecurityAlerts", "Security & Alerts" },

        // Models Screen (§13.8)
        { "Models_Title", "SLM Models" },
        { "Models_Installed", "Installed Models (.gguf)" },
        { "Models_NoModels", "No GGUF models installed" },
        { "Models_Active", "Active" },
        { "Models_SetActive", "Set Active" },
        { "Models_Delete", "Delete" },
        { "Models_ImportLocal", "Import Local Model (.gguf)" },
        { "Models_CuratedCatalog", "Curated External Catalog" },
        { "Models_DownloadExternal", "Open in Browser" },
        { "Models_OfflineNote", "100% offline app privacy: Models are never downloaded inside the app. Zero in-app network calls. Tapping download opens Hugging Face in your external browser. Once downloaded, use 'Import Local Model' to import it into Alak." },
        { "Models_Smol135Title", "SmolLM2-135M-Instruct" },
        { "Models_Smol135Desc", "Q4_K_M (~98 MB) · Ultra-fast, ideal for low RAM and battery saving" },
        { "Models_Smol360Title", "SmolLM2-360M-Instruct" },
        { "Models_Smol360Desc", "Q4_K_M (~230 MB) · Balanced speed and comprehension" },
        { "Models_Qwen05Title", "Qwen2.5-0.5B-Instruct" },
        { "Models_Qwen05Desc", "Q4_K_M (~390 MB) · High quality multilingual & strong Persian support" },
        { "Settings_ManageModels", "SLM Models" },
        { "Settings_ManageModelsDesc", "View installed models, import .gguf, or explore catalog" },
        { "Settings_Rules", "Rules Engine" },
        { "Settings_RulesDesc", "Custom rules and package routing" }
    };

    private static readonly Dictionary<string, string> StringsFa = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation / Tabs
        { "Tab_Inbox", "صندوق ورودی" },
        { "Tab_Codes", "کدها" },
        { "Tab_Digest", "خلاصه" },
        { "Tab_Rules", "قوانین" },
        { "Tab_Settings", "تنظیمات" },

        // Common
        { "Common_Save", "ذخیره" },
        { "Common_Cancel", "انصراف" },
        { "Common_Delete", "حذف" },
        { "Common_Done", "انجام شد" },
        { "Common_Yes", "بله" },
        { "Common_No", "خیر" },
        { "Common_OK", "تأیید" },
        { "Common_Enabled", "فعال" },
        { "Common_Disabled", "غیرفعال" },

        // Inbox
        { "Inbox_Title", "صندوق ورودی" },
        { "Inbox_UnreadFormat", "· {0} خوانده‌نشده" },
        { "Inbox_SearchPlaceholder", "جستجوی اعلان‌ها..." },
        { "Inbox_NoNotifications", "اعلا‌نی وجود ندارد" },
        { "Inbox_EmptyDesc", "اعلان‌های دریافت شده در اینجا نمایش داده می‌شوند" },
        { "Inbox_DailyDigest", "خلاصه اعلان‌ها" },
        { "Inbox_Today", "امروز" },
        { "Inbox_Yesterday", "دیروز" },
        { "Inbox_ViaSms", "از طریق پیامک" },

        // Categories
        { "Cat_All", "همه" },
        { "Cat_Promo", "تخفیف‌ها" },
        { "Cat_Work", "کاری" },
        { "Cat_People", "پیام‌ها" },
        { "Cat_Money", "مالی" },
        { "Cat_Orders", "سفارش‌ها" },
        { "Cat_Security", "امنیتی" },
        { "Cat_Social", "شبکه‌های اجتماعی" },
        { "Cat_Updates", "بروزرسانی‌ها" },
        { "Cat_Other", "سایر" },

        // Detail Page & AI Explanations
        { "Detail_Back", "بازگشت" },
        { "Detail_ViewInWallet", "مشاهده در کیف پول" },
        { "Detail_ChangeCategory", "تغییر دسته‌بندی" },
        { "Detail_CategoryUpdated", "دسته‌بندی به‌روز شد و برای اعلان‌های بعدی ذخیره گردید." },
        { "Detail_ExplainAi", "تحلیل هوشمند" },
        { "Detail_AiSummaryTitle", "خلاصه و تحلیل هوشمند محلی" },
        { "Detail_GeneratingAiSummary", "در حال تحلیل اعلان با مدل محلی..." },
        { "Detail_NoModelTitle", "مدل زبانی نصب نشده است" },
        { "Detail_NoModelDesc", "جهت ایجاد خلاصه هوشمند، لطفاً مدل زبانی SmolLM2 را از بخش تنظیمات دریافت نمایید." },
        { "Detail_GoToSettings", "رفتن به تنظیمات" },
        { "Detail_AiFailed", "خطا در تولید خلاصه هوشمند. لطفاً مجدداً تلاش کنید." },
        { "Detail_Importance", "سطح اهمیت" },
        { "Detail_RecommendedAction", "اقدام پیشنهادی" },
        { "Detail_Tags", "برچسب‌ها" },
        { "Detail_Facts", "اطلاعات استخراج شده" },
        { "Inbox_DigestTitle", "خلاصه هوشمند اعلان‌های امروز" },
        { "Inbox_GeneratingDigest", "در حال تولید خلاصه روز..." },

        // Rules
        { "Rules_Title", "قوانین" },
        { "Rules_AddNew", "افزودن قانون جدید" },
        { "Rules_AppPackage", "برنامه / بسته" },
        { "Rules_PackagePlaceholder", "برنامه یا نام بسته (مانند: reddit، com.reddit.frontpage)" },
        { "Rules_PatternPlaceholder", "الگوی متن (در صورت تعیین برنامه اختیاری است)" },
        { "Rules_Category", "دسته‌بندی" },
        { "Rules_Action", "عملیات" },
        { "Rules_Match", "نوع تطابق" },
        { "Rules_Priority", "اولویت (۰=بالاترین)" },
        { "Rules_CaseSensitive", "حساس به حروف بزرگ و کوچک" },
        { "Rules_AddButton", "افزودن قانون" },
        { "Rules_YourRules", "قوانین شما" },
        { "Rules_NoRulesYet", "هنوز قانونی ثبت نشده است" },
        { "Rules_NoRulesDesc", "برای اولویت‌دهی و شخصی‌سازی اعلان‌ها، قانونی در بالا اضافه کنید." },

        // Settings
        { "Settings_Title", "تنظیمات" },
        { "Settings_General", "عمومی" },
        { "Settings_Notifications", "اعلان‌ها" },
        { "Settings_NotificationsDesc", "دریافت هشدارهای اعلان" },
        { "Settings_AdaptiveLearning", "یادگیری تطبیقی" },
        { "Settings_AdaptiveLearningDesc", "شخصی‌سازی دسته‌بندی بر اساس بازخورد کاربر" },
        { "Settings_RtlLayout", "چیدمان راست‌به‌چپ (RTL)" },
        { "Settings_RtlLayoutDesc", "چیدمان راست‌به‌چپ برای زبان فارسی" },
        { "Settings_Global", "تنظیمات کلی" },
        { "Settings_Theme", "پوسته" },
        { "Settings_Language", "زبان برنامه" },
        { "Settings_Calendar", "تقویم" },
        { "Settings_Digits", "ارقام" },
        { "Settings_PerformanceProfile", "پروفایل عملکرد SLM" },
        { "Settings_Tier2", "لایه ۲ (مدل زبانی محلی SLM)" },
        { "Settings_Tier2Desc", "فعال‌سازی بازرتبه‌بندی محلی با هوش مصنوعی" },
        { "Settings_AppLock", "قفل برنامه (اثر انگشت / پین)" },
        { "Settings_AppLockDesc", "نیاز به احراز هویت بیومتریک برای بازگشایی برنامه" },
        { "Settings_DisplayPrivacy", "نمایش و حریم خصوصی" },
        { "Settings_ExcludeComms", "عدم ثبت پیام‌های شخصی" },
        { "Settings_ExcludeCommsDesc", "عدم ذخیره پیامک‌ها، ایمیل‌ها و چت‌های تلگرام و واتساپ" },
        { "Settings_HideAmounts", "مخفی‌سازی مبالغ در لیست" },
        { "Settings_HideAmountsDesc", "پنهان کردن مبالغ مالی در فهرست اعلان‌ها" },
        { "Settings_NotificationReminders", "یادآور اعلان‌ها" },
        { "Settings_NotificationRemindersDesc", "نمایش یادآور برای اعلان‌های خوانده نشده" },
        { "Settings_GlobalSizeCap", "حداکثر حجم داده (مگابایت)" },
        { "Settings_GlobalSizeCapDesc", "۰ = نامحدود، حداکثر حجم دیتابیس" },
        { "Settings_ExportBackup", "پشتیبان‌گیری رمزنگاری‌شده (.alakbackup)" },
        { "Settings_RestoreBackup", "بازیابی پشتیبان (.alakbackup)" },
        { "Settings_ImportModel", "وارد کردن مدل محلی (.gguf)" },
        { "Settings_ImportModelDesc", "انتخاب فایل مدل .gguf از حافظه دستگاه" },
        { "Backup_ExportTitle", "پشتیبان‌گیری رمزنگاری‌شده" },
        { "Backup_RestoreTitle", "بازیابی نسخه پشتیبان" },
        { "Backup_PassphrasePrompt", "یک گذرواژه برای رمزنگاری وارد کنید. در صورت فراموشی، اطلاعات قابل بازیابی نیست." },
        { "Backup_RestorePrompt", "گذرواژه تعیین‌شده هنگام پشتیبان‌گیری را وارد کنید:" },
        { "Backup_ExportSuccess", "نسخه پشتیبان با موفقیت ذخیره شد." },
        { "Backup_RestoreSuccess", "بازیابی اطلاعات با موفقیت انجام شد." },
        { "Backup_MetadataFound", "فایل پشتیبان تأیید شد" },
        { "Backup_ModeMerge", "ترکیب (حفظ اعلان‌های موجود)" },
        { "Backup_ModeReplace", "جایگزینی کامل (پاکسازی و بازنویسی)" },
        { "Backup_WrongPassphraseOrCorrupted", "گذرواژه نادرست است یا فایل پشتیبان آسیب دیده است." },
        { "Backup_SelectFile", "انتخاب فایل .alakbackup" },
        { "Model_ImportSuccess", "مدل با موفقیت وارد شد و آماده پردازش است." },
        { "Model_InvalidHeader", "فایل انتخاب‌شده یک مدل معتبر GGUF نیست." },
        { "Settings_DataManagement", "مدیریت داده‌ها" },
        { "Settings_StoredNotifications", "اعلان‌های ذخیره‌شده" },
        { "Settings_ManageSources", "مدیریت منابع" },
        { "Settings_ExportData", "خروجی داده‌ها (JSON)" },
        { "Settings_ClearAllData", "پاکسازی تمام داده‌ها" },
        { "Settings_Onboarding", "راهنما" },
        { "Settings_RerunOnboarding", "اجرای مجدد معرفی اولیه" },
        { "Settings_About", "درباره برنامه" },
        { "Settings_Version", "نسخه" },
        { "Settings_Framework", "فریم‌ورک" },
        { "Settings_AppDesc", "الک — دسته‌بندی هوشمند اعلان‌های فارسی" },
        { "Settings_BuiltWith", "توسعه یافته با .NET MAUI، SQLite و llama.cpp محلی" },
        { "Settings_SlmModel", "مدل هوش مصنوعی محلی (SLM)" },
        { "Settings_DownloadModel", "دانلود مدل (SmolLM2-135M)" },
        { "Settings_TestInference", "تست استنتاج مدل زبانی" },
        { "Settings_ModelReady", "مدل هوش مصنوعی آماده بهره‌برداری است" },
        { "Settings_ModelReadyArmv8", "پردازنده فاقد دستورات LSE اتمیک است. موتور قوانین فعال است." },
        { "Settings_NoModel", "هنوز مدلی دانلود نشده است" },
        { "Settings_AppOpenPromoReminders", "یادآور تخفیف هنگام باز کردن برنامه" },
        { "Settings_AppOpenPromoRemindersDesc", "نمایش کد تخفیف فعال هنگام ورود به برنامه‌هایی مانند دیجی‌کالا، اسنپ و..." },
        { "Settings_UsageAccessPermission", "دسترسی به وضعیت استفاده برنامه‌ها" },
        { "Settings_GrantUsageAccess", "اعطای دسترسی" },
        { "Settings_UsageAccessGranted", "دسترسی فعال و مجاز است" },
        { "Settings_UsageAccessNeeded", "جهت تشخیص برنامه‌های باز شده نیاز به فعال‌سازی دارد" },

        // Wallet Details
        { "Wallet_OriginalPromoTitle", "پیام اصلی تخفیف" },
        { "Wallet_Code", "کد تخفیف" },
        { "Wallet_App", "برنامه" },
        { "Wallet_OriginalNotFound", "پیام اصلی این اعلان از تاریخچه پاک شده است، اما این کد در کیف پول شما محفوظ است." },

        // Digest Screen (§13.5)
        { "Digest_Title", "خلاصه اعلان‌ها" },
        { "Digest_ModeRules", "قوانین (آنی)" },
        { "Digest_ModeAi", "هوش مصنوعی (SLM)" },
        { "Digest_PeriodToday", "امروز" },
        { "Digest_PeriodYesterday", "دیروز" },
        { "Digest_Period7Days", "۷ روز گذشته" },
        { "Digest_Generate", "تولید خلاصه" },
        { "Digest_Regenerate", "تولید مجدد" },
        { "Digest_Cancel", "توقف تولید" },
        { "Digest_Generating", "در حال تحلیل اعلان‌ها با مدل محلی..." },
        { "Digest_Streaming", "در حال تولید متن خلاصه..." },
        { "Digest_NoNotifications", "هیچ اعلانی در این بازه زمانی یافت نشد." },
        { "Digest_NoModel", "مدل محلی نصب نشده است. خلاصه هوش مصنوعی نیاز به فایل GGUF دارد." },
        { "Digest_ManageModels", "مدیریت مدل‌ها" },
        { "Digest_CopySection", "کپی" },
        { "Digest_Copied", "در حافظه کپی شد" },
        { "Digest_CopyAll", "کپی کل خلاصه" },
        { "Digest_EmptyState", "یک بازه زمانی انتخاب کنید تا چکیده اعلان‌های شما نمایش داده شود." },
        { "Digest_ActionItems", "اقدامات پیشنهادی و پیگیری‌ها" },
        { "Digest_QuickInsights", "چکیده و نکات کلیدی" },
        { "Digest_FinancialActivity", "تراکنش‌های مالی" },
        { "Digest_OrdersDeliveries", "سفارش‌ها و بسته‌ها" },
        { "Digest_PromoDeals", "تخفیف‌ها و پیشنهادات" },
        { "Digest_SecurityAlerts", "امنیت و هشدارها" },

        // Models Screen (§13.8)
        { "Models_Title", "مدل‌های محلی هوش مصنوعی" },
        { "Models_Installed", "مدل‌های نصب‌شده (.gguf)" },
        { "Models_NoModels", "مدل GGUF نصب نشده است" },
        { "Models_Active", "فعال" },
        { "Models_SetActive", "انتخاب به عنوان مدل فعال" },
        { "Models_Delete", "حذف" },
        { "Models_ImportLocal", "وارد کردن مدل محلی (.gguf)" },
        { "Models_CuratedCatalog", "مدل‌های پیشنهادی خارجی" },
        { "Models_DownloadExternal", "دانلود از مرورگر" },
        { "Models_OfflineNote", "حفظ کامل حریم خصوصی: مدل‌ها هرگز داخل برنامه دانلود نمی‌شوند و هیچ ارتباط اینترنتی برقرار نمی‌گردد. لینک‌ها صفحه هاگینگ‌فیس را در مرورگر خارجی باز می‌کنند. پس از دانلود، فایل را با دکمه 'وارد کردن مدل محلی' انتخاب کنید." },
        { "Models_Smol135Title", "SmolLM2-135M-Instruct" },
        { "Models_Smol135Desc", "نسخه Q4_K_M (حدود ۹۸ مگابایت) · بسیار سریع، مناسب رم پایین و مصرف بهینه باتری" },
        { "Models_Smol360Title", "SmolLM2-360M-Instruct" },
        { "Models_Smol360Desc", "نسخه Q4_K_M (حدود ۲۳۰ مگابایت) · تعادل عالی بین سرعت و درک متن" },
        { "Models_Qwen05Title", "Qwen2.5-0.5B-Instruct" },
        { "Models_Qwen05Desc", "نسخه Q4_K_M (حدود ۳۹۰ مگابایت) · کیفیت بالا در چندزبانه و پشتیبانی بسیار قوی از فارسی" },
        { "Settings_ManageModels", "مدل‌های هوش مصنوعی (SLM)" },
        { "Settings_ManageModelsDesc", "مشاهده مدل‌های نصب شده، وارد کردن .gguf و راهنمای دریافت" },
        { "Settings_Rules", "موتور قوانین" },
        { "Settings_RulesDesc", "مدیریت قوانین سفارشی و اولویت‌بندی اعلان‌ها" }
    };
}
