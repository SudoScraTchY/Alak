#!/usr/bin/env python3
"""Generate a balanced synthetic EN corpus for Alak (QLT-001/002/003, M3 gate).

Templated from real-world notification patterns; no personal data. Output:
tests/corpus/en/cases.json — merged with the existing hand-written worked examples
(adversarial + negative cases stay; generated cases are added).

Category balance target (M3: 300 total EN cases):
  offers 60, work 40, people 40, money 40, orders 40, security 30,
  social 30, updates 20, other/hard-negative 20  → 320 cases.
"""
import json, random, itertools, os, re

random.seed(20260926)

OUT = os.path.join(os.path.dirname(__file__), "en", "cases.json")

# ---------------------------------------------------------------- templates
# Each generator returns a case dict (id, package, channel_name, title, text, expect).

def _mk(i, cat, pkg, title, text, expect_extra=None, channel=None):
    e = {"category": cat}
    if expect_extra:
        e.update(expect_extra)
    return {"id": f"en-gen-{i:03d}", "package": pkg,
            "channel_name": channel, "title": title, "text": text, "expect": e}

# --- offers ---------------------------------------------------------------
OFFERS = [
    ("Store", "{cta}! {pct}% off everything with code {code}", dict(promo_code_hint=True)),
    ("Deals", "Use code {code} at checkout for {pct}% off your order", None),
    ("Sale", "{pct}% off today only — code {code}", None),
    ("Shop", "Free shipping on orders over ${amt} with code {code}", None),
    ("Newsletter", "Members get {pct}% off this weekend. Code: {code}", None),
    ("Boutique", "Your exclusive code {code} takes {pct}% off", None),
    ("Market", "Flash sale: {pct}% off sitewide, apply code {code} at checkout", None),
    ("Fashion", "New arrivals are here — {pct}% off with promo code {code}", None),
    ("Gadget", "Weekend deal: {pct}% off + free shipping with code {code}", None),
    ("Coffee", "Your loyalty reward: {pct}% off your next drink, code {code}", None),
]
CODES = ["SPRING25", "SAVE10", "WELCOME15", "HELLO20", "TAKE30", "VIP40", "FRESH50",
         "NEW5", "GET15", "DAY20", "MEGA10", "PLUS25"]

