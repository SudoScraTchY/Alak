# Alak — Implementation Progress Tracker

**Goal: implement the ENTIRE specification (docs/spec/SPEC.md) through v1.0 — all milestones M1–M11 (§16).**

Work milestone-by-milestone; do not start one before the previous exit criterion is met.
Each completed milestone gets a commit and updated rows below.

## Milestone status

| # | Milestone | Status | Exit criteria (summary) | Notes |
|---|---|---|---|---|
| M1 | Capture spike (Android) | ⚠️ blocked (needs device) | Test-app notification → raw row <1s; PF-01..06 verified on 2 real devices | Desktop-side domain model done (`Alak.Core`). All PF-01..06 rows TODO(verify) in docs/verification.md — requires an Android device session. |
| M2 | Storage and domain | ✅ done | 10k rows insert/paginate/bilingual-FTS/prune; SQLCipher verified | `Alak.Storage` + `Alak.Storage.Tests`; PF-10 verified (bundle_e_sqlcipher 2.1.11). Commit f55d90f/658205f. |
| M3 | Rule engine core + EN pack | ✅ done | §8.10 EN worked examples pass; QLT-003 EN corpus ≥300 cases; RUL-002 budget | `Alak.Text`, `Alak.Rules`, `alak-pack` CLI done. Corpus 330 cases. Macro-F1 1.000, promo P/R 1.000, OTP recall 1.000. Commit 19eb24f. |
| M4a | FA pack completion + FA corpus (part 1) | ✅ done | §8.10 FA worked examples pass; FA corpus ≥300 cases; QLT-003 FA gates (macro-F1≥0.90, promo P≥0.98 R≥0.92, OTP R≥0.995) | FA pack 73 concepts (audit + 4 new: promo.claim, promo.event, money.tax, work.review); channel.orders, channel.money, channel.work added; social.* expanded; security.2fa added; people.greeting fixed. FA corpus 328 cases (64 offers, 41 work, 40 people, 41 money, 40 orders, 31 security, 30 social, 20 updates, 21 other). All suites 670 tests green. EN macro-F1 1.000 (no regression), FA macro-F1 1.000. Commit 253de83. |
| M4b | Jalali calendar + RTL audit (part 2) | 🔜 next | PF-09 golden table 1400–1420; RTL normalization tests | DAT-002 Jalali↔Gregorian conversion (Borkowski/Khayyam 33-year cycle); NRM rows RTL handling; golden-table test in Alak.Text.Tests. |
| M4c | fa-latn (Finglish) extension pack (part 3) | ✅ done | §8.9.3 activation rule (Finglish-only term gating); ≥20 cases | packs/fa-latn: 10 concepts / 16 Finglish-only terms (all §8.9.3 seed terms), validated by alak-pack CLI. Zero term overlap with EN pack (structural gate: an extension hit IS a Finglish-only hit). Corpus 20 cases (9 offers w/ promo codes, 4 money, 3 orders + 1 honest Other, 4 EN-hard-negatives). Dedicated FaLatnActivationTests: EN-precision invariance (identical category/confidence/entities ±0.0001 with vs without pack), term-exclusivity proof, worked examples. CorpusEval supports extension packs (en loads first, fa-latn after). All suites green: 698 Rules + 17 Text tests. |
| M5 | App shell + design system (MAUI) | pending | 500-row inbox scroll at §14 budget; RTL snapshots | — |
| M6 | Onboarding, Sources, Rules, Settings | pending | User rule overrides Tier 1 on next match | — |
| M7 | Codes wallet + Tier 2 | pending | WAL corpus scenarios; ADP-004 F1 gain; backup/restore round-trip | `Alak.Adaptive` skeleton exists |
| M8 | AI runtime spike + decision | pending | ADR-0002 with measured numbers; real `IInferenceEngine` impl passing ENG-001..006 | `Alak.Inference.Abstractions` skeleton exists |
| M9 | Tier 3 SLM features | pending | Golden-prompt suite on-device; SLM-008 cancel; resource guard | — |
| M10 | Corpus completion + hardening | pending | QLT-003 macro-F1 on 600/600 held-out in CI; calibration `weights.json`; a11y pass; no network permission | — |
| M11 | Release candidate | pending | Signed build from GitHub/F-Droid/Bazaar packaging; §14 re-measured | — |

---

## Dated log

- **2026-09-27** — M4a complete (commit `253de83`): FA pack expanded to 73 concepts (added 4 missing from §8.9.2: promo.claim, promo.event, money.tax, work.review; added channel.orders, channel.money, channel.work; expanded social.reaction/social.follow/social.live, added social.feed; added security.2fa; fixed people.greeting terms). FA corpus grown to 328 cases via `gen_fa_corpus.py` (deterministic seed 20260927, 9-category balanced). All 670 tests pass (EN 330 + FA 328 + 12 FA worked examples). CorpusEval: EN macro-F1 1.000 (no regression), FA macro-F1 1.000. QLT-003 FA gates: macro-F1 1.000 ≥ 0.90 ✅, promo P 1.000 ≥ 0.98 ✅, promo R 1.000 ≥ 0.92 ✅, OTP recall 1.000 ≥ 0.995 ✅.

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


