# Alak — Implementation Status Report

**Audience:** AI agent (read together with `docs/spec/SPEC.md`). Not prose for humans — this is a machine-oriented state dump of what exists, what is verified, and what is next.
**Repo:** `M:/Hermes/Alak` (Windows). .NET 10, C#, xUnit. Solution: `Alak.sln`.
**Report date:** 2026-10-17 (milestone section refreshed; M1–M3 details below are original). **HEAD:** `6b5619e` ("feat(m8): redesign UI on the SPEC §12 design system; fix rendering defects"), pushed to origin/main. Current delta + delegation prompt live in `docs/AGENT-HANDOFF.md`.

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

### 3.4 M4 (Persian + Finglish) — ✅ DONE, verified (2026-10; commits `253de83`, `00a4a62`, see `docs/progress.md`)
All five kickoff items completed: fa pack completed (§8.9.2 concepts), FA corpus grown past the ≥300 gate, Jalali↔Gregorian (DAT-002) with PF-09 golden table 1400–1420, RTL normalization audit, `fa-latn` lexicon + activation rule. FA QLT-003 gates met. Suite counts as of `6b5619e`: Rules.Tests 704, Text.Tests 137 (grew from 350/17 with the M4 corpus work).

### 3.5 M5–M11 — status as of 2026-10-17
- **M5 (MAUI shell + design system)** ✅ DONE — app shell per §12–§13, 5-tab nav, screens, DI wired.
- **M6 (Onboarding, Sources, Rules, Settings)** ✅ DONE — §13.1/§13.6/§13.7/§13.9 + UserRulesEngine §11.1–11.3 + GlobalSettings store.
- **M7 (Codes wallet §13.8 + Tier-2 adaptive §8.11)** ✅ mostly done — Backup/Restore §11.5 NOT started.
- **App bring-up + UI (working label “M8” before the spec name was reconciled)** ✅ DONE — boots/renders and verified on the physical device (Redmi Note 11 `5a8c869e`): Inbox/Codes/Digest/Settings/Onboarding, light+dark, capture service, detail routes, crash fixes; 1594 tests green at `a7a314f`.
- **M8 (AI runtime spike + decision)** ✅ **exit measured** (2026-10-31, Redmi Note 11 5a8c869e): ADR-0002 committed with load 1,046 ms, TTFT 599 ms, 12.3 tok/s, PSS ~240 MB, thermal 0/5 runs, 0 crashes on Qwen2.5-0.5B Q4; llama.cpp/LLamaSharp chosen. **Deviations to accept or fix:** no custom C shim (ENG-004/005), no llama.cpp commit pin (ENG-006), native libs lack 16 KB page alignment (PF-07), no mid-range device, LiteRT-LM not tested, APK 88.7 MiB over the 60 MB ceiling.
- **M9 (Tier 3)** 🟡 **exit gates verified** (2026-10-31): golden 12/12 valid JSON (10 explain + 2 digest; ≥85 % target without grammar), grounding 2/2 checked claims, cancellation 6.5 ms, oversized 2 GB model refused (SLM-020). Implemented GGUF probe, SHA import, structured benchmark (benchmark_json), SLM-021/023/024. **Remaining scope:** T3-04 "Fill in details" entry point, SLM-007 slm_results cache, MOD-007/008 catalog + validator, SLM-022 background pause.
- **M10 (Quality/hardening)** 🟡 started 2026-10-31: corpus-gates CI job added (QLT-003 EN/FA/fa-latn), privacy-guard job existed, accessibility checklist started (`docs/accessibility-checklist.md`); REUSE/SPDX metadata and the manual a11y fixes remain.
- **M11** 🔲 not started (release packaging, §14 re-measure, name/package ADR); APK size is a known blocker.

## 4. Conventions (binding on all agents working this repo)

1. **Spec is authoritative.** If code and spec conflict, fix code; if the spec itself is wrong/ambiguous, record an ADR (`docs/adr/`) rather than silently deviating. No network permission anywhere (R2).
2. **Test-first per worked example:** every §8.10 row becomes a corpus case in `tests/corpus/<lang>/cases.json` with full expectation (category, tags, entities).
3. **Commit per milestone** (or per coherent sub-deliverable) with a message naming the spec section. Progress log appended to `docs/progress.md`; verification entries (PF-0x, QLT gates) appended to `docs/verification.md`.
4. **Corpus generation:** `tests/corpus/<lang>/gen_<lang>_corpus.py`, deterministic seed, merges with hand-written cases (never deletes them). No PII; scrub per QLT-002.
5. **Build/test:** never leave the suite red; run the affected suite + corpus suite before committing. `tests/Diagnostics/Program.cs` is a scratch console (fine to overwrite).
6. **Do not touch `docs/spec/SPEC.md`** unless an ADR documents a spec change decision.
7. **Windows/git-bash:** pass native paths to native tools (`C:/...`), MSYS paths for shell builtins.
8. **Recover from incidents by checking git first:** a file was once found wiped mid-session (2026-09-26); `git status`/`git diff` before debugging mystery breakage.

## 5. Immediate next actions (M8 remainder — see `docs/AGENT-HANDOFF.md` §6 for the full list)

1. Verify the Scan-tab fix on the physical device (`BarcodesDetected`, committed `6b5619e`, unverified — phone was unplugged).
2. Walk pushed routes on device (`detail?id=`, `stub-detail?id=`, Sources) and fix what breaks.
3. Onboarding end-to-end on MIUI: notification-access grant (incl. the "Restricted setting" path §13.1) and OEM battery step.
4. NotificationListenerService platform implementation (capture side) — not started.
5. Inbox day-group headers (§13.2) — needs a grouped collection on `InboxViewModel`; agree scope first (logic integration deliberately deferred per user, 2026-10-15).

## 6. Known caveats (do not regress)

- EN corpus metrics (1.000) are on a synthetic-templated corpus; M10 adversarial growth will drop them — that is expected, gates re-verified then on the harder corpus.
- The xUnit corpus harness asserts exact `promo_code.code` values when expectation is present (see `CorpusTests.cs`); eval tool reports metrics the xUnit suite doesn't (macro-F1 etc.).
- `Alak.CorpusEval` currently loads packs with terms only (weight from concept `weight` field, default 0.9); per-term weight arrays in lexicon.json are supported by PackSet.Compile but the eval loader reads only strings — keep lexicon term format consistent (string terms).
- `Alak.Adaptive.Tests` has 1 test (Tier-2 skeleton); M7 expands this.

---

*Generated for AI-agent handoff. Verify claims against the repo before relying on them: `git log --oneline -5`, `dotnet test` (6 projects), `dotnet run --project tools/Alak.CorpusEval ...`.*
