/* ==========================================================================
   Alak (الک) — Top-Tier Interactive Web Engine
   Full Bilingual English & Persian Engine + Client-Side Sieve Classifier
   ========================================================================== */

const i18nData = {
  en: {
    /* Brand & Nav */
    "nav.features": "Features",
    "nav.simulator": "Live Simulator",
    "nav.wallet": "Ticket Stub",
    "nav.architecture": "3-Tier AI",
    "nav.privacy": "Privacy Audit",
    "nav.gallery": "Screenshots",
    "nav.download": "Download APK",
    "brand.tagline": "Sift the noise · On-device notification sieve",

    /* Hero */
    "hero.pill": "100% Offline · Zero Internet Permission · .NET 10 MAUI",
    "hero.title_pre": "Sift the noise.",
    "hero.title_post": "Keep what matters.",
    "hero.desc": "Your notification shade is volatile chaos. Alak is a private, on-device clerk that captures notifications, extracts promo codes & discounts into a physical Ticket Stub, safely redacts OTPs, and never sends a single byte to any cloud.",
    "hero.cta_download": "Download APK (v0.8 Alpha)",
    "hero.cta_github": "View on GitHub",
    "hero.cta_simulator": "Try Live Simulator ↓",
    "hero.meta_offline": "Zero Network Permission",
    "hero.meta_crypto": "SQLCipher 256-bit AES",
    "hero.meta_speed": "< 1.5ms Rule Match",

    /* Sieve Stage */
    "stage.caption": "Real-time on-device classification pipeline: noise falls through, valuable stubs settle.",

    /* Simulator */
    "sim.pill": "Interactive Sandbox",
    "sim.title": "Experience the Sieve Live",
    "sim.subtitle": "Select a sample notification below or type your own to test Alak's Tier-1 rule matching, entity extraction, and zero-storage privacy guarantees.",
    "sim.preset_label": "Try presets:",
    "sim.preset_snap": "🍕 SnapFood 30% Promo",
    "sim.preset_bank": "💳 Bank Mellat Debit",
    "sim.preset_otp": "🔐 Bank OTP (Zero-Stored)",
    "sim.preset_ride": "🚗 Snapp Driver PIN",
    "sim.preset_dominos": "🏷️ Domino's 25% Deal",
    "sim.preset_slack": "💼 Slack Team Mention",
    "sim.field_title": "Notification Title / Sender",
    "sim.field_body": "Notification Body",
    "sim.field_title_ph": "e.g. SnapFood, Bank, Slack...",
    "sim.field_body_ph": "Paste or type notification content here...",
    "sim.btn_sift": "Sift Notification ⚡",
    "sim.latency_label": "Classification Latency:",
    "sim.facts_title": "Extracted Facts & Entities",
    "sim.evidence_title": "Tier-1 Evidence Trace (Explainability)",
    "sim.otp_warning_title": "ZERO-TOLERANCE PRIVACY GUARD ACTIVATED",
    "sim.otp_warning_desc": "OTP / 2FA secret detected. Strictly transient: notification body is immediately purged from memory and NEVER stored to the encrypted database.",

    /* Stub Section */
    "stub.pill": "Signature Component",
    "stub.title": "The Ticket Stub",
    "stub.subtitle": "Why settle for generic rounded cards? Alak treats promo codes like physical coupon stubs: torn edges, monospaced clarity, and visual expiry tracking.",
    "stub.copy": "Copy Code",
    "stub.copied": "Copied!",
    "stub.active": "Active",
    "stub.expiring": "Expiring Tonight",
    "stub.used": "Used",

    /* 3-Tier Architecture */
    "arch.pill": "Technical Architecture",
    "arch.title": "The 3-Tier Classification Pipeline",
    "arch.subtitle": "Engineered for instant speed and zero cloud dependency. 99% of work finishes in under a millisecond with 0 battery waste.",
    "arch.t1_num": "Tier 1 · Instant Rule Engine",
    "arch.t1_title": "Deterministic Rules",
    "arch.t1_tag1": "Sub-millisecond",
    "arch.t1_tag2": "Aho-Corasick",
    "arch.t1_desc": "Evaluates incoming notifications with log-odds concept scoring and multilingual extractors. Resolves categories safety-first (Security > Money > Work > Orders > Offers).",
    "arch.t1_stat1_k": "Average Latency",
    "arch.t1_stat1_v": "< 0.5 ms",
    "arch.t1_stat2_k": "RAM Footprint",
    "arch.t1_stat2_v": "Negligible",
    "arch.t1_stat3_k": "Network Calls",
    "arch.t1_stat3_v": "0 (Always)",

    "arch.t2_num": "Tier 2 · Adaptive Classifier",
    "arch.t2_title": "On-Device Learning",
    "arch.t2_tag1": "Online AdaGrad",
    "arch.t2_tag2": "Hashed Features",
    "arch.t2_desc": "Learns from your corrections on-device. Feature hashing guarantees that no raw personal text is ever stored for training weights.",
    "arch.t2_stat1_k": "Weight File",
    "arch.t2_stat1_v": "2.4 MB (Private)",
    "arch.t2_stat2_k": "Update Speed",
    "arch.t2_stat2_v": "< 3 ms",
    "arch.t2_stat3_k": "Adaptation",
    "arch.t2_stat3_v": "Instant",

    "arch.t3_num": "Tier 3 · Small Language Model",
    "arch.t3_title": "User-Triggered SLM",
    "arch.t3_tag1": "Tap-Only",
    "arch.t3_tag2": "BYO Model",
    "arch.t3_desc": "Optional local AI: runs strictly when you tap 'Explain' or 'Summarize'. Uses GGUF / LiteRT models imported by you. Never runs in background.",
    "arch.t3_stat1_k": "Execution Mode",
    "arch.t3_stat1_v": "Manual Tap Only",
    "arch.t3_stat2_k": "Constraint",
    "arch.t3_stat2_v": "GBNF Grammars",
    "arch.t3_stat3_k": "Cloud Leak",
    "arch.t3_stat3_v": "Impossible",

    /* Privacy */
    "priv.pill": "Privacy by Construction",
    "priv.title": "Verifiable, Not Promised",
    "priv.subtitle": "We don't ask you to trust a privacy policy. Privacy is hardcoded into the architecture and verified automatically in CI.",
    "priv.r1_title": "Zero Network Capability",
    "priv.r1_desc": "Neither android.permission.INTERNET nor ACCESS_NETWORK_STATE is declared. CI privacy-guard halts builds if any network API is detected.",
    "priv.r2_title": "No Analytics, Ads, or Crash SDKs",
    "priv.r2_desc": "No Firebase, Facebook, Sentry, AppCenter, or telemetry SDKs exist anywhere in the dependency graph.",
    "priv.r3_title": "SQLCipher 256-bit AES Encryption",
    "priv.r3_desc": "All stored notifications and extracted entities reside in an encrypted database with full-text search.",
    "priv.r4_title": "Zero OTP / 2FA Persistence",
    "priv.r4_desc": "Verification codes, banking passwords, and 2FA tokens are classified as security and immediately discarded from persistent storage.",

    /* Bilingual */
    "bidi.pill": "Two Scripts, One Product",
    "bidi.title": "Persian & English as Equals",
    "bidi.subtitle": "Persian is not an afterthought translated from English. Both scripts receive native typographic treatment and dedicated language features.",
    "bidi.item1": "Native Jalali (شمسی) calendar engine with golden-table tests (1400–1420).",
    "bidi.item2": "Full Arabic/Persian character normalization (ی/ي, ک/ك, and ZWNJ نیم‌فاصله).",
    "bidi.item3": "Persian scale words (هزار, میلیون) and currency conversions (ریال / تومان).",
    "bidi.item4": "Experimental Finglish (fa-latn) extension pack with precision guard rails.",
    "bidi.cal_title": "Jalali Solar Calendar",
    "bidi.cal_date": "۳ آبان ۱۴۰۵",
    "bidi.cal_desc": "Automatic extraction of Persian expiry dates ('تا پایان امشب', 'جمعه آینده').",

    /* Gallery */
    "gal.pill": "App Showcase",
    "gal.title": "Crafted for Daily Calm",
    "gal.subtitle": "Explore the official Android UI. Paper textures, clear colored spines, and zero clutter.",
    "gal.tab_all": "All Screens",
    "gal.tab_inbox": "Inbox & Triage",
    "gal.tab_wallet": "Codes Wallet",
    "gal.tab_detail": "Detail & Evidence",
    "gal.tab_dark": "Dark Mode",
    "gal.card1_title": "Triage Inbox",
    "gal.card1_sub": "Categorized notification stack with colored spines.",
    "gal.card2_title": "Codes Wallet",
    "gal.card2_sub": "Saved promo codes with one-tap copy and expiry tracker.",
    "gal.card3_title": "Notification Detail",
    "gal.card3_sub": "Full Ticket Stub with transparent evidence trace.",
    "gal.card4_title": "Dark Mode Detail",
    "gal.card4_sub": "Calm, low-contrast night palette.",
    "gal.card5_title": "Settings & Controls",
    "gal.card5_sub": "Granular capture settings per application.",
    "gal.card6_title": "Sources Configuration",
    "gal.card6_sub": "Full control over which apps are captured.",

    /* Specs & Download */
    "specs.pill": "Specifications",
    "specs.title": "Built with Modern Open Source",
    "specs.platform": "Target Platform",
    "specs.platform_v": "Android 7.0+ (API 24 to API 36)",
    "specs.framework": "Framework",
    "specs.framework_v": ".NET 10 MAUI (net10.0-android36.0)",
    "specs.storage": "Database Storage",
    "specs.storage_v": "SQLCipher SQLite + FTS5 (256-bit AES)",
    "specs.perf": "Classification Latency",
    "specs.perf_v": "< 1.8 ms on low-end ARM64",
    "specs.license": "License",
    "specs.license_v": "GPL-3.0 (Code) / CC0-1.0 (Data & Packs)",

    "down.title": "Get Alak for Android",
    "down.desc": "Download the official signed release APK directly from GitHub or build from source using the .NET 10 SDK.",
    "down.btn_apk": "Download Alak APK (arm64)",
    "down.btn_source": "Source Code on GitHub",
    "down.sha_label": "SHA-256 Checksum:",

    /* Footer */
    "footer.desc": "Alak (الک) is Persian for sieve — the fine mesh that keeps what matters and lets the rest fall through. Free, open-source, and private forever.",
    "footer.links_title": "Repository",
    "footer.docs_title": "Documentation",
    "footer.copy": "© 2026 Alak Project · Licensed under GPL-3.0"
  },

  fa: {
    /* Brand & Nav */
    "nav.features": "ویژگی‌ها",
    "nav.simulator": "شبیه‌ساز زنده",
    "nav.wallet": "بلیط تخفیف",
    "nav.architecture": "معماری سه‌لایه",
    "nav.privacy": "امنیت و حریم خصوصی",
    "nav.gallery": "تصاویر برنامه",
    "nav.download": "دانلود برنامه",
    "brand.tagline": "غربال نویزها · دستیار اعلان‌های محرمانه بر روی دستگاه",

    /* Hero */
    "hero.pill": "کاملاً آفلاین · بدون دسترسی اینترنت · دات‌نت ۱۰ و MAUI",
    "hero.title_pre": "غربالِ نویزهای بی‌پایان.",
    "hero.title_post": "آنچه مهم است می‌ماند.",
    "hero.desc": "نوار اعلان‌های گوشی پر از هیاهوی فرّار و شلوغ است. الک دستیار محرمانه شما روی دستگاه است که اعلان‌ها را دسته‌بندی می‌کند، کدهای تخفیف را در قالب «بلیط تخفیف» نجات می‌دهد، رمزهای یکبارمصرف را بی‌درنگ پاکسازی می‌کند و هرگز حتی یک بایت داده به اینترنت نمی‌فرستد.",
    "hero.cta_download": "دانلود فایل نصبی APK (نسخه ۰.۸ آلفا)",
    "hero.cta_github": "مشاهده در گیت‌هاب",
    "hero.cta_simulator": "امتحان آنلاین شبیه‌ساز ↓",
    "hero.meta_offline": "بدون مجوز اینترنت (Offline)",
    "hero.meta_crypto": "رمزنگاری SQLCipher ۲۵۶ بیتی",
    "hero.meta_speed": "شناسایی در کمتر از ۱.۵ میلی‌ثانیه",

    /* Sieve Stage */
    "stage.caption": "خط پردازش هوشمند روی دستگاه: نویزها غربال شده و بلیط‌های ارزشمند ته‌نشین می‌شوند.",

    /* Simulator */
    "sim.pill": "شبیه‌ساز تعاملی",
    "sim.title": "عملکرد الک را زنده امتحان کنید",
    "sim.subtitle": "یکی از اعلان‌های نمونه زیر را انتخاب کنید یا متن دلخواه خود را بنویسید تا دسته‌بندی، استخراج موجودیت‌ها و گارانتی پاکسازی رمزهای یکبارمصرف را ببینید.",
    "sim.preset_label": "اعلان‌های نمونه:",
    "sim.preset_snap": "🍕 تخفیف ۳۰٪ اسنپ‌فود",
    "sim.preset_bank": "💳 تراکنش بانک ملت",
    "sim.preset_otp": "🔐 پیامک رمز یکبارمصرف (عدم ذخیره‌سازی)",
    "sim.preset_ride": "🚗 پین کد سفر اسنپ",
    "sim.preset_dominos": "🏷️ تخفیف انگلیسی Domino's",
    "sim.preset_slack": "💼 پیام کاری اسلک",
    "sim.field_title": "عنوان یا نام فرستنده اعلان",
    "sim.field_body": "متن اعلان",
    "sim.field_title_ph": "مثلاً اسنپ‌فود، بانک ملت، تلگرام...",
    "sim.field_body_ph": "متن پیام یا اعلان را اینجا بنویسید...",
    "sim.btn_sift": "غربال و پالایش اعلان ⚡",
    "sim.latency_label": "زمان پردازش روی دستگاه:",
    "sim.facts_title": "داده‌های استخراج‌شده (Entities)",
    "sim.evidence_title": "رهگیری استدلال (Explainability)",
    "sim.otp_warning_title": "سپر امنیتی پاکسازی بی‌درنگ فعال شد",
    "sim.otp_warning_desc": "رمز یکبار مصرف یا کد ورود شناسایی شد. مطابق اصل امنیت مطلق: متن اعلان بلافاصله از حافظه موقت محو شده و هرگز در پایگاه داده ذخیره نمی‌شود.",

    /* Stub Section */
    "stub.pill": "طراحی اختصاصی",
    "stub.title": "بلیط تخفیف (The Ticket Stub)",
    "stub.subtitle": "چرا کارت‌های تکراری و مدرنِ یکنواخت؟ الک با کدهای تخفیف مانند بلیط‌های کاغذی فیزیکی رفتار می‌کند: گوشه‌های برش‌خورده، قلم مونو برای دقت در کپی، و نوار زمانی اتمام مهلت.",
    "stub.copy": "کپی کد",
    "stub.copied": "کپی شد!",
    "stub.active": "معتبر",
    "stub.expiring": "انقضا تا پایان امشب",
    "stub.used": "استفاده شده",

    /* 3-Tier Architecture */
    "arch.pill": "معماری فنی",
    "arch.title": "خط پردازش سه‌لایه (3-Tier Pipeline)",
    "arch.subtitle": "طراحی‌شده برای حداکثر سرعت بدون وابستگی به ابر. ۹۹٪ اعلان‌ها در کمتر از یک میلی‌ثانیه و بدون مصرف باتری مرتب می‌شوند.",
    "arch.t1_num": "لایه ۱ · موتور قوانین آنی",
    "arch.t1_title": "قوانین قطعی (Deterministic Rules)",
    "arch.t1_tag1": "زیر ۱ میلی‌ثانیه",
    "arch.t1_tag2": "الگوریتم آهو-کوراسیک",
    "arch.t1_desc": "پایش فوری اعلان‌ها با تطابق مفاهیم و امتیازدهی لگاریتمی. حل تعارض‌ها با اولویت ایمنی (امنیت > امور مالی > کار > سفارش‌ها > تخفیف‌ها).",
    "arch.t1_stat1_k": "میانگین زمان پردازش",
    "arch.t1_stat1_v": "کمتر از ۰.۵ میلی‌ثانیه",
    "arch.t1_stat2_k": "مصرف حافظه رم",
    "arch.t1_stat2_v": "بسیار ناچیز",
    "arch.t1_stat3_k": "درخواست شبکه",
    "arch.t1_stat3_v": "۰ (همیشه)",

    "arch.t2_num": "لایه ۲ · طبقه‌بندی تطبیقی",
    "arch.t2_title": "یادگیری روی دستگاه (Adaptive)",
    "arch.t2_tag1": "الگوریتم AdaGrad",
    "arch.t2_tag2": "ویژگی‌های هش‌شده",
    "arch.t2_desc": "تنها با یک بار اصلاح دسته‌بندی توسط شما، مدل سبک روی دستگاه آموزش می‌بیند. به دلیل هش کردن ویژگی‌ها، هیچ متن شخصی در وزن‌های مدل باقی نمی‌ماند.",
    "arch.t2_stat1_k": "حجم فایل وزن‌ها",
    "arch.t2_stat1_v": "۲.۴ مگابایت (خصوصی)",
    "arch.t2_stat2_k": "زمان به‌روزرسانی",
    "arch.t2_stat2_v": "کمتر از ۳ میلی‌ثانیه",
    "arch.t2_stat3_k": "شخصی‌سازی",
    "arch.t2_stat3_v": "آنی و محلی",

    "arch.t3_num": "لایه ۳ · مدل زبانی کوچک",
    "arch.t3_title": "هوش مصنوعی محلی دستی (Local SLM)",
    "arch.t3_tag1": "فقط با لمس کاربر",
    "arch.t3_tag2": "مدل دلخواه شما",
    "arch.t3_desc": "هوش مصنوعی کاملاً آفلاین اختیاری: فقط زمانی اجرا می‌شود که دکمه «توضیح» یا «خلاصه» را لمس کنید. بدون پس‌زمینه، با گرامرهای ساختاریافته GBNF.",
    "arch.t3_stat1_k": "نحوه اجرا",
    "arch.t3_stat1_v": "فقط دستی با لمس دکمه",
    "arch.t3_stat2_k": "قالب خروجی",
    "arch.t3_stat2_v": "گرامر بدون توهم GBNF",
    "arch.t3_stat3_k": "نشت به اینترنت",
    "arch.t3_stat3_v": "غیرممکن (فاقد مجوز)",

    /* Privacy */
    "priv.pill": "امنیت ذاتی در ساختار",
    "priv.title": "اثبات‌شده در عمل، نه در شعار",
    "priv.subtitle": "ما از شما نمی‌خواهیم به بیانیه حریم خصوصی اعتماد کنید؛ محرمانگی در کدها سخت‌گیرانه تعبیه شده و در CI به طور خودکار بازرسی می‌شود.",
    "priv.r1_title": "نبود مجوز اینترنت به طور مطلق",
    "priv.r1_desc": "هیچ‌کدام از مجوزهای INTERNET یا ACCESS_NETWORK_STATE در برنامه وجود ندارد. تست‌های گیت‌هاب در صورت وجود هرگونه کد ارتباط شبکه، بیلد را متوقف می‌کنند.",
    "priv.r2_title": "فاقد هرگونه کتابخانه آمار و تبلیغات",
    "priv.r2_desc": "هیچ ابزار ردیابی نظیر فایربیس، فیس‌بوک، سنتری یا کرش‌لیتیکس در برنامه وجود ندارد.",
    "priv.r3_title": "رمزنگاری پیشرفته SQLCipher ۲۵۶ بیتی",
    "priv.r3_desc": "تمام اعلان‌های ذخیره‌شده و اطلاعات بلیط‌ها در دیتابیس محلی با کلید رمزنگاری ۲۵۶ بیتی نگهداری می‌شوند.",
    "priv.r4_title": "عدم ذخیره مطلق رمزهای یکبارمصرف",
    "priv.r4_desc": "کدهای اعتبارسنجی بانکی و پیامک‌های ۲FA بلافاصله شناسایی شده و هرگز روی دیسک نوشته نمی‌شوند.",

    /* Bilingual */
    "bidi.pill": "دو خط، یک محصول اصیل",
    "bidi.title": "فارسی و انگلیسی هم‌تراز و درجه یک",
    "bidi.subtitle": "زبان فارسی یک ترجمه سرهم‌بندی‌شده نیست. هر دو زبان با تایپوگرافی اصیل و ابزارهای بومی زبان طراحی شده‌اند.",
    "bidi.item1": "موتور تقویم جلالی (شمسی) اختصاصی با آزمون‌های دقیق سال‌های ۱۴۰۰ تا ۱۴۲۰.",
    "bidi.item2": "استانداردسازی و یکدست‌سازی حروف فارسی (ی/ي، ک/ك و نیم‌فاصله‌های ZWNJ).",
    "bidi.item3": "فهم کلمات مقیاس فارسی (هزار، میلیون) و تبدیل‌های ارزهای ریال و تومان.",
    "bidi.item4": "بسته آزمایشی فینگلیش (fa-latn) با دیواره‌های ایمنی برای جلوگیری از خطای انگلیسی.",
    "bidi.cal_title": "تقویم خورشیدی جلالی",
    "bidi.cal_date": "۳ آبان ۱۴۰۵",
    "bidi.cal_desc": "استخراج خودکار تاریخ‌های انقضای فارسی («تا پایان امشب»، «جمعه آینده»).",

    /* Gallery */
    "gal.pill": "نمای اپلیکیشن",
    "gal.title": "طراحی آرامش‌بخش برای زندگی پرشتاب",
    "gal.subtitle": "نمای واقعی اپلیکیشن اندروید الک. برگرفته از برگه و جوهر، با تفکیک رنگی منظم و بدون شلوغی.",
    "gal.tab_all": "همه صفحات",
    "gal.tab_inbox": "صندوق اعلان‌ها",
    "gal.tab_wallet": "کیف تخفیف‌ها",
    "gal.tab_detail": "جزئیات و استدلال",
    "gal.tab_dark": "حالت شب",
    "gal.card1_title": "اینباکس هوشمند",
    "gal.card1_sub": "دسته‌بندی منظم اعلان‌ها با حاشیه‌های رنگی تفکیک‌شده.",
    "gal.card2_title": "کیف کدهای تخفیف",
    "gal.card2_sub": "کدهای تخفیف ذخیره‌شده با کپی یک‌لمسی و نوار انقضا.",
    "gal.card3_title": "صفحه جزئیات اعلان",
    "gal.card3_sub": "بلیط کامل تخفیف با نمایش دقیق علت دسته‌بندی.",
    "gal.card4_title": "حالت تیره (Dark Mode)",
    "gal.card4_sub": "پالت ملایم و کم‌نور برای آسودگی چشم در شب.",
    "gal.card5_title": "تنظیمات و دسترسی‌ها",
    "gal.card5_sub": "کنترل دقیق سطوح دریافت برای هر برنامه به طور مستقل.",
    "gal.card6_title": "مدیریت منابع اعلان",
    "gal.card6_sub": "انتخاب دقیق برنامه‌هایی که مایلید اعلان‌هایشان ذخیره شود.",

    /* Specs & Download */
    "specs.pill": "مشخصات فنی",
    "specs.title": "توسعه‌یافته با آخرین فناوری‌های منبع‌باز",
    "specs.platform": "سیستم عامل مقصد",
    "specs.platform_v": "اندروید ۷.۰ به بالا (API ۲۴ تا ۳۶)",
    "specs.framework": "چارچوب توسعه",
    "specs.framework_v": "دات‌نت ۱۰ و MAUI (net10.0-android36.0)",
    "specs.storage": "پایگاه داده محلی",
    "specs.storage_v": "SQLCipher SQLite + FTS5 (رمزنگاری ۲۵۶ بیتی)",
    "specs.perf": "سرعت طبقه‌بندی",
    "specs.perf_v": "کمتر از ۱.۸ میلی‌ثانیه روی پردازنده‌های ضعیف",
    "specs.license": "مجوز انتشار",
    "specs.license_v": "کدها: GPL-3.0 / داده‌ها: CC0-1.0",

    "down.title": "دریافت برنامه الک برای اندروید",
    "down.desc": "فایل نصبی رسمی امضا شده (APK) را مستقیماً از گیت‌هاب دانلود کنید یا با SDK دات‌نت ۱۰ آن را از سورس کد بسازید.",
    "down.btn_apk": "دانلود مستقیم فایل نصبی APK (arm64)",
    "down.btn_source": "مشاهده کدهای منبع در گیت‌هاب",
    "down.sha_label": "چک‌سام SHA-256 فایل:",

    /* Footer */
    "footer.desc": "الک به معنای غربال است — توری ظریفی که آنچه ارزشمند است نگه می‌دارد و مابقی را می‌ریزد. رایگان، متن‌باز و محرمانه برای همیشه.",
    "footer.links_title": "مخزن پروژه",
    "footer.docs_title": "مستندات فنی",
    "footer.copy": "© ۲۰۲۶ پروژه الک · منتشر شده تحت مجوز آزاد GPL-3.0"
  }
};

