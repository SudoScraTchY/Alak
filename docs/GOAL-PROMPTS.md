# Alak — Hermes Goal Prompts (subagent delegation via omniroute)

**How to use this file:** Each section below is a self-contained "goal prompt" for a Hermes delegation subagent (`delegate_task` with `goal` + `context`). Subagents run on omniroute models. Allowed models for Alak subagents (user-set, 2026-09-27 — do not substitute others):

```
Gemini/gemini-3.6-flash
Gemini/gemini-3.7-flash
Gemini/gemini-3.8-flash
Agnes/agnes-2.5-flash
Sealion/aisingapore/Llama-SEA-LION-v3-70B-IT
Llm7/GLM-5.3-Flash
Llm7/codestral-latest
Llm7/minimax-m2.7
Llm7/mistral-Nemo-Instruct-2407
```

Model assignment heuristics: code-heavy implementation → `Llm7/GLM-5.3-Flash` or `Llm7/codestral-latest`; multilingual (Persian/Finglish) content → `Gemini/gemini-3.8-flash` or `Sealion/aisingapore/Llama-SEA-LION-v3-70B-IT`; long-context spec reasoning / report drafting → `Llm7/minimax-m2.7`; fast batch/labeling chores → `Llm7/mistral-Nemo-Instruct-2407` or a Gemini flash.

Every subagent MUST be given, in `context`: the repo root (`M:/Hermes/Alak`), the spec path (`docs/spec/SPEC.md`), this report's companion status file (`docs/HANDOFF-STATUS.md`), the goal prompt text, and the model list above. Subagents must run `dotnet test` on the affected suite before reporting done, and must append progress to `docs/progress.md` on success.

---

## GOAL A — M4: Persian pack completion + FA corpus (next up)

```
You are a subagent working on the Alak repository at M:/Hermes/Alak (Windows, .NET 10, git-bash). Read docs/spec/SPEC.md (authoritative) and docs/HANDOFF-STATUS.md (current state; M1–M3 done, M3 closed at commit 19eb24f).

TASK — Milestone M4, part 1 (spec §16 M4 row, §8.9.2, QLT-002):
1. Complete packs/fa/lexicon.json against spec §8.9.2's 53-row seed table. Currently 69 concepts; known missing: promo.claim, promo.event, money.tax, work.review. Audit EVERY row term-by-term and add every seed term (keep '~' as optional-separator marker exactly as the pack format/§8.9.2 defines).
2. Write tests/corpus/gen_fa_corpus.py, modeled on tests/corpus/gen_en_corpus.py (read it first): deterministic seed, category-balanced synthetic cases across the 9 categories (offers, work, people, money, orders, security, social, updates, other), realistic channel names, promo_code entity expectations, zero PII (QLT-002 scrub rules). Merge, never delete, the 8 existing hand-written fa cases in tests/corpus/fa/cases.json.
3. Grow the FA corpus to ≥300 cases total.
4. Run: dotnet test tests/Alak.Rules.Tests/Alak.Rules.Tests.csproj -c Release  — all corpus cases (EN 330 + FA ≥300) must pass. Iterate pack/engine until green: fix classification misses by adding missing concept terms or routing (RulesEngine.cs concept→category switch), NOT by weakening expectations.
5. Run dotnet run --project tools/Alak.CorpusEval -c Release -- tests/corpus/fa/cases.json packs/fa/lexicon.json fa and report per-category P/R/F1 + macro-F1. FA gates (QLT-003 FA): macro-F1 ≥0.90, promo-code P≥0.98 R≥0.92, OTP recall ≥0.995.
6. Commit (git add -A; git commit -m "feat(fa): M4 part 1 — FA pack completion + FA corpus NNN cases, QLT-003 FA gates") and append a dated entry to docs/progress.md.

Do not modify docs/spec/SPEC.md. Do not weaken test expectations to make gates pass. Report: final metrics table, commit hash, files changed.
```

