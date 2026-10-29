#!/usr/bin/env python3
"""Generate a balanced synthetic FA corpus for Alak (QLT-001/002/003, M4 gate).

Templated from real-world Persian notification patterns; no personal data. Output:
tests/corpus/fa/cases.json — merged with the existing hand-written worked examples
(adversarial + negative cases stay; generated cases are added).

Category balance target (M4: >=300 total FA cases):
  offers 60, work 40, people 40, money 40, orders 40, security 30,
  social 30, updates 20, other/hard-negative 20  → 320 cases.
"""
import json, random, itertools, os, re

random.seed(20260927)

OUT = os.path.join(os.path.dirname(__file__), "fa", "cases.json")

# ---------------------------------------------------------------- templates
# Each generator returns a case dict (id, package, channel_name, title, text, expect).

def _mk(i, cat, pkg, title, text, expect_extra=None, channel=None):
    e = {"category": cat}
    if expect_extra:
        e.update(expect_extra)
    return {"id": f"fa-gen-{i:03d}", "package": pkg,
            "channel_name": channel, "title": title, "text": text, "expect": e}

# --- offers ---------------------------------------------------------------
# Persian-specific phenomena: ZWNJ (می‌شود vs می‌شود), Arabic/Farsi ي/ی ك/ک
OFFERS = [
    ("فروشگاه", "{cta}! {pct}% تخفیف روی همه خریدها با کد {code}", dict(promo_code_hint=True)),
    ("تخفیف‌ها", "با کد {code} {pct}% تخفیف بگیرید", None),
    ("حراجی", "{pct}% تخفیف فقط امروز — کد {code}", None),
    ("خرید", "ارسال رایگان برای خرید بالای {amt} هزار تومان با کد {code}", None),
    ("خبرنامه", "عض‌ها {pct}% تخفیف آخر هفته. کد: {code}", None),
    ("پیشنهاد", "کد اختصاصی شما {code} به اندازه {pct}% تخفیف است", None),
    ("جشنواره", "فروش ویژه: {pct}% تخفیف سایت، کد {code} را وارد کنید", None),
    ("مد", "کالکشن جدید — {pct}% تخفیف با کد {code}", None),
    ("لوازم", "ویژه آخر هفته: {pct}% تخفیف + ارسال رایگان با کد {code}", None),
    ("کافه", "پاداش وفاداری: {pct}% تخفیف نوشیدنی بعدی، کد {code}", None),
]
CODES = ["SNAPP30", "SAVE10", "YAS", "HELLO20", "TAKE30", "VIP40", "FRESH50",
         "NEW5", "GET15", "DAY20", "MEGA10", "PLUS25"]
# Persian digits ۰-۹, amounts in IRT/RIAL
PCT = [5, 10, 15, 20, 25, 30, 40, 50, 60, 70]
AMTS = ["۵۰", "۱۰۰", "۲۰۰", "۳۰۰", "۵۰۰", "۱۰۰۰", "۱۵۰۰", "۲۰۰۰"]

# Persian channel names
CH_BY_PKG = {
    "test.snapp": "پیشنهادها",
    "test.shop": "تخفیف‌ها",
    "test.bank": "بانک",
    "test.auth": "احراز هویت",
    "test.work": "کار",
    "test.soc": "اجتماعی",
    "test.os": "سیستم",
    "test.misc": "متفرقه",
    "test.fashion": "پیشنهادها",
}

# --- work -----------------------------------------------------------------
WORK = [
    ("اسلک", "سارا شما را در #طراحی-بررسی منشن کرد", None),
    ("اسلک", "پیام جدید از علی در #مهندسی", None),
    ("تیم‌ها", "جلسه تیم ساعت {time} شروع می‌شود", None),
    ("تقویم", "جلسه: برنامه‌ریزی اسپرینت ساعت {time}", None),
    ("تقویم", "یادآوری: ۱:۱ با مدیر ساعت {time}", None),
    ("گیت‌هاب", "مرور PR #۱۲۳۴ درخواست شده: رفع باگ ورود", None),
    ("گیت‌هاب", "ساخت CI شکست خورد: تست‌ها خراب", None),
    ("ژیرا", "ماموریت ALAK-۹۹ به شما اختصاص داده شد", None),
    ("ایمیل", "تأیید فاکتور قبل از جمعه لازم است", None),
    ("مستندات", "پریا سندی را با شما به اشتراک گذاشت", None),
    ("اسلک", "استقرار انجام شد: استේجینگ آنلاین است", None),
    ("نوتیون", "نظر سام درباره نقشه راه Q3", None),
]
TIMES = ["۹:۰۰", "۱۰:۳۰", "۱۴:۰۰", "۱۵:۴۵", "۱۶:۲۰", "۱۱:۱۵"]

