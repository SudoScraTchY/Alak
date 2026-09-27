# Alak — Implementation Status Report

**Audience:** AI agent (read together with `docs/spec/SPEC.md`). Not prose for humans — this is a machine-oriented state dump of what exists, what is verified, and what is next.
**Repo:** `M:/Hermes/Alak` (Windows). .NET 10, C#, xUnit. Solution: `Alak.sln`.
**Report date:** 2026-09-27. **HEAD:** `19eb24f` ("feat(corpus): M3 close-out — EN corpus 330 cases, QLT-003 gates all PASS").

---

## 1. Objective

Implement the ENTIRE specification (`docs/spec/SPEC.md`) through v1.0: all milestones **M1–M11** (spec §16). Goal set 2026-09-25. Work proceeds strictly in milestone order; do not start a milestone before the previous exit criteria are met (§16 rule).

## 2. Solution layout (as built)

```
src/
  Alak.Text/            normalization (NRM-001..010), script/language detection (LNG-001..005)
  Alak.Rules/           RulesEngine.cs (log-odds scoring §8.8), Extraction/ (E-01..E-09),
                        Aho-Corasick word-boundary matcher, PackSet/LanguagePack
  Alak.Core/            RawNotification (Capture), Model (Category enum — 9 cats, Entity,
                        Tag, Evidence), Classification abstractions
  Alak.Storage/         SQLite/SQLCipher schema v1, migrations, FTS5, retention, keyset pagination
  Alak.Inference.Abstractions/  inference interface + Tier-2/3 contracts (21 tests)
tests/                  one test project per src project + tests/Diagnostics (scratch console)
  tests/corpus/en/cases.json    330 EN cases (10 hand + 320 synthetic, gen script committed)
  tests/corpus/fa/cases.json    8 FA cases (§8.10 FA worked examples only)
  tests/corpus/gen_en_corpus.py  generator (deterministic, seed 20260926)
tools/Alak.CorpusEval/  QLT-003 metric harness (macro-F1, promo P/R, OTP recall) — dotnet run
packs/en/              lexicon.json (86 concepts) + pack.json + tests
packs/fa/              lexicon.json (69 concepts) + pack.json + tests
packs/fa-latn/         pack.json ONLY — lexicon.json not started (M4, experimental)
packs/_template/       pack scaffold
docs/spec/SPEC.md      the authoritative spec (~1600 lines)
docs/adr/0001-*.md     recorded ADRs
docs/verification.md   PF-0x verification log (M1/M2 + QLT-003 EN entry)
docs/progress.md       per-session progress log
```

Build/test loop (Windows, git-bash): `dotnet test tests/Alak.Rules.Tests/Alak.Rules.Tests.csproj -c Release`; full loop is 6 projects (see §3.1). `dotnet run --project tools/Alak.CorpusEval -c Release -- tests/corpus/en/cases.json packs/en/lexicon.json en`.

## 3. Verified state — milestone by milestone

### 3.1 M1 (Capture spike) — ✅ DONE, verified
- `NotificationCaptureService` (§7) posts raw rows; PF-01/02/03/05/06/12 findings in `docs/verification.md`.

### 3.2 M2 (Storage & domain) — ✅ DONE, verified (commit `658205f`)
- Schema v1 (§6.6), migrations, retention, keyset pagination, FTS5 both languages, SQLCipher verified via `bundle_e_sqlcipher` 2.1.11 (PF-10). 10k-row test green.

### 3.3 M3 (Rule engine core + EN pack) — ✅ **CLOSED** (commits `d0bfe27`, `b9085cb`, `19eb24f`)
Exit criteria per §16 M3 row — all three met:
1. **§8.10 EN worked examples pass** — in `Alak.Rules.Tests` (worked-example tests).
2. **QLT-003 EN thresholds met on ≥300-case EN corpus** — VERIFIED with `tools/Alak.CorpusEval` on 330 cases:
   - category macro-F1 = **1.000** (gate ≥0.92)
   - promo-code precision **1.000** (gate ≥0.98), recall **1.000** (gate ≥0.92); 62 codes, 0 FP
   - OTP recall **1.000** (gate ≥0.995)
   - CAVEAT: corpus is templated-synthetic (QLT-002 allows this) — adversarial/hard-negative growth to 600/600 + ≥150 negatives + ≥100 adversarial is M10 scope (QLT-001).