## GOAL B — M4: Jalali calendar + RTL audit (parallelizable with GOAL A after pack is merged)

```
You are a subagent working on the Alak repository at M:/Hermes/Alak (.NET 10, git-bash on Windows). Read docs/spec/SPEC.md sections DAT-002, DAT-003, DAT-004, PF-09, §8.4, and docs/HANDOFF-STATUS.md.

TASK — Milestone M4, part 2:
1. Implement Jalali↔Gregorian conversion in src/Alak.Text (new file, e.g. JalaliCalendar.cs). Spec DAT-002: correct for Jalali years 1400–1420; do NOT trust System.Globalization.PersianCalendar blindly (PF-09 explicitly warns it may disagree on leap years). Vendor a trusted algorithm (e.g. the Borkowski/Khayyam 33-year cycle algorithm used by jalaali-js / moment-jalaali) and a golden-table test.
2. Create the PF-09 golden table test in tests/Alak.Text.Tests: known Jalali↔Gregorian pairs covering all of 1400–1420, including leap-year edges (1403, 1408, ... — verify against your table, e.g. 1403/01/01 = 2024/03/20), 33-year cycle boundaries, and Nowruz transitions. Cite the table source in a comment.
3. RTL audit (§8.2 NRM rows): verify Normalization strips/handles RLM/LRM/ALM isolates, Arabic presentation forms (U+FB50–U+FDFF, U+FE70–U+FEFF), Persian digits ۰-۹ vs Arabic-Indic ٠-٩, and ZWNJ (نیم‌فاصله) in the folded text. Add tests for each. Extend only where the spec requires; do not invent behavior.
4. dotnet test tests/Alak.Text.Tests/Alak.Text.Tests.csproj -c Release must be green; also run the Rules suite to confirm no regression.
5. Commit ("feat(text): M4 part 2 — Jalali conversion + PF-09 golden table + RTL normalization tests") and append to docs/progress.md. Add a verification.md line for PF-09: Verified (golden table 1400–1420) with date and evidence.

Do not modify docs/spec/SPEC.md. Report: test names added, golden-table source citation, commit hash.
```

## GOAL C — M4: fa-latn (Finglish) extension pack

```
You are a subagent working on the Alak repository at M:/Hermes/Alak (.NET 10, git-bash). Read docs/spec/SPEC.md §8.9.3 and docs/HANDOFF-STATUS.md.

TASK — Milestone M4, part 3:
1. Create packs/fa-latn/lexicon.json from the §8.9.3 seed list (kod~takhfif, kod~hediye, takhfif, ersal~rayegan, rayegan, foroosh~vizhe, forush~vizhe, ta~payan~emshab, ta~emshab, fori, variz, bardasht, mojoodi, kart~be~kart, sefaresh, code~takhfif, ...). Mark the pack experimental in pack.json metadata if a field allows; it already declares dependsOn: ["en"], scripts: ["Latn"].
2. Implement the §8.9.3 activation rule: the fa-latn pack MUST NOT reduce English precision — it activates only when at least one Finglish-ONLY term hits (terms not present in the en pack, e.g. 'takhfif', not 'code'/'sale'). This likely belongs in PackSet / RulesEngine loading logic — read how packs/en and packs/fa are loaded and mirrored the gating. Write tests proving: (a) Finglish text like 'kod takhfif TAKHFIF25 ta payan emshab' classifies as offers with promo code; (b) pure-English text scores unchanged with vs without fa-latn loaded.
3. Grow tests/corpus/ with a small fa-latn case file (or extend cases.json with a 'fa-latn' tagged set — follow existing corpus conventions) — ≥20 cases.
4. dotnet test tests/Alak.Rules.Tests/Alak.Rules.Tests.csproj -c Release green; CorpusEval for fa-latn corpus if wired.
5. Commit ("feat(fa-latn): M4 part 3 — Finglish extension pack + activation gating") and append to docs/progress.md.

Do not modify docs/spec/SPEC.md. Report: activation rule mechanism, test names, commit hash.
```