## 2026-10-16 — M8 UI redesign on the SPEC §12 design system (this session; commit `6b5619e`)
- Root-caused the "buggy / styles not applied" state: (1) `Label.LineHeight` is a MULTIPLIER in MAUI — styles carried CSS-style px values (18–36) → 18–36× spacing broke every layout; retuned to 1.0–1.3 for Vazirmatn metrics. (2) Stray top-level ShellContents + duplicate route registrations collapsed the TabBar to one tab. (3) ScanPage used non-existent ZXing 0.10.4 API (`IsDetectingState`, event is `BarcodesDetected`) → app exited on tab tap. (4) Dark mode was structural-broken (runtime resource rewrites can't affect StaticResource; promised Colors.Dark.xaml never existed).
- Redesign: spec §12.2 palette (category marks/tints incl. gold wallet) replaced four divergent color sources; AppThemeBinding in style setters + ThemeManager→UserAppTheme only; paper-card Inbox rows with category spines + tap-to-detail; Codes wallet rebuilt around the Stub component (BindableLayout sections, no nested CollectionView hacks); full-screen onboarding; الک typographic empty states; emoji removed; Vazirmatn (4 weights) + JetBrains Mono TTFs added and registered.
- Verified on Redmi Note 11 `5a8c869e` (arm64): Inbox/Codes/Rules/Settings/Onboarding, light+dark. Tests 893/893 green (Core 20, Rules 704, Text 137, Storage 10, Adaptive 1, InfAbs 21). Pushed `f6bf485..6b5619e`.
- PENDING: Scan-tab fix not re-verified on device (phone dropped off USB before the check).

## 2026-10-17 — handoff refresh (this session)
- Rewrote `docs/AGENT-HANDOFF.md` (current state, sandbox environment cheatsheet, debugging history items 11–15, remaining work, fresh delegation prompt for a new agent instance); refreshed milestone state in `docs/HANDOFF-STATUS.md` (M4–M8 status + new next actions).

## 2026-10-30 — app identity display + SMS source attribution (ADR-0003)
- CAP-013 realised end to end: the capture binder thread resolves label/icon through the posting app's package context (works around Android 11+ visibility, PF-05), ingestion caches it in `apps.label`/`apps.icon_png`, falls back to a humanized package name, and a one-time startup backfill fills older rows. Inbox rows and Detail now show app name + icon instead of the raw package/channel; the package remains visible on Detail.
- SMS attribution: new pure `SafeLinkParser` (http/https only, IDN/punycode, no IP/userinfo, registrable-domain heuristic, bounded 10 links/2 KB); new `app_domains` index (migration 9) learns domain→package from non-communication apps; SMS notifications are attributed when exactly one package has ≥2 sightings (`notifications.attributed_package`, display-only) and show a localized “via SMS” chip. Original package is never rewritten; messaging apps never teach the index; no network/DNS/WebView/intent calls (R2).
- Tests: Core 71 (parser/humanizer), Storage 39 (attribution + app metadata round-trip), Rules 1300, Text 148, Adaptive 8, Inference.Abstractions 28 — all green. Build 0 errors.
- ADR: `docs/adr/0003-app-source-display-and-sms-attribution.md` (ADR-0002 stays reserved for the M8 inference-engine spike).


## 2026-10-31 — M8/M9 closure evidence + M10 kickoff
- M8 gate measured on-device (Redmi Note 11, R1-class): llama.cpp/LLamaSharp 0.30 with Qwen2.5-0.5B-Instruct Q4_K_M — load 1,046 ms, TTFT 599 ms, 12.3 tok/s, PSS ~240 MB, thermal 0 after 5 runs, 0 crashes; SmolLM2-135M comparison (37.4 tok/s, but 70–80 % JSON validity). ADR-0002 committed with deviations (no custom C shim, no mid-range device, no LiteRT-LM, GBNF crashes natively in LLamaSharp 0.30, APK 88.7 MiB over the 60 MB ceiling).
- M9 exit gates verified on device: golden suite 10/10 explain + 2/2 digest valid (target ≥85 % without grammar), grounding 2/2 checked claims; cancellation 6.5 ms (<500 ms); oversized model refused (2 GB > 1536 MB cap). Implemented for this: managed GGUF probe (MOD-002), SHA-256 import (MOD-003), structured benchmark persisted to models.benchmark_json (MOD-006), SLM-020/021 resource gate, SLM-023 idle unload, SLM-024 performance profile, per-call executor isolation, JSON prefill + tolerant parser, `Explain_Failed` localization.
- M10 started: CI `corpus-gates` job (QLT-003 EN/FA/fa-latn, fails the build), accessibility checklist (`docs/accessibility-checklist.md`); privacy-guard job already existed.