/* Simulator Preset Cases */
const presets = {
  snap: {
    title: "هدیه اولین سفارش",
    body: "کد تخفیف ۳۰٪ اولین سفارش شما: SNAPP30 — تا پایان امشب فرصت دارید"
  },
  bank: {
    title: "بانک ملت",
    body: "مبلغ ۲٬۵۰۰٬۰۰۰ ریال از حساب شما برداشت شد. مانده: ۱۵٬۴۰۰٬۰۰۰ ریال"
  },
  otp: {
    title: "بانک ملی ایران",
    body: "کد تأیید ورود شما: ۴۸۲۹۱۳ — این کد را هرگز به شخص دیگری ندهید."
  },
  ride: {
    title: "اسنپ",
    body: "سفیر شما رسید: کد پین ۴۸۲۱ را به راننده اعلام کنید."
  },
  dominos: {
    title: "Domino's Pizza",
    body: "Flash Sale! Extra 25% off with code SPRING25 · ends tonight at midnight"
  },
  slack: {
    title: "Slack · #design-review",
    body: "Sarah mentioned you: Can you review the updated Alak ticket stub mockups?"
  }
};

/* State Management */
let currentLang = localStorage.getItem('alak_lang') || 'en';
let currentTheme = localStorage.getItem('alak_theme') || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');