# --- work -----------------------------------------------------------------
WORK = [
    ("Slack", "Sarah mentioned you in #design-review", None),
    ("Slack", "New message from Alex in #engineering", None),
    ("Teams", "Standup starts in 10 minutes", None),
    ("Calendar", "Meeting: sprint planning at {time}", None),
    ("Calendar", "Reminder: 1:1 with your manager at {time}", None),
    ("GitHub", "PR #1234 review requested: fix login flow", None),
    ("GitHub", "CI failed on main: tests broken", None),
    ("Jira", "ALAK-99 assigned to you: investigate crash", None),
    ("Mail", "Invoice approval needed before Friday", None),
    ("Docs", "Priya shared a document with you", None),
    ("Slack", "Deploy finished: staging is live", None),
    ("Notion", "Comment from Sam on Q3 roadmap", None),
]
# --- people ---------------------------------------------------------------
PEOPLE = [
    ("Messages", "Mom: see you at dinner tonight", None),
    ("WhatsApp", "Dad: call me when you land", None),
    ("Messages", "New message from Emma: are we still on for Saturday?", None),
    ("Messenger", "Liam sent you a photo", None),
    ("Telegram", "Nora: I'll be 5 minutes late", None),
    ("Messages", "Missed call from Dad", None),
    ("Phone", "Missed call from +1 555 010 2233", None),
    ("Voicemail", "New voicemail from Mom (0:42)", None),
    ("Messages", "Emma liked your message", None),
    ("WhatsApp", "Voice message from Ahmed", None),
]
# --- money ----------------------------------------------------------------
MONEY = [
    ("Bank", "${amt2} charged to card ending {last4} at {place}", None),
    ("Bank", "Your balance is ${amt}.", None),
    ("Wallet", "You sent ${amt} to Jordan", None),
    ("Wallet", "Payment received: ${amt} from Riley", None),
    ("Bank", "Direct deposit of ${amt2} posted to your account", None),
    ("Card", "Transaction alert: ${amt2} at {place}", None),
    ("Bank", "Your statement is ready to view", None),
    ("Budget", "You spent ${amt} on groceries this week", None),
    ("Bank", "Refund of ${amt} processed to your account", None),
    ("Pay", "You paid ${amt} to City Utilities", None),
]
# --- orders ---------------------------------------------------------------
ORDERS = [
    ("Shop", "Order {ref} has shipped — arriving tomorrow", None),
    ("Shop", "Your order {ref} is out for delivery", None),
    ("Shop", "Order {ref} delivered. Rate your experience?", None),
    ("Food", "Your food is on the way — arriving in 20 minutes", None),
    ("Food", "Courier assigned to order {ref}", None),
    ("Ride", "Your driver is arriving", None),
    ("Ride", "You're 3 minutes away from your destination", None),
    ("Shop", "Order {ref} confirmed — we'll email tracking soon", None),
    ("Parcel", "Package {ref} is at your front door", None),
    ("Shop", "Return for order {ref} was received", None),
]
# --- security -------------------------------------------------------------
SECURITY = [
    ("Bank", "Your verification code is {digits6}. Don't share it.", None),
    ("Auth", "Your login code is {digits6}", None),
    ("Bank", "Passcode: {digits6} — valid for 5 minutes", None),
    ("Auth", "{digits6} is your one-time code", None),
    ("Bank", "Confirm your number with code {digits6}", None),
    ("Security", "New device signed in to your account", None),
    ("Auth", "Two-factor code: {digits6}", None),
    ("Bank", "Heads up: password changed on your account", None),
]
# --- social ---------------------------------------------------------------
SOCIAL = [
    ("Insta", "alex_dev liked your photo", None),
    ("Tok", "New follower: sam_codes", None),
    ("Insta", "You have 3 new follower requests", None),
    ("X", "Your post got 12 new likes", None),
    ("Tok", "kate posted a new video: morning routine", None),
    ("Insta", "Story mention from leo", None),
    ("X", "Trending in your area: city marathon", None),
    ("Tok", "Your video reached 1,000 views", None),
]
# --- updates --------------------------------------------------------------
UPDATES = [
    ("OS", "System update available: security patch", None),
    ("OS", "Storage almost full: 500 MB remaining", None),
    ("Battery", "Battery at 15% — consider charging", None)
    ,
    ("Weather", "Rain expected around 6 PM today", None),
    ("Play", "3 apps updated in the background", None),
    ("OS", "Screenshot saved", None),
    ("Backup", "Last backup: yesterday 11:42 PM", None),
]
# --- other / hard negatives ----------------------------------------------
OTHER = [
    ("Misc", "Your zip code 90210 was saved to your profile", None),
    ("Misc", "Backup completed successfully", None),
    ("Random", "You have a new memory in your gallery", None),
    ("Misc", "Terms of service updated — tap to review", None),
    ("Care", "How are we doing? Rate your last order", None),
    ("Care", "Account tip: set a profile photo", None),
    ("Care", "Complete your profile to get started", None),
    ("Misc", "Weekly digest: 5 tips for better sleep", None),
    ("Care", "Thanks for signing up! Complete your profile", None),
]

PCT = [5, 10, 15, 20, 25, 30, 40, 50, 60, 70]

# realistic channel names per package family (Android apps label their channels)
CH_BY_PKG = {
    "com.shop": "Order status", "com.msg": "Messages", "com.bank": "Finance",
    "com.auth": "Verification", "com.work": "Meetings", "com.soc": "Community",
    "com.os": "System", "com.misc": "Miscellaneous", "com.fashion": "Marketing",
}
def chan_for(pkg, alt_promo=False):
    fam = ".".join(pkg.split(".")[:2])
    fam = fam.rstrip("0123456789")  # strip the per-instance digit (com.shop0 → com.shop)
    ch = CH_BY_PKG.get(fam)
    if alt_promo and fam == "com.shop":
        return "Marketing"
    return ch
AMTS = ["9.99", "12.50", "24.00", "45.00", "60.75", "89.99", "120.00", "250.00"]
PLACES = ["STARBUCKS", "AMAZON MKTPL", "CITY UTILITIES", "UBER TRIP", "NETFLIX"]
TIMES = ["9:00", "10:30", "14:00", "15:45", "16:20", "11:15"]

