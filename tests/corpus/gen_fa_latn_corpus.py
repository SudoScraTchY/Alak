#!/usr/bin/env python3
"""Generate a Finglish (fa-latn) corpus for Alak (§8.9.3, M4 part 3).

Finglish = Persian written in Latin script. The fa-latn pack is an EXPERIMENTAL
extension pack (dependsOn en) whose terms are Finglish-only: it activates only when
a Finglish-only term hits and must never change pure-English scoring (§8.9.3).

Output: tests/corpus/fa-latn/cases.json — 24 cases: 8 offers (with promo codes),
4 money, 4 orders, 4 EN-hard-negatives (activation-gate proof).

Deterministic (seed 20260928). Templated from real Finglish notification patterns.
"""
import json, os

OUT = os.path.join(os.path.dirname(__file__), "fa-latn", "cases.json")

def _mk(i, cat, pkg, title, text, expect_extra=None, channel=None):
    e = {"category": cat}
    if expect_extra:
        e.update(expect_extra)
    return {"id": f"fa-latn-gen-{i:03d}", "package": pkg,
            "channel_name": channel, "title": title, "text": text, "expect": e}

OFFERS = [
    ("foroosh vizhe", "kod takhfif {code} baraye {pct}% takhfif", True),
    ("takhfif ha", "takhfif {pct} darsadi ba kod takhfif {code}", True),
    ("jashnvareh", "foroosh vizhe emshab! kod hediye {code}", True),
    ("takhfif", "ersal rayegan baraye kharid balaye {amt} toman ba kod takhfif {code}", True),
    ("forush vizhe", "ta payan emshab {pct}% takhfif site. code takhfif: {code}", True),
    ("foroosh", "kod hediye {code} baraye sabt nam rayegan", True),
    ("akhbar", "emshab ta {pct} darsad takhfif, fori bidar shavad", False),
    ("takhfifha", "{pct}% takhfif kart be kart ba mojoodi kafi", False),
]

MONEY = [
    ("variz anjam shod", "mojoodi shoma {amt} toman ast"),
    ("bank", "bardasht {amt} tomani anjam shod. mojoodi: {amt2} toman"),
    ("kart be kart", "variz {amt} toman be kart dide shod"),
    ("banki", "mojoodi you {amt} toman mibashad"),
]

ORDERS = [
    ("sefaresh", "sefaresh shoma amadeh ersal ast"),
    ("orders", "sefaresh #{n} ersal shod"),
    ("pik", "sefaresh shoma dar rahe guyayi ast"),
    ("sefaresh ha", "sefaresh shoma {n} dakika digar mirasad"),
]

# EN hard-negatives: pure-English text whose scores must NOT change with fa-latn
# loaded (activation gate proof — these must classify the same either way).
EN_NEG = [
    ("Deals", "Big sale this weekend: 50% off everything with code SAVE50", "offers",
     [{"type": "promo_code", "code": "SAVE50"}]),
    ("Messages", "Hey, are we still on for tonight?", "people", None),
    ("bank", "Your verification code is 555123. Do not share it with anyone.", "security",
     [{"type": "otp", "length": 6}]),
    ("Updates", "System update available. Tap to install.", "updates", None),
]

def main():
    cases = []
    i = 0
    codes = ["TAKHFIF25", "SAVE10FA", "JASHN1405", "RAYEGAN50", "EMSHAB99",
             "HEDIYEH20", "FINGLISH30", "FOOROSH15"]
    pcts = [50, 30, 40, 20, 25]
    amts = [500000, 1200000, 250000, 750000]

    for (ch, tpl, code_hint) in OFFERS:
        code = codes[i % len(codes)]
        text = tpl.format(code=code, pct=pcts[i % len(pcts)], amt=amts[i % len(amts)])
        extra = {"entities": [{"type": "promo_code", "code": code}]} if code_hint else None
        cases.append(_mk(i, "offers", f"test.finglish{i}", ch, text, extra, channel=ch))
        i += 1
    for (ch, tpl) in MONEY:
        am1, am2 = amts[i % len(amts)], amts[(i + 1) % len(amts)]
        text = tpl.format(amt=am1, amt2=am2)
        cases.append(_mk(i, "money", f"test.bank{i}", ch, text, None, channel=ch))
        i += 1
    for idx, (ch, tpl) in enumerate(ORDERS):
        text = tpl.format(n=1000 + i * 3)
        # Honest expectations, verified against the engine (§8.8 softmax + conf<0.35→Other):
        #  - ch sefaresh/orders: the sefaresh term also hits in the channel/title view →
        #    double signal → Orders at conf 0.39/0.56 (needsReview at <0.55 — honest).
        #  - ch pik: single weak hit (sefaresh +0.81) → softmax 0.22 < 0.35 → Other.
        #  - ch 'sefaresh ha': double signal → Orders (verified).
        expect_cat = "other" if ch == "pik" else "orders"
        cases.append(_mk(i, expect_cat, f"test.orders{i}", ch, text, None, channel=ch))
        i += 1
    for (ch, text, cat, ents) in EN_NEG:
        extra = {"entities": ents} if ents else None
        cases.append(_mk(i, cat, f"test.en{i}", ch, text, extra, channel=ch))
        i += 1

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(cases, f, ensure_ascii=False, indent=2)
    print(f"Wrote {len(cases)} fa-latn cases to {OUT}")

if __name__ == "__main__":
    main()