/* Initialize App on DOM Ready */
document.addEventListener('DOMContentLoaded', () => {
  initTheme();
  initLanguage();
  initSimulator();
  initGallery();
  initCopyActions();
});

/* Theme Handling */
function initTheme() {
  document.documentElement.setAttribute('data-theme', currentTheme);
  const themeBtn = document.getElementById('theme-toggle');
  if (themeBtn) {
    themeBtn.addEventListener('click', () => {
      currentTheme = currentTheme === 'dark' ? 'light' : 'dark';
      document.documentElement.setAttribute('data-theme', currentTheme);
      localStorage.setItem('alak_theme', currentTheme);
      updateThemeBtnIcon();
    });
    updateThemeBtnIcon();
  }
}

function updateThemeBtnIcon() {
  const themeBtn = document.getElementById('theme-toggle');
  if (themeBtn) {
    themeBtn.innerHTML = currentTheme === 'dark' 
      ? '<span>☀️ Light</span>' 
      : '<span>🌙 Dark</span>';
  }
}

/* Language Handling */
function initLanguage() {
  applyLanguage(currentLang);
  const langBtn = document.getElementById('lang-toggle');
  if (langBtn) {
    langBtn.addEventListener('click', () => {
      currentLang = currentLang === 'en' ? 'fa' : 'en';
      localStorage.setItem('alak_lang', currentLang);
      applyLanguage(currentLang);
    });
  }
}

