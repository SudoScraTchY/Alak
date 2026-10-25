/* ==========================================================================
   Alak (الک) — Vibrant Web Engine
   Full Bilingual English & Persian Engine + Interactive Gallery & Stubs
   ========================================================================== */

const i18nData = {
  en: {
    /* Brand & Nav */
    "nav.features": "Features",
    "nav.wallet": "Ticket Stub",
    "nav.architecture": "3-Tier AI",
    "nav.privacy": "Privacy Audit",
    "nav.gallery": "Screenshots",
    "nav.download": "Download APK",
    "brand.tagline": "Sift the noise · On-device notification intelligence",

    /* Hero */
    "hero.pill": "⚡ 100% Offline · Zero Internet Permission · .NET 10 MAUI",
    "hero.title_pre": "Sift the noise.",
    "hero.title_post": "Keep what matters.",
    "hero.desc": "Your notification shade is volatile chaos. Alak is a private, on-device intelligence engine that captures notifications, extracts promo codes & discounts into physical Ticket Stubs, safely wipes OTPs, and never sends a single byte to any cloud.",
    "hero.cta_download": "Download APK (v0.8 Alpha)",
    "hero.cta_github": "View on GitHub",
    "hero.meta_offline": "Zero Network Permission",
    "hero.meta_crypto": "SQLCipher 256-bit AES",
    "hero.meta_speed": "< 1.5ms Rule Match",

    /* Metrics */
    "metric.m1_val": "< 0.5 ms",
    "metric.m1_lbl": "Rule Match Latency",
    "metric.m2_val": "0 KB",
    "metric.m2_lbl": "Network Traffic (Always)",
    "metric.m3_val": "256-bit",
    "metric.m3_lbl": "SQLCipher AES",
    "metric.m4_val": "100%",
    "metric.m4_lbl": "Open Source GPL-3",

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
    "bidi.cal_date": "۴ آبان ۱۴۰۵",
    "bidi.cal_desc": "Automatic extraction of Persian expiry dates ('تا پایان امشب', 'جمعه آینده').",

    /* Gallery */
    "gal.pill": "Device Showcase",
    "gal.title": "Crafted for Daily Calm",
    "gal.subtitle": "Explore the official Android UI captured directly from device audits. Paper textures, clear colored spines, and zero clutter.",
    "gal.tab_all": "All Screens",
    "gal.tab_inbox": "Inbox & Triage",
    "gal.tab_wallet": "Codes Wallet",
    "gal.tab_detail": "Detail & Evidence",
    "gal.tab_settings": "Settings & Rules",
    "gal.card1_title": "Smart Triage Inbox (Light)",
    "gal.card1_sub": "Categorized notification stack with colored spines.",
    "gal.card2_title": "Smart Triage Inbox (Dark)",
    "gal.card2_sub": "Deep night palette with low glare.",
    "gal.card3_title": "Codes Wallet (Light)",
    "gal.card3_sub": "Saved coupon stubs with instant one-tap copy.",
    "gal.card4_title": "Codes Wallet (Dark)",
    "gal.card4_sub": "Active and expiring discounts categorized.",
    "gal.card5_title": "Ticket Stub Detail View",
    "gal.card5_sub": "Full ticket stub with transparent evidence trace.",
    "gal.card6_title": "Night Stub Detail",
    "gal.card6_sub": "Calm receipt-like evidence explanations.",
    "gal.card7_title": "System Settings (Light)",
    "gal.card7_sub": "Theme, Jalali calendar, and privacy options.",
    "gal.card8_title": "User Rules Engine (Dark)",
    "gal.card8_sub": "Custom rules to route senders automatically.",

    /* Download */
    "down.title": "Get Alak for Android",
    "down.desc": "Download the official signed release APK directly from GitHub or build from source using the .NET 10 SDK.",
    "down.btn_apk": "Download Alak APK (arm64)",
    "down.btn_source": "View Release v0.8.0-alpha",
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
    "nav.wallet": "بلیط تخفیف",
    "nav.architecture": "معماری سه‌لایه",
    "nav.privacy": "امنیت و حریم خصوصی",
    "nav.gallery": "تصاویر برنامه",
    "nav.download": "دانلود برنامه",
    "brand.tagline": "غربال نویزها · دستیار اعلان‌های هوشمند و محرمانه",

    /* Hero */
    "hero.pill": "⚡ کاملاً آفلاین · بدون دسترسی اینترنت · دات‌نت ۱۰ و MAUI",
    "hero.title_pre": "غربالِ نویزهای بی‌پایان.",
    "hero.title_post": "آنچه مهم است می‌ماند.",
    "hero.desc": "نوار اعلان‌های گوشی پر از هیاهوی فرّار و شلوغ است. الک دستیار محرمانه شما روی دستگاه است که اعلان‌ها را دسته‌بندی می‌کند، کدهای تخفیف را در قالب «بلیط تخفیف» نجات می‌دهد، رمزهای یکبارمصرف را بی‌درنگ پاکسازی می‌کند و هرگز حتی یک بایت داده به اینترنت نمی‌فرستد.",
    "hero.cta_download": "دانلود فایل نصبی APK (نسخه ۰.۸ آلفا)",
    "hero.cta_github": "مشاهده در گیت‌هاب",
    "hero.meta_offline": "بدون مجوز اینترنت (Offline)",
    "hero.meta_crypto": "رمزنگاری SQLCipher ۲۵۶ بیتی",
    "hero.meta_speed": "شناسایی در کمتر از ۱.۵ میلی‌ثانیه",

    /* Metrics */
    "metric.m1_val": "۰.۵ میلی‌ثانیه",
    "metric.m1_lbl": "سرعت تطبیق قوانین",
    "metric.m2_val": "۰ بایت",
    "metric.m2_lbl": "ترافیک اینترنت (همیشه صفر)",
    "metric.m3_val": "۲۵۶ بیتی",
    "metric.m3_lbl": "رمزنگاری دیتابیس AES",
    "metric.m4_val": "۱۰۰٪",
    "metric.m4_lbl": "منبع‌باز با مجوز GPL-3",

    /* Stub Section */
    "stub.pill": "طراحی اختصاصی",
    "stub.title": "بلیط تخفیف (The Ticket Stub)",
    "stub.subtitle": "چرا کارت‌های تکراری و یکنواخت؟ الک با کدهای تخفیف مانند بلیط‌های کاغذی فیزیکی رفتار می‌کند: گوشه‌های برش‌خورده، قلم مونو برای دقت در کپی، و نوار زمانی اتمام مهلت.",
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
    "bidi.cal_date": "۴ آبان ۱۴۰۵",
    "bidi.cal_desc": "استخراج خودکار تاریخ‌های انقضای فارسی («تا پایان امشب»، «جمعه آینده»).",

    /* Gallery */
    "gal.pill": "نمای واقعی اپلیکیشن",
    "gal.title": "طراحی آرامش‌بخش برای زندگی پرشتاب",
    "gal.subtitle": "تصاویر واقعی استخراج شده از آزمایشگاه نسخه اندروید. برگه و جوهر، تفکیک رنگی منظم و کاملاً کاربردی.",
    "gal.tab_all": "همه صفحات",
    "gal.tab_inbox": "صندوق اعلان‌ها",
    "gal.tab_wallet": "کیف تخفیف‌ها",
    "gal.tab_detail": "جزئیات و استدلال",
    "gal.tab_settings": "تنظیمات و قوانین",
    "gal.card1_title": "اینباکس هوشمند (روشن)",
    "gal.card1_sub": "دسته‌بندی منظم اعلان‌ها با حاشیه‌های رنگی تفکیک‌شده.",
    "gal.card2_title": "اینباکس هوشمند (تیره)",
    "gal.card2_sub": "پالت ملایم و کم‌نور برای آسودگی چشم در شب.",
    "gal.card3_title": "کیف کدهای تخفیف (روشن)",
    "gal.card3_sub": "کدهای تخفیف ذخیره‌شده با کپی یک‌لمسی و نوار انقضا.",
    "gal.card4_title": "کیف کدهای تخفیف (تیره)",
    "gal.card4_sub": "مدیریت کدهای تخفیف فعال و در آستانه انقضا.",
    "gal.card5_title": "صفحه جزئیات بلیط تخفیف",
    "gal.card5_sub": "بلیط کامل تخفیف با نمایش شفاف دلایل استدلال هوش مصنوعی.",
    "gal.card6_title": "جزئیات بلیط در حالت شب",
    "gal.card6_sub": "استدلال شفاف بدون ابهام در پالت تیره.",
    "gal.card7_title": "تنظیمات سیستم (روشن)",
    "gal.card7_sub": "شخصی‌سازی ظاهر، تقویم جلالی و گزینه‌های حریم خصوصی.",
    "gal.card8_title": "موتور قوانین کاربر (تیره)",
    "gal.card8_sub": "تنظیم قوانین اختصاصی برای دسته‌بندی مستقیم فرستنده‌ها.",

    /* Download */
    "down.title": "دریافت برنامه الک برای اندروید",
    "down.desc": "فایل نصبی رسمی امضا شده (APK) را مستقیماً از گیت‌هاب دانلود کنید یا با SDK دات‌نت ۱۰ آن را از سورس کد بسازید.",
    "down.btn_apk": "دانلود مستقیم فایل نصبی APK (arm64)",
    "down.btn_source": "مشاهده نسخه v0.8.0-alpha در گیت‌هاب",
    "down.sha_label": "چک‌سام SHA-256 فایل:",

    /* Footer */
    "footer.desc": "الک به معنای غربال است — توری ظریفی که آنچه ارزشمند است نگه می‌دارد و مابقی را می‌ریزد. رایگان، متن‌باز و محرمانه برای همیشه.",
    "footer.links_title": "مخزن پروژه",
    "footer.docs_title": "مستندات فنی",
    "footer.copy": "© ۲۰۲۶ پروژه الک · منتشر شده تحت مجوز آزاد GPL-3.0"
  }
};

/* State Management */
let currentLang = localStorage.getItem('alak_lang') || 'en';
let currentTheme = localStorage.getItem('alak_theme') || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');

/* Initialize App on DOM Ready */
document.addEventListener('DOMContentLoaded', () => {
  initTheme();
  initLanguage();
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
}

/* Copy Actions */
function initCopyActions() {
  document.querySelectorAll('.stub-copy-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const code = btn.getAttribute('data-code');
      if (code) {
        copyStubCode(code, btn);
      }
    });
  });
}

function copyStubCode(code, btn) {
  navigator.clipboard.writeText(code).then(() => {
    const originalSvg = btn.innerHTML;
    btn.innerHTML = `<span style="font-size: 11px; font-weight: 800; color: var(--cat-people);">${currentLang === 'fa' ? 'کپی شد' : 'Copied'}</span>`;
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
    document.querySelectorAll('.gallery-card').forEach(item => {
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

      document.querySelectorAll('.gallery-card').forEach(item => {
        if (filter === 'all' || item.getAttribute('data-category') === filter) {
          item.style.display = 'flex';
        } else {
          item.style.display = 'none';
        }
      });
    });
  });
}
