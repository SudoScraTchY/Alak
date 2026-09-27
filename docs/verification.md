# Verification log

Per spec §0.1: every **VERIFY** / PF-xx item gets a row here when it is actually checked,
with date, method, and result. Until then it stays `TODO(verify)`.

| ID | Item | Status | Date | Method & Result |
|---|---|---|---|---|
| PF-01 | NotificationListenerService binding lifecycle | TODO(verify) | — | Needs M1 spike on a real device (no device attached yet at bootstrap) |
| PF-02 | Android 15+ OTP redaction behavior | TODO(verify) | — | Needs Android 15/16 device; design already assumes redaction (CAP-008) |
| PF-03 | Sideload "Restricted settings" (Android 13+) | TODO(verify) | — | Needs sideloaded APK on device |
| PF-04 | Channel metadata readable via listener | TODO(verify) | — | Needs device session |
| PF-05 | Package visibility for labels/icons (no QUERY_ALL_PACKAGES) | TODO(verify) | — | Needs Android 11+ device |
| PF-06 | OEM background-kill behavior (2 skins) | TODO(verify) | — | Needs two OEM-skinned devices |
| PF-07 | 16 KB page-size alignment of native lib | TODO(verify) | — | Needs NDK build (M8) |
| PF-08 | EXTRA_IS_SENSITIVE clipboard flag (API 33+) | TODO(verify) | — | Needs API 33+ device |
| PF-09 | PersianCalendar vs official Jalali table 1400–1420 | TODO(verify) | — | Golden-table test to be written in Alak.Text.Tests (DAT-002); do NOT trust .NET's PersianCalendar until the 1400–1420 sweep passes against a vendored trusted table |
| PF-10 | SQLCipher bundle works on Android + FTS5 included | **Verified (desktop)** | 2026-09-25 | `SQLitePCLRaw.bundle_e_sqlcipher` **2.1.11** + `Microsoft.Data.Sqlite.Core` **10.0.12** (verified on nuget.org flatcontainer API; the spec's `SQLitePCLRaw.bundle.sqlcipher` id **does not exist** — 404 — the maintained bundle id uses an underscore). Live xUnit test `Alak.Storage.Tests/Pf10SqlCipherFts5Tests` proves: encrypted DB created via `Password=`; FTS5 `unicode61 remove_diacritics 2` virtual table created **inside** the encrypted DB; Persian + English MATCH queries return correct rows; opening without the key throws `SqliteException`; file header is not plaintext SQLite. **Android device check still TODO(verify) at M1.** |
| PF-11 | MAUI cold service start cost | TODO(verify) | — | Needs device (M1) |
| PF-12 | Post-reboot backfill via GetActiveNotifications | TODO(verify) | — | Needs device (M1) |
| QLT-003-FA | FA corpus gates: macro-F1 ≥ 0.90, promo P ≥ 0.98, promo R ≥ 0.92, OTP R ≥ 0.995 | **Verified (desktop)** | 2026-09-27 | `dotnet run --project tools/Alak.CorpusEval -c Release -- tests/corpus/fa/cases.json packs/fa/lexicon.json fa` → macro-F1 1.000, promo P 1.000, promo R 1.000, OTP recall 1.000. All gates pass. FA corpus: 328 cases (64 offers, 41 work, 40 people, 41 money, 40 orders, 31 security, 30 social, 20 updates, 21 other). EN regression test: macro-F1 1.000 (no regression). All 670 tests green (EN 330 + FA 328 + 12 FA worked examples). Commit 253de83. |
| D-01 | Name availability (Play/F-Droid/GitHub/domain) | TODO(verify) | — | Before public release |
| D-05 | Store targetSdk requirement at release | TODO(verify) | — | At release time |
| D-06 | Store policies on notification-access apps | TODO(verify) | — | Before store submission |
| RUL-002 | Tier-1 perf: ≤20ms p95 / ≤40ms p99 per notification | **Verified (desktop)** | 2026-09-26 | `Alak.Rules.Tests/PerformanceTests` x3 in Release on this host — all within budget |
| RUL-003 | One AC automaton over all packs, word-boundary hits | **Verified (desktop)** | 2026-09-26 | `ConceptMatcher.Scan` now enforces word boundaries (fixes 'address'→ad, 'saved'→save false hits); 30/30 corpus incl. EN+FA worked examples §8.10 |
| §8.10 | All 16 worked examples pass as corpus cases | **Verified (desktop)** | 2026-09-26 | EN cases en-works-001..007 + negatives; FA cases fa-works-008..014 + negatives; `CorpusTests` 30/30 green (xUnit, Release) |
| QLT-003 (EN) | macro-F1 ≥ 0.92, promo P≥0.98/R≥0.92, OTP R≥0.995 | **Verified (desktop)** | 2026-09-26 | `tools/Alak.CorpusEval` on 330-case EN corpus (10 hand-written + 320 synthetic per QLT-002): macro-F1 **1.000**, promo-code P=1.000 R=1.000 (62 codes), OTP recall 1.000. All gates PASS. Note: corpus is templated-synthetic; QLT-001 adversarial/negative growth is M10 work. |
| RUL-002 | Tier-1 perf ≤20ms p95/≤40ms p99 | **Verified (desktop)** | 2026-09-26 | PerfTests x3 in Release |
| Toolchain | .NET 10.0.301 SDK + android workload 36.1.43 + JDK 21 | Verified | 2026-09-25 | `dotnet --list-sdks`, `dotnet workload list`, `java -version` on this host |
| NuGet pins | MAUI 10.0.110 line, CT.Mvvm 8.4.2, CT.Maui 15.0.1, MDS 10.0.12, xUnit 2.9.3, Shouldly 4.3.0 | Verified | 2026-09-25 | nuget.org flatcontainer index endpoints (live query, not memory) |
| Licenses | GPL-3.0 text + CC0 legal code fetched from gnu.org / creativecommons.org | Verified | 2026-09-25 | curl fetch, byte counts 35149 / 7048 |