3. **RUL-002 budget met on R1** — PerfTests x3, Release config.

All six suites green as of `19eb24f`:

| Suite | Tests | Status |
|---|---|---|
| Alak.Text.Tests | 17 | PASS |
| Alak.Rules.Tests | 350 (330 EN + 20 FA corpus) | PASS |
| Alak.Core.Tests | 20 | PASS |
| Alak.Storage.Tests | 10 | PASS |
| Alak.Inference.Abstractions.Tests | 21 | PASS |
| Alak.Adaptive.Tests | 1 | PASS |

Engine details worth knowing before touching it (learned via the 300-case run):
- Concept→category routing table in `RulesEngine.cs` (`var (cat, weight) = h.ConceptId switch`): people.* family incomplete in the switch — `people.direct_message`, `people.photo`, `people.reaction` were missing; now routed to People 0.9. Verify before assuming family completeness.
- Channel-name concepts (`channel.*`): the engine gives channel.promo/messages/security/updates/social/work/calendar/orders/money ±1.5–1.8. Spec §8.9.1 channel rows live in pack; channel.people concept was REMOVED (duplicated channel.messages terms 'messages','sms' — overlapped; keep it that way unless spec'd).
- E-01 promo-code extractor now enforces (§8.6 rules 3–5, §8.10 #13):
  - sentence punctuation trimmed from candidates (`! ? ;` added)
  - weak anchor requires offer context, and `promo.no_code` hit suppresses weak-anchor extraction entirely ("no code needed" ⇒ tag, no code)
  - dedupe: same token near two anchors yields once.
- `packs/en/lexicon.json` has 86 concepts incl. all §8.9.1 seed rows; `packs/fa/lexicon.json` has 69.
- FA pack is missing 4 spec §8.9.2 concepts: `promo.claim`, `promo.event`, `money.tax`, `work.review` (M4 work).
- `Alak.Text` RTL handling exists (Normalization/ScriptLanguage) but Jalali↔Gregorian conversion (DAT-002, PF-09 golden table 1400–1420) is NOT implemented.
- Corpus tests load REAL packs (`packs/en|fa/lexicon.json`), not inline BuildEn seed. Don't regress to BuildEn in tests.

### 3.4 M4 (Persian + Finglish) — ⏳ NEXT UP
Scope (§16): `fa` pack completion (§8.9.2), Jalali dates (DAT-002 + PF-09 golden table for 1400–1420), RTL in Alak.Text, `fa-latn` extension pack (§8.9.3, experimental, dependsOn en, activation rule: only when ≥1 Finglish-only term hits — MUST NOT reduce English precision).
Exit: §8.10 FA worked examples pass; **FA corpus ≥300 cases**; PF-09 golden-table test green for 1400–1420.
Gaps inventory (verified against code, 2026-09-27):
1. FA pack: 4 missing concepts (`promo.claim`, `promo.event`, `money.tax`, `work.review`) + term coverage of all 53 §8.9.2 rows needs an audit vs the 69 present concepts (some may be sparse).
2. FA corpus: 8 cases → need ≥300 (mirroring the EN approach: §8.10 examples + synthetic templated generator; write `tests/corpus/gen_fa_corpus.py` analogous to `gen_en_corpus.py`, same scrub rules QLT-002).
3. Jalali conversion: missing entirely (DAT-002, PF-09). Implement algorithm (do not trust `PersianCalendar` blindly — spec explicitly warns), golden-table test for Jalali years 1400–1420.
4. RTL: partially in Normalization (RTL marks, Arabic presentation forms) — audit DAT/RUL entries on RTL (e.g. RLM/LRM stripping NRM-009) and expand per spec §8.2/8.4.
5. fa-latn pack: pack.json exists; lexicon.json missing. Seeds from §8.9.3 list.
6. FA run of CorpusEval at the end: gates QLT-003 FA thresholds macro-F1 ≥0.90 (EN gate is 0.92; FA gate is 0.90), promo P≥0.98 R≥0.0.92, OTP R≥0.995.

### 3.5 M5–M11 — 🔲 not started
- M5: MAUI shell + design system (§12.2 theming, §12.3 typography/fonts licenses, §12.7 Stub, Inbox + Detail screens §13.2–13.3, RTL layouts).
- M6: Onboarding, Sources, Rules, Settings (§13.4–13.7).
- M7: Codes wallet (§13.8) + Tier-2 adaptive retraining (§8.11).
- M8: on-device AI runtime spike + ADR-0002 (§9, RUL-012+).
- M9: Tier-3 SLM (§9.4, RUL-013).
- M10: corpus 600/600 + ≥150 hard negatives + ≥100 adversarial (QLT-001), calibration tool (RUL-019, weights.json), a11y pass (§12.9), OSS hygiene (§15), privacy audit.
- M11: release packaging (GitHub/F-Droid/Cafe Bazaar/Myket, D-06), §14 re-measure, final ADR.

## 4. Conventions (binding on all agents working this repo)

1. **Spec is authoritative.** If code and spec conflict, fix code; if the spec itself is wrong/ambiguous, record an ADR (`docs/adr/`) rather than silently deviating. No network permission anywhere (R2).
2. **Test-first per worked example:** every §8.10 row becomes a corpus case in `tests/corpus/<lang>/cases.json` with full expectation (category, tags, entities).
3. **Commit per milestone** (or per coherent sub-deliverable) with a message naming the spec section. Progress log appended to `docs/progress.md`; verification entries (PF-0x, QLT gates) appended to `docs/verification.md`.
4. **Corpus generation:** `tests/corpus/<lang>/gen_<lang>_corpus.py`, deterministic seed, merges with hand-written cases (never deletes them). No PII; scrub per QLT-002.
5. **Build/test:** never leave the suite red; run the affected suite + corpus suite before committing. `tests/Diagnostics/Program.cs` is a scratch console (fine to overwrite).
6. **Do not touch `docs/spec/SPEC.md`** unless an ADR documents a spec change decision.
7. **Windows/git-bash:** pass native paths to native tools (`C:/...`), MSYS paths for shell builtins.
8. **Recover from incidents by checking git first:** a file was once found wiped mid-session (2026-09-26); `git status`/`git diff` before debugging mystery breakage.

## 5. Immediate next actions (M4 kickoff)

1. Add the 4 missing FA concepts + audit the 53-row §8.9.2 table term-by-term → `packs/fa/lexicon.json`.
2. Write `tests/corpus/gen_fa_corpus.py` → grow FA corpus 8 → ≥300 cases (mirroring EN balance: offers/work/people/money/orders/security/social/updates/other).
3. Implement Jalali↔Gregorian (DAT-002) in `Alak.Text` with PF-09 golden table 1400–1420 (find a trusted conversion table; vendor it as test data).
4. RTL audit of Normalization (NRM rows in §8.2) — RLM/LRM isolation marks, Arabic presentation forms, digit variants (۰-۹ vs ٠-٩).
5. fa-latn lexicon (§8.9.3 seed list) + activation rule test (Finglish-only term gating).
6. Run corpus eval for FA; target QLT-003 FA gates: macro-F1 ≥0.90, promo P≥0.98/R≥0.92, OTP R≥0.995.
7. Commit M4; append `docs/progress.md` + `docs/verification.md`.

## 6. Known caveats (do not regress)

- EN corpus metrics (1.000) are on a synthetic-templated corpus; M10 adversarial growth will drop them — that is expected, gates re-verified then on the harder corpus.
- The xUnit corpus harness asserts exact `promo_code.code` values when expectation is present (see `CorpusTests.cs`); eval tool reports metrics the xUnit suite doesn't (macro-F1 etc.).
- `Alak.CorpusEval` currently loads packs with terms only (weight from concept `weight` field, default 0.9); per-term weight arrays in lexicon.json are supported by PackSet.Compile but the eval loader reads only strings — keep lexicon term format consistent (string terms).
- `Alak.Adaptive.Tests` has 1 test (Tier-2 skeleton); M7 expands this.

---

*Generated for AI-agent handoff. Verify claims against the repo before relying on them: `git log --oneline -5`, `dotnet test` (6 projects), `dotnet run --project tools/Alak.CorpusEval ...`.*