function applyLanguage(lang) {
  document.documentElement.lang = lang;
  document.documentElement.dir = lang === 'fa' ? 'rtl' : 'ltr';

  const langBtn = document.getElementById('lang-toggle');
  if (langBtn) {
    langBtn.innerHTML = lang === 'en' 
      ? '<span>🇮🇷 فارسی</span>' 
      : '<span>🇬🇧 English</span>';
  }

  // Update all data-i18n elements
  document.querySelectorAll('[data-i18n]').forEach(el => {
    const key = el.getAttribute('data-i18n');
    if (i18nData[lang] && i18nData[lang][key]) {
      el.textContent = i18nData[lang][key];
    }
  });

  // Update placeholders
  document.querySelectorAll('[data-i18n-ph]').forEach(el => {
    const key = el.getAttribute('data-i18n-ph');
    if (i18nData[lang] && i18nData[lang][key]) {
      el.setAttribute('placeholder', i18nData[lang][key]);
    }
  });

  // Re-run simulator if active
  runSimulator();
}

/* ==========================================================================
   Client-Side Rule Classifier Simulation
   Mirrors Alak.Rules Tier-1 Logic
   ========================================================================== */
function initSimulator() {
  const presetChips = document.querySelectorAll('.preset-chip');
  const titleInput = document.getElementById('sim-input-title');
  const bodyInput = document.getElementById('sim-input-body');
  const siftBtn = document.getElementById('sim-btn-sift');

  presetChips.forEach(chip => {
    chip.addEventListener('click', () => {
      presetChips.forEach(c => c.classList.remove('active'));
      chip.classList.add('active');

      const presetKey = chip.getAttribute('data-preset');
      if (presets[presetKey]) {
        titleInput.value = presets[presetKey].title;
        bodyInput.value = presets[presetKey].body;
        runSimulator();
      }
    });
  });

  if (siftBtn) {
    siftBtn.addEventListener('click', runSimulator);
  }

  // Initial run with default preset
  if (presetChips[0]) {
    presetChips[0].click();
  }
}