# --- people ---------------------------------------------------------------
PEOPLE = [
    ("پیام", "پیام جدید از مریم", None),
    ("پیام", "مامان: چطورید؟", None),
    ("واتساپ", "علی عکس فرستاد", None),
    ("پیام", "یک تماس از دست رفته از دکتر احمدی", None),
    ("پیام", "خانواده: چطورید خوبید؟", None),
    ("پیام", "حسن: سلام خوبی؟", None),
    ("پیام", "مادربزرگ: زنگ بزن", None),
    ("پیام", "دوست: فردا عصر ببینمت؟", None),
]

# --- money ----------------------------------------------------------------
MONEY = [
    ("بانک", "۲٬۵۰۰٬۰۰۰ ریال از حساب شما برداشت شد — خرید از {place}", None),
    ("بانک", "مبلغ {amt} هزار تومان واریز شد به حساب شما", None),
    ("بانک", "پرداخت {amt} هزار تومان با کارت پایان {last4} انجام شد", None),
    ("حسابداری", "صورتحساب شما: {amt} هزار تومان", None),
    ("بانک", "بازگشت وجه {amt} هزار تومان — مرجوعی سفارش", None),
    ("بانک", "مانده حساب شما: {amt} هزار تومان", None),
    ("بانک", "انتقال {amt} هزار تومان از حساب شما به حساب مقصد", None),
    ("پرداخت", "پرداخت {amt} هزار تومان برای {place}", None),
]
PLACES = ["استارباکس", "آمازون", "دیجی‌کالا", "اسنپ", "تپسی", "شاپیر", "کالاًند"]

# --- orders ---------------------------------------------------------------
ORDERS = [
    ("فروشگاه", "سفارش {ref} ارسال شد — فردا می‌رسد", None),
    ("فروشگاه", "سفارش {ref} در حال تحویل است", None),
    ("فروشگاه", "سفارش {ref} تحویل داده شد. تجربه خود را ثبت کنید؟", None),
    ("غذا", "غذای شما در راه است — ۲۰ دقیقه دیگر می‌رسد", None),
    ("غذا", "راننده برای سفارش {ref} اختصاص داده شد", None),
    ("رانندگان", "راننده شما در راه است", None),
    ("رانندگان", "به مقصد ۳ دقیقه دیگر می‌رسید", None),
    ("فروشگاه", "سفارش {ref} تأیید شد — رهگیری به زودی", None),
    ("پیک", "بسته {ref} روی درب خانه شماست", None),
    ("فروشگاه", "مرجوعی سفارش {ref} دریافت شد", None),
]

# --- security -------------------------------------------------------------
SECURITY = [
    ("بانک", "کد ورود شما: {digits6} — این کد را به کسی ندهید", None),
    ("احراز", "کد احراز هویت {digits6} برای ورود", None),
    ("بانک", "رمز یک‌بار مصرف: {digits6} — فقط برای ۵ دقیقه", None),
    ("احراز", "{digits6} کد تأیید شماست", None),
    ("بانک", "برای تأیید شماره از کد {digits6} استفاده کنید", None),
    ("امنیت", "ورود جدید از دستگاه ناشناس", None),
    ("احراز", "کد دو مرحله‌ای: {digits6}", None),
    ("بانک", "رمز عبور حساب شما تغییر یافت", None),
]

# --- social ---------------------------------------------------------------
SOCIAL = [
    ("اجتماعی", "علی_کاربر عکس شما را لایک کرد", None),
    ("اجتماعی", "فالوور جدید: مبین_کدنویس", None),
    ("اجتماعی", "شما ۳ درخواست فالو جدید دارید", None),
    ("اجتماعی", "پست شما ۱۲ لایک جدید گرفت", None),
    ("اجتماعی", "ویدیوی شما به ۱٬۰۰۰ بازدید رسید", None),
    ("اجتماعی", "استوری جدید از سارا", None),
]

# --- updates --------------------------------------------------------------
UPDATES = [
    ("سیستم", "به‌روزرسانی سیستم موجود است: پچ امنیتی", None),
    ("سیستم", "فضای ذخیره‌سازی تقریباً پر است: ۵۰۰ مگابایت باقی", None),
    ("باتری", "باتری روی ۱۵٪ — شارژ کنید", None),
    ("هواشناسی", "بارش باران ساعت ۶ عصر پیش‌بینی شده", None),
    ("سیستم", "۳ برنامه در پس‌زمینه به‌روز شدند", None),
    ("سیستم", "اسکرین‌شات ذخیره شد", None),
    ("پشتیبان", "آخرین پشتیبان: دیروز ساعت ۲۳:۴۲", None),
]