## GOAL D — M5: MAUI app shell + design system (after M4 exit)

```
You are a subagent working on the Alak repository at M:/Hermes/Alak (.NET 10, git-bash, spec §12, §13.1–13.3, §16 M5 row; read docs/HANDOFF-STATUS.md first).

TASK — Milestone M5:
1. Create the MAUI app project (src/Alak.App or per solution conventions; check Alak.sln) with the shell: Inbox (§13.2) and Notification Detail (§13.3) screens wired to REAL data from Alak.Storage (repo pattern established in M2).
2. Theming per §12.2: light + dark themes via named resources; typography per §12.3 (pick fonts, VERIFY licenses, record in docs/verification.md).
3. RTL layout support: layouts must mirror correctly for fa (test with FlowDirection=RightToLeft on the Inbox).
4. The Stub component (§12.7): build the stub mechanism for placeholder screens exactly as spec'd.
5. Performance: cold-start and listener start measured per PF-11 guidance; log numbers in docs/verification.md (R1 = a low-end Android device — if no device is attached, record 'desktop-emulator only' honestly, do not fabricate).
6. dotnet build must succeed for the app head; unit tests green; commit ("feat(app): M5 — MAUI shell, theming, Inbox + Detail, RTL") and append docs/progress.md.

Do not modify docs/spec/SPEC.md. Report: screens built, theme/typography decisions + font licenses, commit hash.
```

## GOAL E — Housekeeping: adversarial/negative corpus growth (M10 prep, safe anytime after M4)

```
You are a subagent working on the Alak repository at M:/Hermes/Alak (.NET 10). Read docs/spec/SPEC.md QLT-001, QLT-002, §8.10 edge-case table, and docs/HANDOFF-STATUS.md.

TASK — adversarial corpus batch 1:
1. Extend tests/corpus/gen_en_corpus.py (or a new gen_en_adversarial.py) with the QLT-001 adversarial categories: obfuscation (spaced letters, zero-width chars), mixed scripts (Finglish/Latin in EN text), punctuation glue, RTL marks, emoji-only text, empty title, 5KB body, truncated text, InboxStyle multi-line (text_lines array), MessagingStyle threads.
2. Generate ≥60 adversarial EN cases with honest expectations (many will be low-confidence or category=other — that is fine; the corpus documents BEHAVIOR, not perfection. But a behavior expectation must match actual engine output only if it is spec-correct: where the engine mis-behaves on a spec-clarified case, FIX THE ENGINE, don't lower the bar).
3. Where the engine currently fails a spec-mandated behavior, fix the engine (RulesEngine/Extractors) with minimal, targeted changes and note each fix in docs/progress.md.
4. dotnet test tests/Alak.Rules.Tests -c Release green; run CorpusEval and report the (expected lower) metrics honestly.
5. Commit ("test(corpus): adversarial batch 1 — NN EN adversarial/negative cases (QLT-001)") and append docs/progress.md.

Do not modify docs/spec/SPEC.md. Report: case count by type, engine fixes made, metrics delta, commit hash.
```

---

### Dispatch notes for the orchestrating agent

- Run GOAL A first (pack + FA corpus unblocks B and C's test baselines). GOAL B and C can then run in parallel (different files: Jalali/Text vs fa-latn pack). GOAL E can run any time after A merges; GOAL D strictly after M4 exit criteria (FA ≥300 corpus + PF-09 golden table green + FA worked examples pass).
- Every goal prompt is self-contained: paste it verbatim as the subagent `goal`; put repo paths, spec path, and the allowed-model list in `context`. Pin the model by name from the allowed list above per the heuristics.
- Subagents cannot read this conversation — they see only goal + context. Include the HANDOFF-STATUS.md path in every context.
- Validate subagent claims before merging: re-run `dotnet test` and CorpusEval yourself; check `git log` for the promised commit.