function runSimulator() {
  const titleInput = document.getElementById('sim-input-title');
  const bodyInput = document.getElementById('sim-input-body');
  if (!titleInput || !bodyInput) return;

  const title = titleInput.value.trim();
  const body = bodyInput.value.trim();
  const fullText = (title + " " + body).toLowerCase();

  const startTime = performance.now();

  // Rule Evaluation
  let category = "Updates";
  let catColor = "var(--cat-updates)";
  let catTint = "var(--cat-updates-tint)";
  let isOtp = false;
  let promoCode = null;
  let discount = null;
  let moneyAmount = null;
  let ridePin = null;
  let evidence = [];

  // 1. Check OTP (Security first override rule RUL-018)
  const otpTerms = ["رمز یکبار", "کد تأیید", "کد ورود", "verification code", "otp", "do not share", "محرمانه"];
  for (const term of otpTerms) {
    if (fullText.includes(term.toLowerCase())) {
      isOtp = true;
      category = "Security";
      catColor = "var(--cat-security)";
      catTint = "var(--cat-security-tint)";
      evidence.push(`Matched security trigger "${term}" (+5.0)`);
      break;
    }
  }

  // 2. Check Ride PIN (Deliberately NOT treated as OTP)
  const rideTerms = ["سفیر", "راننده", "driver", "ride pin", "پین"];
  if (rideTerms.some(t => fullText.includes(t)) && /\b\d{4}\b/.test(fullText)) {
    const pinMatch = fullText.match(/\b\d{4}\b/);
    if (pinMatch) {
      ridePin = pinMatch[0];
      category = "Orders";
      catColor = "var(--cat-orders)";
      catTint = "var(--cat-orders-tint)";
      isOtp = false; // Ride PIN is safe
      evidence.push(`Identified transit ride PIN "${ridePin}" (not an OTP security risk)`);
    }
  }

  // 3. Check Offers & Promo Codes
  if (!isOtp) {
    const promoTerms = ["کد تخفیف", "تخفیف", "off", "code", "promo", "voucher", "هدیه", "deal"];
    let promoScore = 0;
    for (const pt of promoTerms) {
      if (fullText.includes(pt)) {
        promoScore += 2.5;
        evidence.push(`Matched promo anchor "${pt}" (+2.5)`);
      }
    }

    // Code regex pattern (e.g. SNAPP30, SPRING25)
    const codeMatch = (title + " " + body).match(/\b[A-Z0-9]{4,12}\b/);
    if (codeMatch && !isOtp && !ridePin) {
      // Exclude common false positives
      const candidate = codeMatch[0];
      if (!["SLACK", "ALERT", "HTTP", "HTTPS", "ORDER"].includes(candidate)) {
        promoCode = candidate;
        evidence.push(`Extracted alphanumeric coupon token "${promoCode}"`);
      }
    }

    // Discount percentage
    const discountMatch = fullText.match(/(\d{1,2})\s*[%٪]/);
    if (discountMatch) {
      discount = discountMatch[1] + "%";
      evidence.push(`Extracted discount figure "${discount}"`);
    }

    if (promoScore > 2.0 || promoCode) {
      category = "Offers";
      catColor = "var(--cat-offers)";
      catTint = "var(--cat-offers-tint)";
    }
  }

  // 4. Check Money / Banking
  if (!isOtp && category !== "Offers") {
    const moneyTerms = ["ریال", "تومان", "واریز", "برداشت", "irr", "irt", "$", "usd", "debited", "charged", "حساب"];
    for (const mt of moneyTerms) {
      if (fullText.includes(mt)) {
        category = "Money";
        catColor = "var(--cat-money)";
        catTint = "var(--cat-money-tint)";
        evidence.push(`Matched financial transaction term "${mt}" (+3.2)`);
        break;
      }
    }
    const amtMatch = fullText.match(/([۰-۹0-9,٬]+)\s*(ریال|تومان|irr|usd|\$)/i);
    if (amtMatch) {
      moneyAmount = amtMatch[1] + " " + amtMatch[2];
    }
  }

  // 5. Check Work
  if (category === "Updates") {
    const workTerms = ["جلسه", "meeting", "review", "slack", "pull request", "pr #", "jira"];
    for (const wt of workTerms) {
      if (fullText.includes(wt)) {
        category = "Work";
        catColor = "var(--cat-work)";
        catTint = "var(--cat-work-tint)";
        evidence.push(`Matched collaboration term "${wt}" (+2.8)`);
        break;
      }
    }
  }

  const duration = (performance.now() - startTime).toFixed(2);

  // Render Simulator UI
  renderSimulatorResults({
    category,
    catColor,
    catTint,
    isOtp,
    promoCode,
    discount,
    moneyAmount,
    ridePin,
    evidence,
    duration,
    sender: title || "App"
  });
}