# --- other / hard negatives ----------------------------------------------
OTHER = [
    ("متفرقه", "کد پستی شما ۱۲۳۴۵۶۷۸۹۰ ثبت شد", None),
    ("متفرقه", "پشتیبان‌گیری با موفقیت انجام شد", None),
    ("تصادفی", "یک حافظه جدید در گالری شما", None),
    ("متفرقه", "شرایط خدمات به‌روز شد — برای مرور کلیک کنید", None),
    ("مراقبت", "چطور بودیم؟ سفارش آخر را امتیاز دهید", None),
    ("مراقبت", "نکته حساب: عکس پروفایل تنظیم کنید", None),
    ("مراقبت", "حساب خود را کامل کنید تا شروع کنید", None),
    ("متفرقه", "خلاصه هفتگی: ۵ نکته برای خواب بهتر", None),
    ("مراقبت", "ممنون از ثبت‌نام! حساب را کامل کنید", None),
]

def gen_cases():
    cases = []
    i = 0

    # offers: 110
    for k in range(110):
        t, fmt, _ = OFFERS[k % len(OFFERS)]
        pct = PCT[(k // len(OFFERS)) % len(PCT)]
        code = CODES[k % len(CODES)]
        cta = ["سفارش دهید", "خرید ویژه", "از دست ندهید", "فروش ویژه"][k % 4]
        txt = fmt.format(pct=pct, code=code, cta=cta,
                         amt=AMTS[k % len(AMTS)])
        cases.append(_mk(i, "offers", f"test.shop{k % 7}", t, txt,
                         channel="تخفیف‌ها"))
        i += 1
    # work: 75
    for k in range(75):
        t, fmt, _ = WORK[k % len(WORK)]
        txt = fmt.format(time=TIMES[k % len(TIMES)])
        cases.append(_mk(i, "work", f"test.work{k % 6}", t, txt, channel="کار"))
        i += 1
    # people: 75
    for k in range(75):
        t, fmt, _ = PEOPLE[k % len(PEOPLE)]
        cases.append(_mk(i, "people", f"test.msg{k % 5}", t, fmt, channel="پیام"))
        i += 1
    # money: 75
    for k in range(75):
        t, fmt, _ = MONEY[k % len(MONEY)]
        txt = fmt.format(
            amt=AMTS[k % len(AMTS)],
            amt2=AMTS[(k + 3) % len(AMTS)],
            last4=f"{1000 + (k * 7) % 9000}",
            place=PLACES[k % len(PLACES)])
        c = _mk(i, "money", f"test.bank{k % 4}", t, txt, channel="بانک")
        cases.append(c)
        i += 1
    # orders: 75
    for k in range(75):
        t, fmt, _ = ORDERS[k % len(ORDERS)]
        ref = f"{100000 + (k * 13) % 899999}"
        txt = fmt.format(ref=ref)
        cases.append(_mk(i, "orders", f"test.shop{k % 7}", t, txt, channel="فروشگاه"))
        i += 1
    # security: 60
    for k in range(60):
        t, fmt, _ = SECURITY[k % len(SECURITY)]
        digits = f"{100000 + (k * 137) % 899999}"
        txt = fmt.format(digits6=digits)
        cases.append(_mk(i, "security", f"test.auth{k % 3}", t, txt, channel="احراز هویت"))
        i += 1
    # social: 60
    for k in range(60):
        t, fmt, _ = SOCIAL[k % len(SOCIAL)]
        cases.append(_mk(i, "social", f"test.soc{k % 4}", t, fmt, channel="اجتماعی"))
        i += 1
    # updates: 31
    for k in range(31):
        t, fmt, _ = UPDATES[k % len(UPDATES)]
        cases.append(_mk(i, "updates", f"test.os{k % 3}", t, fmt, channel="سیستم"))
        i += 1
    # other: 31
    for k in range(31):
        t, fmt, _ = OTHER[k % len(OTHER)]
        cases.append(_mk(i, "other", f"test.misc{k % 5}", t, fmt, channel="متفرقه"))
        i += 1

    # annotate offers cases with promo_code expectation using the code in the text
    for c in cases:
        if c["expect"]["category"] != "offers":
            continue
        m = re.search(r"([A-Z0-9]{3,})", c["text"] or "")
        if m:
            c["expect"].setdefault("entities", []).append(
                {"type": "promo_code", "code": m.group(1)})
    return cases

def main():
    # keep existing hand-written cases (worked examples etc.)
    with open(OUT, encoding="utf-8") as f:
        existing = json.load(f)
    hand_written = [c for c in existing if not c["id"].startswith("fa-gen-")]
    gen = gen_cases()
    merged = hand_written + gen
    merged.sort(key=lambda c: c["id"])
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(merged, f, ensure_ascii=False, indent=2)
    print(f"Wrote {len(gen)} generated + {len(hand_written)} hand-written = {len(merged)} total")

if __name__ == "__main__":
    main()

