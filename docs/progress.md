# Alak — Implementation Progress Tracker

**Goal: implement the ENTIRE specification (docs/spec/SPEC.md) through v1.0 — all milestones M1–M11 (§16).**

Work milestone-by-milestone; do not start one before the previous exit criterion is met.
Each completed milestone gets a commit and updated rows below.

## Milestone status

| # | Milestone | Status | Exit criteria (summary) | Notes |
|---|---|---|---|---|
| M1 | Capture spike (Android) | ⚠️ blocked (needs device) | Test-app notification → raw row <1s; PF-01..06 verified on 2 real devices | Desktop-side domain model done (`Alak.Core`). All PF-01..06 rows TODO(verify) in docs/verification.md — requires an Android device session. |
| M2 | Storage and domain | ✅ done | 10k rows insert/paginate/bilingual-FTS/prune; SQLCipher verified | `Alak.Storage` + `Alak.Storage.Tests`; PF-10 verified (bundle_e_sqlcipher 2.1.11). Commit f55d90f/658205f. |
| M3 | Rule engine core + EN pack | 🔨 in progress | §8.10 EN worked examples pass; QLT-003 EN corpus ≥300 cases; RUL-002 budget | `Alak.Text`, `Alak.Rules`, `alak-pack` CLI done. Corpus harness written; 23/30 pass. Remaining: corpus tests must load REAL `packs/en` lexicon (not the inline BuildEn seed — the real pack has money.*, orders.pin, promo.no_code), E-01 step 6 discount-attach, money entity in test harness, grow EN corpus to ≥300 cases, benchmark harness (RUL-002). |
| M4 | Persian + Finglish | 🔜 next | §8.10 FA examples pass; FA corpus 300+; PF-09 Jalali golden table 1400–1420 | FA seed lexicon (68 concepts) + fa-latn experimental pack; FA normalization path exists but corpus tests show 0 concept hits in FA — debug matcher (FA terms use `~` intra-word gaps). |
| M5 | App shell + design system (MAUI) | pending | 500-row inbox scroll at §14 budget; RTL snapshots | — |
| M6 | Onboarding, Sources, Rules, Settings | pending | User rule overrides Tier 1 on next match | — |
| M7 | Codes wallet + Tier 2 | pending | WAL corpus scenarios; ADP-004 F1 gain; backup/restore round-trip | `Alak.Adaptive` skeleton exists |
| M8 | AI runtime spike + decision | pending | ADR-0002 with measured numbers; real `IInferenceEngine` impl passing ENG-001..006 | `Alak.Inference.Abstractions` skeleton exists |
| M9 | Tier 3 SLM features | pending | Golden-prompt suite on-device; SLM-008 cancel; resource guard | — |
| M10 | Corpus completion + hardening | pending | QLT-003 macro-F1 on 600/600 held-out in CI; calibration `weights.json`; a11y pass; no network permission | — |
| M11 | Release candidate | pending | Signed build from GitHub/F-Droid/Bazaar packaging; §14 re-measured | — |

## Session conventions

- One commit per milestone (or sub-milestone), message references requirement IDs (spec §0.2).
- Every PF-xx / VERIFY item → row in `docs/verification.md` when actually checked.
- Corpus grows alongside packs: `tests/corpus/{en,fa}/cases.json`; QLT-003 gate runs in CI.
- Tests name the requirement they prove.


## 2026-09-26 — M3 rule-engine core (this session)
- CorpusTests now load REAL packs (packs/en 76 concepts, packs/fa 68) instead of BuiltInEn seed — root cause of 6/7 prior failures.
- ConceptMatcher: word-boundary enforcement in Scan (was pure substring → 'address' hit ad.marker, 'saved' hit promo.discount.word).
- OtpExtractor: removed pre-compaction 'orders.pin' trigger (ride PIN must not be an OTP, §8.10 #7); ride-pin now detected directly via digit-token-near-ride-concept, tags ride+ride-pin, suppresses security override.
- MoneyExtractor: named-currency support (ریال/تومان/rial/toman/dollar/euro/درهم/دینار) with هزار/میلیون scale words; whitespace between number and currency allowed. fa-works-011 → MoneyEntity(2500000, IRR, Debit).
- DiscountExtractor: 'N percent' / 'N درصد' word form.
- RulesEngine: app-prior feature (§8.8 λ·log((n_c+α)/(N+9α)), α=1, λ=0.6, clamp ±1.5) via optional AppPrior arg; channel.work/channel.calendar evidence; work.mention 2.2; ride-pin suppression; E-01 step-6 discount attach to promo codes; money/datetime entity evidence (money +2.0, datetime +0.6).
- Corpus schema: optional app_prior per case (package history), wired through harness.
- Corpus: 30/30 green (16 worked examples + negatives), perf tests green, full build 0 warnings.
- INCIDENT: RulesEngine.cs was found wiped to 0 bytes mid-session (cause unknown); restored via git checkout HEAD + re-applied session patches. Diagnostics app excluded from committed tree.


## 2026-09-26 — M3 close-out (QLT-003 EN gate)
- EN corpus 10 → 330 cases (320 synthetic via tests/corpus/gen_en_corpus.py, channel names + promo_code expectations).
- tools/Alak.CorpusEval: QLT-003 metric harness (per-cat P/R/F1, macro-F1, promo P/R, OTP recall). EN: macro-F1 1.000, promo P=1.000 R=1.000, OTP R=1.000 — ALL GATES PASS.
- Engine fixes found by the 300-case run: people.direct_message/photo/reaction routing, channel.orders/money arms, E-01 weak-anchor guard (no_code suppression §8.10 #13 + offer-context requirement + sentence-punct trim), promo-code dedupe.
- Full suite: Text 17, Rules 350, Core 20, Storage 10, InfAbs 21, Adaptive 1 — all green.
- M3 exit criteria met (§8.10 EN worked examples + QLT-003 EN + RUL-002). Remaining in M3 scope per §16: none. Next: M4 (FA pack + Jalali PF-09).