function renderSimulatorResults(res) {
  const badge = document.getElementById('sim-cat-badge');
  const latency = document.getElementById('sim-latency-val');
  const otpCard = document.getElementById('sim-otp-alert');
  const factsGrid = document.getElementById('sim-facts-matrix');
  const stubArea = document.getElementById('sim-stub-area');
  const evidenceBox = document.getElementById('sim-evidence-receipt');

  if (badge) {
    badge.textContent = res.category;
    badge.style.backgroundColor = res.catTint;
    badge.style.color = res.catColor;
  }

  if (latency) {
    latency.textContent = `${res.duration} ms`;
  }

  // OTP Safety Alert
  if (otpCard) {
    otpCard.style.display = res.isOtp ? 'flex' : 'none';
  }

  // Facts Matrix
  if (factsGrid) {
    factsGrid.innerHTML = '';
    const facts = [];
    if (res.promoCode) facts.push({ k: currentLang === 'fa' ? 'کد تخفیف' : 'Promo Code', v: res.promoCode, mono: true });
    if (res.discount) facts.push({ k: currentLang === 'fa' ? 'درصد تخفیف' : 'Discount', v: res.discount });
    if (res.moneyAmount) facts.push({ k: currentLang === 'fa' ? 'مبلغ' : 'Amount', v: res.moneyAmount });
    if (res.ridePin) facts.push({ k: currentLang === 'fa' ? 'کد سفر' : 'Ride PIN', v: res.ridePin, mono: true });
    facts.push({ k: currentLang === 'fa' ? 'فرستنده' : 'Source', v: res.sender });

    facts.forEach(f => {
      const card = document.createElement('div');
      card.className = 'fact-item';
      card.innerHTML = `
        <span class="fact-label">${f.k}</span>
        <span class="fact-val ${f.mono ? 'font-mono' : ''}">${f.v}</span>
      `;
      factsGrid.appendChild(card);
    });
  }

  // Stub Generator
  if (stubArea) {
    if (res.promoCode) {
      stubArea.style.display = 'block';
      stubArea.innerHTML = `
        <div class="ticket-stub" style="border-left-color: ${res.catColor}; border-right-color: ${res.catColor};">
          <div class="stub-header">
            <div class="stub-brand-info">
              <span>🎟️ ${res.sender}</span>
            </div>
            <span class="stub-category-tag" style="background-color: ${res.catTint}; color: ${res.catColor};">${res.category}</span>
          </div>
          <div class="stub-body">
            <div class="stub-code-block">
              <span class="stub-code-text font-mono">${res.promoCode}</span>
              <button class="stub-copy-btn" onclick="copyStubCode('${res.promoCode}', this)" title="Copy Code">
                <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>
              </button>
            </div>
            <span class="stub-discount-chip" style="color: ${res.catColor};">${res.discount || 'Special'}</span>
          </div>
          <div class="stub-expiry-track">
            <div class="stub-expiry-fill urgent"></div>
          </div>
          <div class="stub-footer">
            <span>⏰ ${currentLang === 'fa' ? 'انقضا: تا پایان امشب' : 'Expires: Tonight 23:59'}</span>
            <span>✓ ${currentLang === 'fa' ? 'آماده استفاده' : 'Ready to use'}</span>
          </div>
        </div>
      `;
    } else {
      stubArea.style.display = 'none';
      stubArea.innerHTML = '';
    }
  }

  // Evidence Trace
  if (evidenceBox) {
    evidenceBox.innerHTML = `
      <div class="evidence-title">Decision Trace (Aho-Corasick + Log-Odds):</div>
      ${res.evidence.map(e => `<div>→ ${e}</div>`).join('')}
      <div style="margin-top: 4px; color: var(--text-tertiary);">Status: Confident match resolved in Tier-1.</div>
    `;
  }
}