def gen_cases():
    cases = []
    i = 0

    # offers: 60
    for k in range(60):
        t, fmt, _ = OFFERS[k % len(OFFERS)]
        pct = PCT[(k // len(OFFERS)) % len(PCT)]
        code = CODES[k % len(CODES)]
        cta = ["Shop now", "Big sale", "Don't miss out", "Flash deal"][k % 4]
        txt = fmt.format(pct=pct, code=code, cta=cta,
                         amt=AMTS[k % len(AMTS)].split(".")[0])
        cases.append(_mk(i, "offers", f"com.shop{k % 7}", t, txt,
                         channel=chan_for(f"com.shop{k % 7}", alt_promo=True)))
        i += 1
    # work: 40
    for k in range(40):
        t, fmt, _ = WORK[k % len(WORK)]
        txt = fmt.format(time=TIMES[k % len(TIMES)])
        cases.append(_mk(i, "work", f"com.work{k % 6}", t, txt, channel=chan_for(f"com.work{k % 6}")))
        i += 1
    # people: 40
    for k in range(40):
        t, fmt, _ = PEOPLE[k % len(PEOPLE)]
        cases.append(_mk(i, "people", f"com.msg{k % 5}", t, fmt, channel=chan_for(f"com.msg{k % 5}")))
        i += 1
    # money: 40
    for k in range(40):
        t, fmt, _ = MONEY[k % len(MONEY)]
        txt = fmt.format(
            amt=AMTS[k % len(AMTS)],
            amt2=AMTS[(k + 3) % len(AMTS)],
            last4=f"{1000 + (k * 7) % 9000}",
            place=PLACES[k % len(PLACES)])
        c = _mk(i, "money", f"com.bank{k % 4}", t, txt, channel=chan_for(f"com.bank{k % 4}"))
        cases.append(c)
        i += 1
    # orders: 40
    for k in range(40):
        t, fmt, _ = ORDERS[k % len(ORDERS)]
        ref = f"{100000 + (k * 13) % 899999}"
        txt = fmt.format(ref=ref)
        cases.append(_mk(i, "orders", f"com.shop{k % 7}", t, txt, channel=chan_for(f"com.shop{k % 7}")))
        i += 1
    # security: 30
    for k in range(30):
        t, fmt, _ = SECURITY[k % len(SECURITY)]
        digits = f"{100000 + (k * 137) % 899999}"
        txt = fmt.format(digits6=digits)
        cases.append(_mk(i, "security", f"com.auth{k % 3}", t, txt, channel=chan_for(f"com.auth{k % 3}")))
        i += 1
    # social: 30
    for k in range(30):
        t, fmt, _ = SOCIAL[k % len(SOCIAL)]
        cases.append(_mk(i, "social", f"com.soc{k % 4}", t, fmt, channel=chan_for(f"com.soc{k % 4}")))
        i += 1
    # updates: 20
    for k in range(20):
        t, fmt, _ = UPDATES[k % len(UPDATES)]
        cases.append(_mk(i, "updates", f"com.os{k % 3}", t, fmt, channel=chan_for(f"com.os{k % 3}")))
        i += 1
    # other: 20
    for k in range(20):
        t, fmt, _ = OTHER[k % len(OTHER)]
        cases.append(_mk(i, "other", f"com.misc{k % 5}", t, fmt, channel=chan_for(f"com.misc{k % 5}")))
        i += 1

    # annotate offers cases with promo_code expectation using the code in the text
    for c in cases:
        if c["expect"]["category"] != "offers":
            continue
        m = re.search(r"code:? ([A-Z0-9]{4,})", c["text"] or "", re.IGNORECASE)
        if m:
            c["expect"].setdefault("entities", []).append(
                {"type": "promo_code", "code": m.group(1)})
    return cases

def main():
    # merge with existing hand-written cases (worked examples etc.)
    with open(OUT, encoding="utf-8") as f:
        existing = json.load(f)
    existing_ids = {c["id"] for c in existing}
    gen = [c for c in gen_cases() if c["id"] not in existing_ids]
    merged = existing + gen
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(merged, f, ensure_ascii=False, indent=1)
    print(f"existing {len(existing)} + generated {len(gen)} = {len(merged)} EN cases")

if __name__ == "__main__":
    main()
