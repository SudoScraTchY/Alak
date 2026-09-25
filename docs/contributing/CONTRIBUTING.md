# Alak — contribution guide

Alak (الک) is an offline, on-device notification organizer for Android built with .NET MAUI.
This guide covers **two contribution paths**:

1. **Code contributions** (C# / .NET) — the usual PR workflow.
2. **Language pack contributions** (data only) — the lower-friction path most contributors take.

**Spec:** `docs/spec/SPEC.md` · **Verification log:** `docs/verification.md` · **ADRs:** `docs/adr/`

## Ground rules for every contribution (spec R1–R8)

- **No network.** The app MUST NOT declare `INTERNET` or `ACCESS_NETWORK_STATE`, and MUST NOT
  ship an HTTP client, analytics SDK, crash reporter, or ad SDK (R2). CI's `privacy-guard` job
  fails the build on any violation (OSS-004).
- **Data-only extensibility.** Nothing loaded from a pack may execute code (R3).
- **Every automatic decision explains itself.** Anything classified/extracted/summarized
  carries a human-readable reason (R4 / RUL-020).
- **Test first for rules.** A rule without positive AND negative cases is incomplete (R5).
- **No guessed identifiers.** Package ids, model repo names, SHA-256s, NuGet versions must
  come from a source actually opened (R1). If unverifiable, leave `TODO(verify)` + a failing/skipped test.
- **Time via `IClock` only.** Never `DateTime.Now` in logic (ARC-003).

## Path 1: Code contributions

1. Fork/branch from `main`.
2. `dotnet build Alak.slnx -c Debug` must succeed with **zero warnings** (warnings-as-errors, ARC-004).
3. `dotnet test Alak.slnx` must be green.
4. Reference the requirement ID (CAP-xxx, STO-xxx, NRM-xxx …) in commits, tests, and PR title.
5. New rules require a corpus case **before** the fix lands (QLT-005): a bug report becomes a
   corpus case first, then the fix.
6. Tests must run on a desktop CI runner without an emulator (ARC-001).

## Path 2: Language pack contributions ("add a language in an afternoon")

A pack is a **folder of JSON/YAML data files**. No C#, no scripts, nothing executable.

```
packs/<id>/
├─ pack.json        # manifest (id, name, direction, scripts, weekend, suffixSets)
├─ lexicon.json     # concept → terms (the main content)
├─ numbers.json     # number words, scales, currencies
├─ dates.json       # month names, weekdays, relative words
├─ patterns.json    # signal rules + pattern rules (linear-time regexes only)
├─ apps.json        # optional per-app hints
└─ tests/*.yaml     # REQUIRED: positive AND negative cases (R5)
```

Quick start:

1. Copy `packs/_template/` to `packs/<your-lang>/`.
2. Fill `pack.json` (keep `schemaVersion: 1`).
3. Fill `lexicon.json` with terms for the concepts in the spec §8.6 catalog
   (start with the high-value ones: `promo.code.trigger`, `promo.discount.word`,
   `otp.trigger`, `money.*`, `orders.status`).
4. Write tests for **every** term: each term needs a case that fails if the term is removed.
5. Validate + test locally:

   ```
   dotnet run --project src/Alak.Packs.Tool -- validate packs/<your-lang>
   dotnet run --project src/Alak.Packs.Tool -- test packs/<your-lang>
   ```

6. Coverage gates (v1.0, spec QLT-004): ≥ 40 terms for `promo.discount.word`, `promo.limited`,
   `ad.cta`, `otp.trigger`, `money.*`, `orders.status`; ≥ 15 for every other filled concept.
7. Open a PR; CI runs the pack tests and the coverage report.

**Pack safety rules (PCK-004):** regexes must compile with `RegexOptions.NonBacktracking`
(no lookarounds/backreferences), ≤ 2000 chars per regex, ≤ 5000 rules per pack, no file
references outside the pack folder. Packs never inject UI text — `explain.key` points at
app-owned localized templates.

## Commit style

Reference requirement IDs, e.g.:

```
feat(rules): implement Aho-Corasick concept matching (RUL-003)

- one automaton over all enabled packs, rebuilt atomically on pack change
- corpus case added for multi-pack overlap (QLT-005)
```