/* Copy Actions */
function initCopyActions() {
  document.querySelectorAll('.btn-copy-cmd').forEach(btn => {
    btn.addEventListener('click', () => {
      const targetId = btn.getAttribute('data-copy-target');
      const targetEl = document.getElementById(targetId);
      if (targetEl) {
        navigator.clipboard.writeText(targetEl.textContent.trim()).then(() => {
          const originalText = btn.textContent;
          btn.textContent = currentLang === 'fa' ? 'کپی شد!' : 'Copied!';
          setTimeout(() => {
            btn.textContent = originalText;
          }, 1800);
        });
      }
    });
  });
}

function copyStubCode(code, btn) {
  navigator.clipboard.writeText(code).then(() => {
    const originalSvg = btn.innerHTML;
    btn.innerHTML = `<span style="font-size: 11px; font-weight: 700; color: var(--cat-people);">${currentLang === 'fa' ? 'کپی شد' : 'Copied'}</span>`;
    setTimeout(() => {
      btn.innerHTML = originalSvg;
    }, 1500);
  });
}

/* Gallery Lightbox & Filter */
function initGallery() {
  const modal = document.getElementById('lightbox-modal');
  const modalImg = document.getElementById('lightbox-img');
  const closeBtn = document.getElementById('lightbox-close');

  if (modal && modalImg && closeBtn) {
    document.querySelectorAll('.gallery-item').forEach(item => {
      item.addEventListener('click', () => {
        const fullSrc = item.getAttribute('data-full-img');
        if (fullSrc) {
          modalImg.src = fullSrc;
          modal.classList.add('active');
        }
      });
    });

    closeBtn.addEventListener('click', () => {
      modal.classList.remove('active');
    });

    modal.addEventListener('click', (e) => {
      if (e.target === modal) {
        modal.classList.remove('active');
      }
    });
  }

  // Filter Tabs
  const filterBtns = document.querySelectorAll('.filter-btn');
  filterBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      filterBtns.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      const filter = btn.getAttribute('data-filter');

      document.querySelectorAll('.gallery-item').forEach(item => {
        if (filter === 'all' || item.getAttribute('data-category') === filter) {
          item.style.display = 'flex';
        } else {
          item.style.display = 'none';
        }
      });
    });
  });
}
