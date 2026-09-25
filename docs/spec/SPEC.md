# Alak — Product & Engineering Specification

> **Working title:** Alak (الک, Persian for "sieve"). Tagline: *Sift the noise.* The name is a codename; see D-01.
> **Status:** Draft v0.1 · **Date:** 2026-09-24
> **Audience:** AI coding agent(s) that will implement the app, plus human maintainers who review the work.
> **Language of this document:** English. The product itself ships in English and Persian (Farsi).

---

## 0. How to use this document (read first)

### 0.1 Reading order for an implementing agent

1. Read §1–§4 fully. They define what the product is, what it must never do, and which decisions are still open.
2. Read §5 (architecture) before writing any code.
3. Implement milestone by milestone following §16. Do not jump ahead. Each milestone has exit criteria that must pass before the next begins.
4. When a section says **VERIFY**, that is a platform or library fact that was true at the time of writing but must be re-checked against current official documentation or a spike on a real device *before* you build on it. Record the result in `docs/adr/` or `docs/verification.md`.

### 0.2 Conventions

- **MUST / MUST NOT / SHOULD / MAY** follow RFC 2119.
- Every requirement has an ID (`CAP-003`, `RUL-012`). Reference IDs in commits, tests, and pull requests. Tests should name the requirement they prove.
- **Acceptance** criteria are written so a test (automated where possible, manual checklist otherwise) can prove them.
- Anything marked **[DECISION NEEDED]** has a default. Proceed with the default, write an ADR stating that you did, and keep the decision easy to reverse.
- "Notification" always means an Android `StatusBarNotification` posted by *another* app. The app's own notifications (expiry reminders) are called "reminders".

### 0.3 Rules of engagement for the agent

| # | Rule |
|---|---|
| R1 | **Do not guess identifiers.** Android package names, Hugging Face repo names, model file names, SHA-256 hashes, NuGet package versions and license terms must come from a source you actually opened (store page, repository, official docs). If you cannot verify one, leave a `TODO(verify)` and a failing test or a skipped test with the reason. |
| R2 | **Never add a network capability.** The app MUST NOT declare `android.permission.INTERNET` or `ACCESS_NETWORK_STATE`, and MUST NOT ship an HTTP client, analytics SDK, crash reporter, or ad SDK. CI enforces this (OSS-004). |
| R3 | **Data-only extensibility.** Language packs and model catalogs are data. Nothing loaded from a pack may execute code. |
| R4 | **Explain every automatic decision.** Anything the app classifies, extracts, or summarizes must carry a human-readable reason that the UI can show (RUL-020). |
| R5 | **Test first for rules.** A rule without positive *and* negative test cases is incomplete. |
| R6 | **Small, reversible steps.** Prefer boring technology, small files, explicit interfaces, and ADRs over cleverness. |
| R7 | **Measure on a real low-end phone** for anything about performance, memory, battery or the AI runtime. Emulators are not evidence. |
| R8 | **Ask when the spec conflicts with reality.** If a VERIFY item fails, stop that branch of work, document what you found, propose the smallest spec change, and continue with independent work. |

---

## 1. Product overview

### 1.1 Problem

People receive hundreds of notifications a day: advertisements, promo codes, work email, chat, bank alerts, delivery updates, one-time passwords. Android's notification shade is a **volatile** store. The moment a notification is swiped away, cleared, or auto-dismissed, it is gone. That is especially painful for promo codes, which are sent as push notifications and deliberately create urgency ("ends tonight"). Users lose the code before they need it.

Android's built-in notification history is a flat, chronological list with no categories, no extraction, and no code handling.

### 1.2 Solution

An **offline, on-device notification organizer** for Android:

1. **Captures** every notification the user allows the app to read.
2. **Sorts** each one into a category (Offers, Work, People, Money, Orders, Security, Social, Updates, and a special Codes wallet) using a fast, explainable, multilingual rule engine.
3. **Extracts** structured facts: promo codes, discounts, amounts, dates, expiry, links.
4. **Learns** the user's corrections on-device (a tiny adaptive classifier).
5. **Optionally summarizes and explains** using a small language model (SLM) that the user installs and runs only when they tap a button.

It never talks to a network, has no account, no subscription and no cloud.

### 1.3 Product principles

1. **Private by construction.** No internet permission. Sensitive content is not stored by default. Privacy is verifiable, not promised.
2. **Fast and light first.** Rules and a tiny learned model handle 99% of the work in milliseconds. The SLM is an optional, user-triggered enhancement, never a dependency.
3. **Explainable.** Every category has a "why".
4. **Two scripts, one product.** English (LTR) and Persian (RTL) are first-class. More languages arrive through community language packs.
5. **Calm.** The product's job is to reduce noise. The UI is quiet, exact, and paper-like (see §12).
6. **Open.** Open-source code, open language packs, open model catalog. Contribution is designed in from day one.

### 1.4 Personas

| Persona | Description | Needs |
|---|---|---|
| **Deal-hunter (primary)** | Uses delivery, ride, and shopping apps; gets 5–30 promo pushes a day. Loses codes. | Saved codes with expiry, one-tap copy, "expiring soon". |
| **Overloaded professional** | 200+ notifications a day mixed across work email, chat, tickets, ads. | Work vs everything else, digest of what matters, search. |
| **Privacy-minded power user** | Distrusts apps that read notifications. Sideloads from F-Droid/GitHub. | Proof it is offline, control over what is captured, encrypted storage. |
| **Contributor** | Speaks a language the app lacks. | Simple data-only pack format, a validator CLI, clear docs. |

### 1.5 Core user stories

- **US-01** As a shopper, when a store sends me a promo code I want it saved automatically, so I can use it later even after I cleared my notifications.
- **US-02** As a shopper, I want to see which codes expire soon and copy one with a single tap.
- **US-03** As anyone, I want each notification sorted (ad, work, money, people…) so I can look only at what matters.
- **US-04** As a user, I want to correct a wrong category once and have the app get it right next time.
- **US-05** As a privacy-conscious user, I want to choose exactly which apps are captured and how much of each notification is kept.
- **US-06** As a user, I want to tap a button to get an AI summary of a notification or of my day, using a model I chose, without anything leaving my phone.
- **US-07** As a Persian speaker, I want Persian text, Persian digits, Jalali dates and right-to-left layout to work everywhere.
- **US-08** As a contributor, I want to add my language with a data file and a test file, without touching C#.

---

## 2. Scope

### 2.1 In scope (v1.0)

- Android only, built with **.NET MAUI** (UI shell) plus native Android platform code where the OS demands it.
- Notification capture via `NotificationListenerService`.
- Local encrypted storage (SQLite), full-text search, retention rules.
- Tiered classification pipeline: **Tier 1** rules, **Tier 2** adaptive local classifier, **Tier 3** user-triggered SLM.
- Language packs (English, Persian at launch; Persian-in-Latin-script "Finglish" as an extension pack).
- Codes wallet with expiry tracking and local reminders.
- Per-app capture policy, user rules, sensitive-content guard.
- Model manager (import, verify, select, test speed) and a pluggable inference runtime.
- Full light and dark themes, English and Persian UI, RTL support.
- Local backup/restore (encrypted file).

### 2.2 Non-goals (v1.0)

| Non-goal | Reason |
|---|---|
| **iOS** | iOS has no API for one app to read other apps' notifications. This is an OS sandbox rule, not a MAUI limitation. Keep the `Core` library platform-neutral, but do not build an iOS target. |
| Any cloud, sync, account, telemetry, or remote model download inside the app | Core promise (R2). |
| Dismissing, replying to, or acting on other apps' notifications | The app is read-only with respect to other apps. |
| Reading SMS directly | Different permission class and Play policy. SMS apps' *notifications* are captured like any other app's. |
| Generative "chat" with a model | The SLM performs bounded tasks (§9), not open chat. |
| Bundling or redistributing any model weights | Users bring their own model file (D-03). |

### 2.3 Hard constraints (from the maintainer)

- **.NET MAUI** for the app.
- **Android** first; iOS not possible for the core feature.
- **No subscription, no cloud, completely offline.**
- **Open-source.**
- SLM must come from a **ready-made model on Hugging Face** and be runnable on a **low-end modern smartphone**.
- The user **chooses which model to install**; the app should work with several.
- The SLM runs **only on a user action** (button), never in the background.
- Multilingual support: **English and Persian confirmed**; more via community packs, plugins, or app updates.
- The rule engine must be **very comprehensive**, to the point that it "feels like AI".
- The app is a **general notification categorizer**, not just a promo-code extractor.
- **AI runtime is undecided** (llama.cpp vs Google's runtime). This spec provides an abstraction and a decision gate (§9.4).
- **Outstanding UI/UX**, with a defined palette, guidelines, and pages (§12–§13).

---

## 3. Decisions register

Defaults are binding for the agent until the maintainer changes them. Record each in `docs/adr/`.

| ID | Decision | Default | Notes |
|---|---|---|---|
| **D-01** | Product name | **Alak / الک** (codename) | Alternatives: *Sift*, *Stub*. Verify availability (Play, F-Droid, GitHub, domain) before public release. Never hard-code the name; use `AppInfo.Name` and resource strings. |
| **D-02** | Licenses | App code: **GPL-3.0-or-later**. Language packs, corpora, model catalog: **CC0-1.0**. | GPL protects a privacy app from closed, tracker-laden forks. Apache-2.0 is the alternative if maximum adoption matters more. Use SPDX headers and the REUSE spec. |
| **D-03** | How models reach the phone | **Import only.** The app has *no* INTERNET permission. The user downloads a model file with their browser (or any means) and imports it via the Android file picker. The Models screen offers a curated list with "Open in browser" links. | Preserves "completely offline, verifiably." An in-app downloader would require INTERNET and break R2. |
| **D-04** | AI runtime | **Abstraction + spike gate (§9.4).** Provisional first engine: **llama.cpp**. Second engine (LiteRT-LM) is an optional plugin. | Do not use MediaPipe LLM Inference: it is deprecated (§9.4). |
| **D-05** | Android API levels | `minSdk 28`, `targetSdk 36` | minSdk 28 simplifies native builds (llama.cpp docs use `android-28`) and still covers the vast majority of active devices. VERIFY store requirements for targetSdk at release time. |
| **D-06** | Distribution | GitHub Releases (signed APK), F-Droid, and Iranian stores (Cafe Bazaar, Myket). Google Play is optional. | VERIFY each store's policy on notification-access apps. Play requires a prominent disclosure and justification for sensitive permissions. |
| **D-07** | Default capture depth per category | See §6.5. Chat apps default to "sender + time only" until the user opts in. OTP/security content is never stored. | Privacy by default. |
| **D-08** | AI summaries | User-triggered only. Never scheduled, never automatic. | Confirmed by maintainer. |
| **D-09** | Persian calendar and digits | Jalali dates and Persian digits are the default when the UI language is Persian. Both are user-toggleable. | |
| **D-10** | Target reference device ("R1") | Android 11+, 3 GB RAM, octa-core Cortex-A53/A55-class SoC (~1.8–2.0 GHz), eMMC or UFS 2.x storage. | All performance budgets (§14) are measured on R1 or worse. The agent MUST test on at least one physical device of this class. |

---

## 4. Glossary

| Term | Meaning |
|---|---|
| **Capture** | Receiving a notification from the OS listener and persisting it. |
| **Category** | The single primary bucket of a notification (§6.1). |
| **Tag** | A secondary, multi-valued label (`promo-code`, `expiring`, `meeting`…). |
| **Entity** | A structured fact extracted from text (promo code, percent, amount, date…). |
| **Concept** | A language-neutral idea (`promo.urgency`, `otp.trigger`) that language packs fill with words. |
| **Language pack** | Data files that map concepts to words/patterns for one language (§8). |
| **Evidence** | One piece of reasoning contributed by a rule (`+0.30 Offers: phrase "up to 70% off"`). |
| **Tier 1 / 2 / 3** | Rules / adaptive classifier / SLM. |
| **Stub** | The ticket-shaped UI component that shows a promo code (§12). |
| **Capture depth** | How much of a notification is stored: Full, Headline, Metadata, Ignore. |
| **SLM** | Small language model run locally on the device. |
| **Engine** | A runtime that can run an SLM (llama.cpp, LiteRT-LM…). |

---

## 5. Architecture

### 5.1 Technology stack

| Concern | Choice | Notes |
|---|---|---|
| Runtime / UI | **.NET 10 (LTS)** with **.NET MAUI 10**. App TFM `net10.0-android36.0`; libraries `net10.0`. | .NET 10 is the LTS release (GA Nov 2025). Pin the SDK in `global.json`; pin workloads in the repo docs. VERIFY the exact Android workload version at project start. |
| UI pattern | XAML + **MVVM** using `CommunityToolkit.Mvvm` and `CommunityToolkit.Maui`. Shell navigation. | No Blazor Hybrid (extra WebView cost). |
| Custom drawing | `Microsoft.Maui.Graphics` (`GraphicsView`) for the Stub and the Tape strip. | Avoid SkiaSharp to keep APK small unless a spike proves it necessary. |
| Storage | `Microsoft.Data.Sqlite` + **SQLCipher** (via a SQLitePCLRaw SQLCipher bundle). FTS5 for search. | VERIFY (PF-10) that the chosen bundle includes FTS5. Fallback: encrypt fields, or use plain SQLite plus Android file-based encryption and document the trade-off. |
| DI / config | `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`. | |
| JSON | `System.Text.Json` with **source generators** (trimming friendly). | |
| Regex | `System.Text.RegularExpressions` with match timeouts. | See RUL-006. |
| Native inference | C shim over **llama.cpp** built with the Android NDK (arm64-v8a). Optional second engine: LiteRT-LM. | §9.4. |
| Icons | **Phosphor Icons** (MIT) as an icon font. | VERIFY license at import. |
| Fonts | **Vazirmatn** (Persian + Latin, OFL) for UI; a monospace face for codes (JetBrains Mono, OFL). | VERIFY licenses; bundle only the weights used (§12.3). |
| Tests | xUnit, **Shouldly** (not FluentAssertions ≥ 8, which changed licensing), BenchmarkDotNet, YamlDotNet (tooling/tests only). | |
| Tooling | `alak-pack` CLI (dotnet tool in this repo). | §8.9. |
| Dev environment | Windows or Linux, JetBrains Rider or VS Code. | Keep all logic in plain `net10.0` libraries so >90% of tests run without an emulator. |

### 5.2 Repository / solution layout

```
alak/
├─ src/
│  ├─ Alak.Core/                    net10.0   Domain models, taxonomy, pipeline interfaces. No Android/MAUI references.
│  ├─ Alak.Text/                    net10.0   Normalization, script/language detection, bidi, tokenization, numbers, dates (incl. Jalali).
│  ├─ Alak.Rules/                   net10.0   Pack loader/compiler, extractors, evidence scoring (Tier 1).
│  ├─ Alak.Adaptive/                net10.0   Hashed online logistic regression (Tier 2).
│  ├─ Alak.Storage/                 net10.0   SQLite/SQLCipher repositories, migrations, FTS, retention.
│  ├─ Alak.Inference.Abstractions/  net10.0   IInferenceEngine, prompts, task schemas, output validators, model probing.
│  ├─ Alak.Inference.LlamaCpp/      net10.0-android36.0   P/Invoke bindings to the native shim.
│  ├─ Alak.Inference.LiteRtLm/      (optional, later)     Second engine behind the same interface.
│  ├─ Alak.App/                     net10.0-android36.0   MAUI app: Views, ViewModels, design system, Platforms/Android.
│  └─ Alak.Packs.Tool/              net10.0   `alak-pack` CLI (validate, test, lint, coverage, scaffold).
├─ native/llama-shim/               C shim + CMake; produces libalak_llama.so per ABI.
├─ packs/                           en/  fa/  fa-latn/  _template/
├─ catalog/                         models.json (model catalog), apps.json (package → hints)
├─ tests/                           Alak.*.Tests, corpus/ (labelled notification corpora)
├─ docs/                            spec/, adr/, design/, contributing/, verification.md
└─ .github/workflows/
```

**Dependency rule:** arrows only point inward: `App → (Rules, Adaptive, Storage, Inference.*) → (Text, Core)`. `Core` and `Text` depend on nothing but the BCL.

### 5.3 Runtime view

```
 Other apps ─► Android NotificationManager
                       │  (binder, one-way)
                       ▼
   NotificationCaptureService  (Android, same process as the app)
     1. copy fields → immutable RawNotification            budget ≤ 5 ms
     2. enqueue → bounded Channel<RawNotification>
                       │
                       ▼
   IngestPipeline  (background, single reader)
     a. Policy gate: ignore? depth? sensitive guard?
     b. Persist raw row
     c. Normalize → detect language(s)
     d. Tier 1: extract entities, collect evidence
     e. Tier 2: adaptive probabilities (if trained)
     f. Decide: category, tags, confidence, needs_review
     g. Persist enrichment · update FTS · update codes wallet · schedule reminders
     h. Publish domain events (in-process event bus)
                       │
        ┌──────────────┴───────────────┐
        ▼                              ▼
  UI (ViewModels observe events)   Tier 3 — only when the user taps
                                   ModelManager → IInferenceEngine → validated JSON
```

Key properties:

- The capture path MUST NOT depend on any Activity or UI object. The service can be bound by the OS when no UI exists.
- A single reader processes the queue, so ordering is deterministic and the DB has one writer.
- If the process dies mid-pipeline, rows persisted in step (b) with `enriched_at IS NULL` are re-processed at next start.

### 5.4 Platform facts to VERIFY

These were researched at spec time but MUST be re-confirmed on real hardware during M1. Record results in `docs/verification.md`.

| ID | Fact (as understood) | How to verify |
|---|---|---|
| PF-01 | A `NotificationListenerService` is bound only after the user grants *Notification access*. `OnListenerConnected` fires; `RequestRebind` recovers after disconnects. | Build the M1 spike; toggle access on/off; kill the process; reboot. |
| PF-02 | **Android 15+ withholds or redacts OTP-bearing notification content from "untrusted" listeners.** The permission that would lift this (`RECEIVE_SENSITIVE_NOTIFICATIONS`) is `signature\|role` and is not obtainable by ordinary apps. | Send yourself a test OTP SMS on an Android 15/16 device; log what the listener receives. Design already assumes redaction (CAP-008). |
| PF-03 | **Sideloaded apps hit "Restricted settings" (Android 13+) and stricter "Enhanced confirmation" (Android 15+)**; the Notification access switch may be greyed out until the user allows restricted settings in App info. Installs via F-Droid or Play use a store-trusted installer path and are typically not restricted. | Install the APK via `adb install` and via a file manager; observe the switch. Onboarding MUST explain the fix (§13.1). |
| PF-04 | Listeners can read channel metadata (`getNotificationChannels`, API 26+): channel id and name are strong classification signals ("promotions", "offers"). | Log channel id/name for 20 real apps. |
| PF-05 | Resolving app labels/icons for arbitrary packages may be blocked by package-visibility rules without `QUERY_ALL_PACKAGES`, which Play restricts. | Test on Android 11+; see CAP-013 for the fallback strategy. |
| PF-06 | OEMs (Xiaomi/MIUI/HyperOS, Oppo, Vivo, Huawei, Samsung, others) may kill background processes and revoke the listener binding. | Test on at least two OEM skins; build the guidance table for §13.1. |
| PF-07 | Play requires native libraries to be **16 KB page-size compatible** for apps targeting Android 15+. Recent NDKs build compatible by default; older ones need a linker flag (`-Wl,-z,max-page-size=16384`). | Build the shim with the current NDK; check alignment with `llvm-readelf`/`zipalign -c -P 16`. |
| PF-08 | Clipboard entries can be flagged sensitive (`ClipDescription.EXTRA_IS_SENSITIVE`, API 33) so the keyboard/OS hides the preview. | Copy a code on API 33+ and observe the overlay. |
| PF-09 | .NET's `PersianCalendar` may disagree with the official Iranian calendar for some leap years. | Compare against a trusted table for Jalali years 1400–1420 (§8.4). |
| PF-10 | The SQLitePCLRaw SQLCipher bundle works on Android and includes FTS5. | Create an encrypted DB, `CREATE VIRTUAL TABLE … USING fts5`, query in Persian and English. |
| PF-11 | Starting the MAUI app process just to run the listener has a cost (runtime start, DI, handler registration). | Measure cold service start time and PSS on R1; optimize if the budget in §14 is missed (lazy DI, AOT/profiled AOT, trimming). |
| PF-12 | Notifications posted before first unlock after reboot are delivered only after the listener binds; `GetActiveNotifications()` returns what is still posted. | Reboot test; confirm backfill (CAP-002). |

### 5.5 Cross-cutting requirements

| ID | Requirement | Acceptance |
|---|---|---|
| ARC-001 | Every class in `Core`, `Text`, `Rules`, `Adaptive`, `Storage` MUST compile and be unit-testable on a desktop CI runner without an emulator. | CI job `unit-tests` runs on Linux with no Android workload. |
| ARC-002 | Public interfaces between layers MUST be small and documented: `IIngestPipeline`, `IClassifier`, `IEntityExtractor`, `INotificationStore`, `IModelManager`, `IInferenceEngine`, `IClock`, `ILanguagePackRepository`. | Interfaces exist with XML docs; fakes used in tests. |
| ARC-003 | Time MUST come from `IClock` (never `DateTime.Now` in logic). All timestamps are stored as UTC epoch milliseconds plus the local UTC offset in minutes at capture. | Analyzer/grep rule in CI; tests with fixed clocks. |
| ARC-004 | Nullable reference types enabled, warnings as errors, analyzers on. | Build fails on warnings. |
| ARC-005 | No static mutable state. Dependency injection everywhere except the Android service entry point, which resolves from the app's service provider. | Code review checklist. |
| ARC-006 | Every long-running operation is cancellable and reports progress. | Unit tests with cancelled tokens. |
| ARC-007 | Feature flags (compile-time constants or settings) exist for: Tier 2, Tier 3, Finglish pack, LiteRT-LM engine. | Flags documented in `docs/`. |

---

## 6. Domain model and storage

### 6.1 Categories

Each notification has **exactly one primary category**. The Codes wallet is a *collection* derived from entities (§10), not a category. A notification in any category can contribute a code.

| Id | Display name (EN / FA) | What belongs here | Glyph (Phosphor) | Color token |
|---|---|---|---|---|
| `offers` | Offers / تبلیغات و پیشنهادها | Ads, deals, sales, coupons-without-code, marketing pushes, newsletters | `tag` | `cat.offers` |
| `work` | Work / کار | Work email, calendar, meetings, tickets, code review, team-chat mentions, deployments | `briefcase` | `cat.work` |
| `people` | People / افراد | Messages and calls from individuals; group chats; missed calls | `chat-circle` | `cat.people` |
| `money` | Money / مالی | Bank and card alerts, transfers, bills, invoices, taxes, wallet balance | `wallet` | `cat.money` |
| `orders` | Orders & trips / سفارش و سفر | Delivery, shipping, ride status, flights, bookings, tickets, ride PINs | `package` | `cat.orders` |
| `security` | Security / امنیت | One-time passwords, login alerts, password or account-safety notices | `shield-check` | `cat.security` |
| `social` | Social & media / شبکه‌های اجتماعی | Likes, follows, feeds, streaming and content recommendations | `at` | `cat.social` |
| `updates` | Updates / به‌روزرسانی‌ها | App and system updates, device status, weather, news digests | `arrows-clockwise` | `cat.updates` |
| `other` | Unsorted / دسته‌بندی‌نشده | Low-confidence items awaiting review | `question` | `cat.updates` (dashed spine) |

**Priority order for conflicts (safety first):** `security` > `money` > `work` > `orders` > `people` > `offers` > `social` > `updates` > `other`. The order breaks ties and is used by hard overrides (RUL-018); it does not outrank strong contrary evidence.

### 6.2 Tags (initial vocabulary; extensible by packs)

`promo-code, discount, free-shipping, cashback, flash-sale, expiring, referral, gift-credit, no-code-needed, newsletter, unsubscribe-available, urgent, meeting, mention, review-requested, build-status, incident, invoice, payment-sent, payment-received, bill-due, low-balance, tax, delivery, out-for-delivery, delivered, ride, ride-pin, flight, booking, verification-code, login-alert, password-change, missed-call, message, group-message, like, follow, update-available, system, weather, news, reminder`.

Tags are lower-kebab-case ASCII identifiers. Display names come from resource files, never from pack authors' raw strings.

### 6.3 Entities

Entities are structured facts pulled out of text by Tier 1 (and optionally Tier 3). All entities carry `field` (which text field), `span` (start/end offsets into the **original** text, via the normalization span map, §8.2), `confidence` 0–1, and `rule_id`.

| Type | Value fields | Notes |
|---|---|---|
| `promo_code` | `code`, `kind` (promo, referral, gift, voucher, unknown), `discount`, `min_spend`, `cap`, `scope`, `first_order`, `single_use`, `valid_from`, `valid_until`, `store_hint` | Central to the Codes wallet. |
| `discount` | `type` (percent, amount, free_shipping, bogo, cashback, credit, unknown), `value`, `currency`, `up_to` | May exist without a code. |
| `money` | `value`, `currency` (ISO 4217 or `IRT` for Toman), `direction` (debit, credit, balance, unknown), `account_hint` (last 4 only) | Toman/Rial are distinct units (10 Rial = 1 Toman). Store the unit the text used and a normalized value. |
| `datetime` | `role` (expiry, start, event, due), `instant` (UTC ms), `precision` (date, time), `calendar` (gregorian, jalali), `ambiguous` | Relative expressions ("tonight") resolve against `posted_at`. |
| `url` | `url`, `host` | Never fetched (no network). |
| `phone` | `number` | |
| `order_ref` | `kind` (order, tracking, invoice, ticket, booking, pin), `value` | Prevents tracking/order codes from being mistaken for promo codes. |
| `sender` | `name`, `kind` (person, brand, system) | From title, conversation title, or app. |
| `otp` | *(value never persisted)* | Only its presence and length are kept. |

### 6.4 Capture depth

| Depth | Stored | Use |
|---|---|---|
| **Full** | Title, text, big text, sub text, lines, action labels, entities | Default for most categories. |
| **Headline** | App, title (usually the sender), time, category; *body discarded* | Default for chats. |
| **Metadata** | App, time, category only | Default for Security. |
| **Ignore** | Nothing | User-selected or default-excluded apps. |

Depth is decided **before persistence** (CAP-007). Text that will not be stored MUST be dropped from memory as soon as classification finishes. Classification itself still runs on the full text in memory so the category is right.

### 6.5 Default depth by detected category

| Category | Default depth | Notes |
|---|---|---|
| offers, orders, updates, social, other | Full | |
| work | Full | Per-app override available. |
| money | Full | Setting: "Hide amounts in lists" (UI only, still stored). |
| people | **Headline** | User can raise per app. |
| security | **Metadata** | OTP content is *never* stored, at any depth (CAP-008). |

A **new-app card** appears the first time an app is seen: "Alak noticed *AppName*. Keep full messages, sender only, or ignore?" Default is the category-derived depth; the card is informative, not blocking.

### 6.6 SQLite schema (v1)

The agent MUST implement migrations (`schema_migrations`), WAL mode, foreign keys ON, and SQLCipher keyed from an Android Keystore-wrapped key (SEC-002). Column names below are normative; types are SQLite affinities.

```sql
CREATE TABLE apps (
  package         TEXT PRIMARY KEY,
  label           TEXT,
  first_seen_at   INTEGER NOT NULL,
  default_category TEXT,               -- user or catalog default
  depth           INTEGER,             -- NULL = derive from category (§6.5)
  muted           INTEGER NOT NULL DEFAULT 0,
  auto_archive    INTEGER NOT NULL DEFAULT 0,
  region_hint     TEXT,
  notif_count     INTEGER NOT NULL DEFAULT 0,
  last_seen_at    INTEGER,
  icon_png        BLOB                 -- small cached icon, optional (CAP-013)
);

CREATE TABLE notifications (
  id              INTEGER PRIMARY KEY,
  uid             TEXT NOT NULL UNIQUE,          -- ULID
  sbn_key         TEXT NOT NULL,
  package         TEXT NOT NULL REFERENCES apps(package),
  posted_at       INTEGER NOT NULL,              -- UTC ms
  captured_at     INTEGER NOT NULL,
  updated_at      INTEGER NOT NULL,
  removed_at      INTEGER,
  removal_reason  INTEGER,
  tz_offset_min   INTEGER NOT NULL,
  channel_id      TEXT,
  channel_name    TEXT,
  android_category TEXT,
  importance      INTEGER,
  group_key       TEXT,
  title           TEXT,
  text            TEXT,
  big_text        TEXT,
  sub_text        TEXT,
  lines_json      TEXT,
  actions_json    TEXT,
  depth           INTEGER NOT NULL,
  redacted        INTEGER NOT NULL DEFAULT 0,
  langs           TEXT,                           -- e.g. "fa,en"
  category        TEXT NOT NULL DEFAULT 'other',
  category_source TEXT NOT NULL DEFAULT 'rules',  -- rules|adaptive|slm|user|app-default
  confidence      REAL NOT NULL DEFAULT 0,
  needs_review    INTEGER NOT NULL DEFAULT 0,
  tags_json       TEXT,
  evidence_json   TEXT,                           -- explainability trace (RUL-020)
  content_hash    TEXT NOT NULL,
  dup_of          INTEGER REFERENCES notifications(id),
  dup_count       INTEGER NOT NULL DEFAULT 1,
  state           INTEGER NOT NULL DEFAULT 0,     -- 0 new, 1 read, 2 archived, 3 trashed
  pinned          INTEGER NOT NULL DEFAULT 0,
  rules_version   TEXT,
  enriched_at     INTEGER
);
CREATE INDEX ix_n_posted        ON notifications(posted_at DESC);
CREATE INDEX ix_n_cat_posted    ON notifications(category, posted_at DESC);
CREATE INDEX ix_n_pkg_posted    ON notifications(package, posted_at DESC);
CREATE INDEX ix_n_hash          ON notifications(package, content_hash);
CREATE INDEX ix_n_sbn           ON notifications(sbn_key);
CREATE INDEX ix_n_review        ON notifications(posted_at DESC) WHERE needs_review = 1;
CREATE INDEX ix_n_unenriched    ON notifications(id) WHERE enriched_at IS NULL;

CREATE TABLE entities (
  id              INTEGER PRIMARY KEY,
  notification_id INTEGER NOT NULL REFERENCES notifications(id) ON DELETE CASCADE,
  type            TEXT NOT NULL,
  value_json      TEXT NOT NULL,
  field           TEXT NOT NULL,                  -- title|text|big_text|sub_text|line
  span_start      INTEGER,
  span_end        INTEGER,
  confidence      REAL NOT NULL,
  rule_id         TEXT
);
CREATE INDEX ix_e_notif ON entities(notification_id);
CREATE INDEX ix_e_type  ON entities(type);

CREATE TABLE promo_codes (                        -- the Codes wallet
  id              INTEGER PRIMARY KEY,
  code            TEXT NOT NULL,                  -- as displayed (original case)
  code_norm       TEXT NOT NULL,                  -- upper-cased, separators normalized
  package         TEXT NOT NULL REFERENCES apps(package),
  store_name      TEXT,
  kind            TEXT NOT NULL DEFAULT 'promo',
  discount_json   TEXT,
  min_spend_json  TEXT,
  cap_json        TEXT,
  conditions_json TEXT,
  first_seen_at   INTEGER NOT NULL,
  last_seen_at    INTEGER NOT NULL,
  times_seen      INTEGER NOT NULL DEFAULT 1,
  notification_id INTEGER REFERENCES notifications(id) ON DELETE SET NULL,
  valid_until     INTEGER,                        -- UTC ms, NULL = unknown
  valid_until_precision INTEGER,                  -- 0 unknown, 1 date, 2 time
  status          INTEGER NOT NULL DEFAULT 0,     -- 0 active, 1 used, 2 expired, 3 dismissed
  used_at         INTEGER,
  note            TEXT,
  reminder_at     INTEGER,
  UNIQUE (code_norm, package)
);

-- Standalone FTS5 over NORMALIZED text (see §8.2) so Persian/English variants match.
CREATE VIRTUAL TABLE notification_fts USING fts5(
  title_n, body_n, sender_n,
  tokenize = 'unicode61 remove_diacritics 2'
);                                                -- rowid = notifications.id

CREATE TABLE user_rules (
  id INTEGER PRIMARY KEY, name TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1,
  priority INTEGER NOT NULL DEFAULT 100, match_json TEXT NOT NULL, action_json TEXT NOT NULL,
  hit_count INTEGER NOT NULL DEFAULT 0, created_at INTEGER NOT NULL
);

CREATE TABLE corrections (                        -- training signal for Tier 2 (§9.1)
  id INTEGER PRIMARY KEY, notification_id INTEGER, package TEXT NOT NULL,
  features_blob BLOB NOT NULL,                    -- hashed feature indices, NOT readable text
  from_category TEXT, to_category TEXT NOT NULL, created_at INTEGER NOT NULL
);

CREATE TABLE digests (
  id INTEGER PRIMARY KEY, day TEXT NOT NULL, kind TEXT NOT NULL,   -- 'rules'|'slm'
  content_json TEXT NOT NULL, model_id INTEGER, created_at INTEGER NOT NULL,
  UNIQUE (day, kind)
);

CREATE TABLE slm_results (
  id INTEGER PRIMARY KEY, notification_id INTEGER REFERENCES notifications(id) ON DELETE CASCADE,
  task TEXT NOT NULL, model_id INTEGER, input_hash TEXT NOT NULL, output_json TEXT,
  valid INTEGER NOT NULL DEFAULT 0, tokens_in INTEGER, tokens_out INTEGER,
  duration_ms INTEGER, created_at INTEGER NOT NULL
);

CREATE TABLE models (
  id INTEGER PRIMARY KEY, display_name TEXT NOT NULL, path TEXT NOT NULL, format TEXT NOT NULL,
  engine TEXT NOT NULL, size_bytes INTEGER NOT NULL, sha256 TEXT NOT NULL, metadata_json TEXT,
  benchmark_json TEXT, added_at INTEGER NOT NULL, last_used_at INTEGER, is_active INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE packs (
  id TEXT PRIMARY KEY, version TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1,
  source TEXT NOT NULL, sha256 TEXT NOT NULL, installed_at INTEGER NOT NULL
);

CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE schema_migrations (version INTEGER PRIMARY KEY, applied_at INTEGER NOT NULL);
```

### 6.7 Retention and pruning

| ID | Requirement | Acceptance |
|---|---|---|
| STO-001 | Defaults: Offers/Social/Updates/Other 30 days; Work/Orders/People 90 days; Money 365 days; Security metadata 7 days. Codes stay until `valid_until + 30 days` (or 90 days after last seen when expiry is unknown). Pinned items never expire. All values are user-editable per category. | Unit tests with a fake clock. |
| STO-002 | A daily job (Android WorkManager, no exact alarms) prunes expired rows, cascades entities, and rebuilds nothing else. | Instrumented test or manual checklist. |
| STO-003 | Global soft cap (default 200 MB). Above the cap, prune oldest non-pinned rows first, *never* active codes. | Test with synthetic 100k rows. |
| STO-004 | Keyset pagination for every list query (no `OFFSET`). | Query-plan test asserts index use. |
| STO-005 | Reprocessing: when a pack or engine version changes, rows with `category_source IN ('rules','adaptive')` are re-enriched lazily in the background, oldest last. Rows with `category_source = 'user'` are never changed. | Test: change rules_version, assert user rows untouched. |
| STO-006 | `android:allowBackup="false"` and data-extraction rules exclude everything, so the OS never uploads the DB to a cloud backup. | Manifest lint in CI (OSS-004). |

---

## 7. Capture layer (Android)

### 7.1 Service skeleton

```csharp
[Service(Name = "dev.alak.app.NotificationCaptureService",   // TODO: final application id (D-01)
         Label = "@string/app_name",
         Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE",
         Exported = true)]
[IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
public sealed class NotificationCaptureService : NotificationListenerService
{
    public override void OnListenerConnected()   { /* mark healthy; backfill active notifications */ }
    public override void OnListenerDisconnected(){ /* mark unhealthy; RequestRebind(componentName) */ }
    public override void OnNotificationPosted(StatusBarNotification? sbn, RankingMap? ranking) { /* copy → enqueue */ }
    public override void OnNotificationRemoved(StatusBarNotification? sbn, RankingMap? ranking, NotificationListenerCancelReason reason) { /* record removal */ }
}
```

The DTO handed to the pipeline (all values copied; no Android objects escape the binder thread):

```csharp
public sealed record RawNotification(
    string SbnKey, string Package, int Id, string? Tag,
    long PostedAtUtcMs, int TzOffsetMin,
    string? ChannelId, string? ChannelName, string? AndroidCategory, int Importance,
    bool IsOngoing, bool IsGroupSummary, bool IsForegroundService, bool IsClearable,
    string? GroupKey, string? Title, string? Text, string? BigText, string? SubText,
    IReadOnlyList<string> TextLines, IReadOnlyList<string> ActionLabels,
    string? ConversationTitle, bool LooksRedactedByOs, int UserHandle);
```

### 7.2 Capture requirements

| ID | Requirement | Acceptance |
|---|---|---|
| CAP-001 | The service MUST be declared as above and MUST work with **no Activity alive**. | Force-stop UI, post a test notification, see the row appear. |
| CAP-002 | On `OnListenerConnected`, call `GetActiveNotifications()` and ingest any notification not already stored (identified by `sbn_key` + `posted_at`). This covers first-run, post-reboot (PF-12) and rebind. | Reboot test; grant-access test shows the current shade content imported. |
| CAP-003 | Copy these fields on the binder thread: package, id, tag, key, post time, channel id and name (API 26+), `Notification.category`, importance (from ranking), ongoing/group-summary/foreground-service/clearable flags, group key, title, text, big text, sub text, text lines, action *labels* (not PendingIntents), conversation title. Budget ≤ 5 ms. | Unit test on mapper with fakes; on-device timing log. |
| CAP-004 | The app MUST NOT call `CancelNotification`, `SnoozeNotification`, `SetNotificationsShown` or otherwise alter other apps' notifications. | Code search in CI; review checklist. |
| CAP-005 | **Default ignore filter:** own package; ongoing notifications; foreground-service notifications; group summaries (children are captured); `CATEGORY_TRANSPORT`, `CATEGORY_PROGRESS`, `CATEGORY_SERVICE`, `CATEGORY_CALL` (in-progress), `CATEGORY_NAVIGATION`, `CATEGORY_ALARM`, `CATEGORY_STOPWATCH`. Each filter is user-toggleable in Settings → Capture. | Table-driven tests. |
| CAP-006 | **Update semantics:** a notification with an existing live `sbn_key` updates that row (`updated_at`, changed text) instead of creating a new one. Chats that append messages update in place. **Cross-post dedupe:** the same `(package, content_hash)` within 6 h increments `dup_count` on the first row and links `dup_of`. `content_hash` = hash of normalized title + text. | Tests: repeated promo push → 1 row, `dup_count` 3. |
| CAP-007 | The **policy gate** runs before persistence: resolve the app's depth (user setting → catalog → category default), drop fields not covered by the depth, then persist. Classification uses the full in-memory text. | Test: Headline depth stores no body but still yields correct category. |
| CAP-008 | **Sensitive guard.** OTP/verification content MUST NOT be persisted at any depth. Detect via the OTP concept (RUL-014) *and* via OS redaction (PF-02). If the OS redacted the content, store `redacted=1`, category `security`, metadata only. First determine on a real Android 15/16 device how redaction manifests (empty fields, placeholder, public version) and encode it in `RedactionDetector` with a recorded fixture. | Fixture-driven tests; manual test with a real OTP SMS. |
| CAP-009 | **Health monitor.** Track `listener_connected`, `last_captured_at`, and `last_rebind_at`. If access is granted but no callback arrived for 24 h while other apps were active, surface a "Check capture" banner (§13.9). On `OnListenerDisconnected`, call `RequestRebind` with backoff. | Simulated disconnect test; UI test for banner logic. |
| CAP-010 | **Burst safety:** 500 notifications in 60 s MUST not drop or reorder. Channel capacity 2048; on overflow, spill to a small on-disk queue rather than dropping. | Load test. |
| CAP-011 | **Default exclusions** (Depth=Ignore until the user opts in): password managers, authenticator apps, keyboards, VPN/clean-up utilities' foreground notices, and the system UI. The exclusion list is data in `catalog/apps.json`, populated only with *verified* package ids (R1). | Catalog validator test. |
| CAP-012 | Store the removal reason (`click`, `user dismissed`, `app cancel`, `timeout`, …) to power "dismissed unseen" views. | Unit test on reason mapping. |
| CAP-013 | **App label and icon resolution** order: (1) `PackageManager` lookup; (2) the notification's package context (`sbn.GetPackageContext`) for label/icon; (3) humanized package name. Do **not** add `QUERY_ALL_PACKAGES` unless the maintainer approves (D-06, PF-05). Cache a ≤ 96 px icon per captured app only. | Test on Android 11+ with apps from different sources. |
| CAP-014 | Large images, big pictures, and remote content are never stored or fetched. | Schema has no such columns. |
| CAP-015 | Notification `Locale`/time zone: persist UTC ms plus offset minutes at capture (ARC-003). | Unit test. |
| CAP-016 | All capture code MUST catch and log exceptions per notification; one malformed notification never stops the service. | Fuzz test with malformed extras. |

### 7.3 Manifest and permissions

| Permission / setting | Required | Why |
|---|---|---|
| `BIND_NOTIFICATION_LISTENER_SERVICE` (on the service) | Yes | Receive notifications. |
| `POST_NOTIFICATIONS` (Android 13+) | Optional, requested lazily | Only for the app's own expiry reminders (§10). |
| `USE_BIOMETRIC` | Optional | App lock. |
| `INTERNET`, `ACCESS_NETWORK_STATE`, `READ_EXTERNAL_STORAGE`, `QUERY_ALL_PACKAGES` | **Must not be declared** (unless D-06 is changed) | Privacy promise; model import uses the Storage Access Framework file picker, which needs no storage permission. |
| `android:allowBackup` | `false` | STO-006. |
| `android:exported` | Only the listener service and launcher activity | Minimize attack surface. |

---

## 8. Tier 1 — Rule engine and language packs

Tier 1 is the workhorse. It MUST run in milliseconds on the reference device (D-10), be fully deterministic, and explain itself. It is designed to feel "intelligent" through breadth (concept-level matching, morphology tolerance, typo tolerance, evidence fusion) rather than through a model.

### 8.1 Pipeline overview

```
RawNotification
   │
   ▼
[1] Normalize each text field ─► NormalizedText { Text, Folded, SpanMap, EmojiSignals }
   │
   ▼
[2] Script runs + language guess (Latn / Arab; en / fa / fa-Latn / unknown)
   │
   ▼
[3] Concept matching: one Aho–Corasick automaton over ALL enabled packs' lexicons
        + fuzzy pass for single-word terms (bounded edit distance)
        → ConceptHits[ concept, span, weight ]
   │
   ▼
[4] Extractors (C#): numbers, currency, dates, promo codes, discounts, money, order refs, OTP, urls, senders
        + pack pattern rules (regex, NonBacktracking) activated by concept hits
        → Entities[ type, value, span, confidence, rule_id ]
   │
   ▼
[5] Evidence collection: signal rules + metadata priors + app profile + user rules
        → Evidence[ category, weight, reason ]
   │
   ▼
[6] Decision: softmax over categories, hard overrides, tags, needs_review, explanation trace
```

### 8.2 Normalization (`Alak.Text`)

Normalization produces two parallel views of each field and a span map so that highlights and stored spans always refer to the **original** text.

| ID | Requirement | Acceptance |
|---|---|---|
| NRM-001 | Apply Unicode **NFKC**. | Fullwidth `ＳＡＬＥ` and math-bold `𝐒𝐀𝐋𝐄` fold to `SALE`; Arabic presentation forms fold to base letters. |
| NRM-002 | Remove format/bidi controls from the matching view: U+200B, U+200E, U+200F, U+202A–U+202E, U+2066–U+2069, U+FEFF, U+00AD. **Keep** them in the display text. | RLM/LRM glued to a code (`SAVE20‏`) does not break extraction. |
| NRM-003 | Map **ZWNJ (U+200C) to a space** in the matching view. Lexicon terms use `~` (optional separator) so `می‌شود`, `میشود` and `می شود` all match one term. | Three spellings of one term produce identical hits. |
| NRM-004 | **Persian/Arabic letter unification** (matching view): ي/ى→ی, ك→ک, ۀ/ة→ه, أ/إ/آ→ا (keep `آ` as a distinct key only if a pack requires it), ؤ→و, ئ→ی where safe. Remove tatweel (U+0640) and harakat (U+064B–U+065F, U+0670). | `كد تخفيف` (Arabic keyboard) matches the Persian lexicon term `کد تخفیف`. |
| NRM-005 | **Digits:** Persian ۰–۹ (U+06F0–06F9) and Arabic-Indic ٠–٩ (U+0660–0669) → ASCII `0–9` in the matching view. **Separators:** ٬→`,`, ٫→`.`, ٪→`%`, ،→`,`, ؛→`;`, ؟→`?`. Packs then match ASCII digits only. | `۵۰٪` becomes `50%`; span map still points to the original glyphs. |
| NRM-006 | **Case-folding view** (`Folded`): invariant lowercase for Latin; keep original case in `Text` (codes need it). | Code `SaveNow10` displayed exactly as sent. |
| NRM-007 | **Elongation:** in `Folded`, collapse runs of ≥3 identical letters to one (`saaale` → `sale`). The lexicon compiler also accepts the single-letter form of terms that contain a legitimate double letter. | `SAAALE!!!` hits `promo.discount.word`. |
| NRM-008 | Whitespace: NBSP/tabs → space; collapse runs of spaces; keep `\n` as a line boundary. | Multi-line bigText keeps line structure. |
| NRM-009 | **Emoji:** removed from `Folded` (replaced by a space) but counted into `EmojiSignals` (promo: 🔥🎁💥🎉🏷️💰🛍️; urgency: ⏰⌛🚨⚡; delivery: 📦🚚; security: 🔒🔐⚠️). Signals feed evidence with small weights. | Unit tests per emoji class. |
| NRM-010 | **Span map:** `int[] Map` from normalized index to original index (plus a helper `OriginalSpan(start,end)`). Every entity span stored in the DB is an *original* span. | Property test: for random text, `Original[Map[i]]` round-trips the visible glyph. |
| NRM-011 | **Confusable letters (MAY):** in `Folded`, map common Cyrillic/Greek Latin-lookalikes inside otherwise-Latin tokens; low-weight obfuscation handling (`fr33`, `0ff`) only for a fixed high-value set (`free`, `off`, `sale`). | Adversarial corpus cases. |
| NRM-012 | Normalization MUST be pure, allocation-conscious (spans, pooled buffers), and ≤ 1 ms for 1 KB of text on R1. | Benchmark. |

### 8.3 Script and language detection

| ID | Requirement | Acceptance |
|---|---|---|
| LNG-001 | Split text into **script runs** (Latin, Arabic-script, other). Keep digits/punctuation attached to the surrounding run. | Mixed `کد SAVE20 را وارد کنید` yields Arab, Latn, Arab runs. |
| LNG-002 | Guess language per Arabic-script run: **Persian** if it contains پ چ ژ گ or ی/ک-unified letters plus common Persian function words (`است`, `را`, `که`, `برای`, `با`, `در`); **Arabic** if it contains ة/ي (pre-normalization) and Arabic function words. Output a probability, not a hard label. | Unit tests on 50 short strings. |
| LNG-003 | Guess **Finglish** (Persian in Latin letters) for Latin runs when hits from the `fa-latn` lexicon outnumber `en` hits. | Tests on `kod takhfif shoma: EID25`. |
| LNG-004 | Language detection is **advisory**: all enabled packs run regardless, gated only by cheap script prefilters (a pack whose `scripts` do not occur in the text is skipped). | Perf test: English-only text does not touch Persian regexes. |
| LNG-005 | Text direction for **display** is decided per text block by first-strong-character, independent of UI language (§12.8). | UI test. |

### 8.4 Numbers, currency and dates

| ID | Requirement | Acceptance |
|---|---|---|
| NUM-001 | Parse numbers with grouping (`1,000`, `1 000`, `1.000` when locale-plausible) and decimals (`10.5`, `10,50 €` when a currency follows). | Table-driven tests incl. EU style. |
| NUM-002 | **Number words** are supplied by packs (`numbers.json`): units, tens, hundreds, thousand/million, plus scale multipliers (`هزار`, `میلیون`, `میلیارد`, `k`, `m`). Compose values such as `بیست و پنج هزار` = 25 000 and `fifty` = 50; `نصف`/`half` = 50 % in discount contexts. | Tests for both languages. |
| NUM-003 | **Currency:** `$ € £ ¥ ₹ USD EUR AED TRY …` (en) and `تومان`, `تومن`, `ریال`, `هزار تومان`, `میلیون تومان` (fa). Toman and Rial are distinct units with 10 Rial = 1 Toman; store the unit as written and a normalized base value. | `۲۰۰ هزار تومان` → 200 000 IRT; `۲٬۰۰۰٬۰۰۰ ریال` → 2 000 000 IRR = 200 000 IRT. |
| DAT-001 | Absolute dates: ISO, `Sep 30`, `30 September`, `9/30`, `30/9`, `9-30-26`, plus Jalali `1405/07/08`, `۸ مهر`, `۸ مهر ۱۴۰۵`. Month names (Gregorian and Jalali, including Dari names) come from packs. Ambiguous numeric dates (`03/04`) use the device locale order, are flagged `ambiguous`, and lower confidence. | Tests incl. ambiguity flag. |
| DAT-002 | **Jalali ↔ Gregorian conversion** MUST be correct for years 1400–1420. Do not trust `System.Globalization.PersianCalendar` blindly (PF-09): implement or vendor an algorithm and test against a known table. | Golden table test. |
| DAT-003 | Relative expressions resolve against `posted_at` in the notification's local offset: `tonight`/`امشب` → 23:59:59 local; `midnight`/`نیمه‌شب`/`ساعت ۲۴` → end of that day (00:00 next day, documented); `tomorrow`/`فردا`; `in 3 hours`/`۳ ساعت دیگر`; `48 hours`; `this weekend`/`آخر هفته` (Persian weekend: Thursday-Friday by common usage, Friday official; make it a pack setting). | Tests with fixed clocks and offsets. |
| DAT-004 | Times: `11:59 PM`, `23:59`, `ساعت ۲۳:۵۹`, `ساعت ۱۲ شب` (midnight), `ساعت ۱۲ ظهر` (noon), `تا ساعت ۲۴`. | Tests. |
| DAT-005 | **Role assignment:** anchors decide `role`: `until|through|by|expires|ends|valid till|before` and `تا|تا پایان|مهلت|منقضی|اعتبار|معتبر تا` → `expiry`; `starts|from|begins|از|شروع` → `start`; `due|سررسید` → `due`. Ranges (`Sep 20–25`, `از ۲۰ تا ۲۵ مهر`) yield start and expiry. | Tests. |
| DAT-006 | Weekday anchors (`until Sunday`, `تا پنجشنبه`) resolve to the next occurrence, not the past one. Persian weeks start on Saturday. | Tests. |

### 8.5 Language pack format (data only)

A pack is a folder in `packs/<id>/` in the repo, and a zip named `<id>-<version>.alakpack` when distributed. **No code, no scripts.**

```
packs/fa/
├─ pack.json        manifest
├─ lexicon.json     concept → terms (the main content)
├─ numbers.json     number words, scales, currencies
├─ dates.json       month names, weekdays, relative words, time words, calendar setting
├─ patterns.json    fragments + signal rules + pattern rules
├─ apps.json        (optional) package → hints, field maps
└─ tests/*.yaml     REQUIRED. Positive and negative cases.
```

**`pack.json`**

```json
{
  "schemaVersion": 1,
  "id": "fa",
  "name": "Persian",
  "nativeName": "فارسی",
  "version": "1.0.0",
  "minAppVersion": "1.0.0",
  "scripts": ["Arab"],
  "direction": "rtl",
  "calendars": ["jalali", "gregorian"],
  "weekend": ["thu", "fri"],
  "suffixSets": {
    "fa-nominal": ["ها", "های", "هایی", "ی", "ات", "ان", "تان", "شان", "مان", "ش", "م", "ت"]
  },
  "authors": ["Alak contributors"],
  "license": "CC0-1.0",
  "dependsOn": []
}
```

**`lexicon.json`** — terms are written in *normalized* form (§8.2). `~` means "optional ZWNJ, space, or nothing".

```json
{
  "concepts": {
    "promo.code.trigger": {
      "weight": 1.0,
      "terms": [
        "کد~تخفیف", "کد هدیه", "کد~کوپن", "کوپن", "کد~ویژه", "کد~اختصاصی",
        { "t": "با کد", "w": 0.9 },
        { "t": "با~استفاده~از~کد", "w": 0.95 }
      ]
    },
    "promo.code.trigger.weak": { "weight": 0.35, "terms": ["کد"] },
    "promo.discount.word": {
      "weight": 0.8,
      "suffixSet": "fa-nominal",
      "terms": ["تخفیف", "حراج", "شگفت~انگیز", "فروش~ویژه", "نصف~قیمت", "ارسال~رایگان"]
    }
  }
}
```

Term options: `t` (text), `w` (weight multiplier), `suffixSet` (allow morphology from the manifest), `fuzzy` (`true|false`, default true for single words of length ≥ 5), `flags` (`["word-boundary"]` default).

**`patterns.json`** — two kinds of rules. All regexes MUST compile with `RegexOptions.NonBacktracking` (linear-time; no lookarounds or backreferences) and MUST have a match timeout (RUL-006).

```json
{
  "fragments": {
    "lb": "(?:^|[\\s(\\[{:،,;\"'«])",
    "code_token": "[A-Za-z0-9](?:[A-Za-z0-9_-]{2,22})[A-Za-z0-9]"
  },
  "signalRules": [
    {
      "id": "fa.offers.percent_plus_discount",
      "when": { "all": ["promo.discount.word"], "entities": ["discount:percent"] },
      "emit": { "evidence": [{ "category": "offers", "weight": 1.2 }], "tags": ["discount"] },
      "explain": { "key": "why.discount_language" }
    },
    {
      "id": "fa.offers.ad_cancel_suffix",
      "when": { "regex": "(?:^|\\s)لغو\\s?\\d{1,3}\\s*$" },
      "emit": { "evidence": [{ "category": "offers", "weight": 1.6 }], "tags": ["unsubscribe-available"] },
      "explain": { "key": "why.ad_unsubscribe_marker" }
    }
  ],
  "patternRules": [
    {
      "id": "fa.promo.code.colon_after_trigger",
      "extractor": "promo_code.candidate",
      "requires": ["promo.code.trigger"],
      "regex": "{{concept:promo.code.trigger}}\\s*(?:شما|ویژه)?\\s*[:：]?\\s*[«\"'`]?(?<code>{{code_token}})",
      "weight": 0.9
    }
  ]
}
```

`{{concept:X}}` expands to an alternation of the concept's terms; `{{fragmentName}}` expands to a fragment. `explain.key` refers to an app-owned localized template (packs never inject UI text).

**`apps.json`** (optional; data-only app knowledge)

```json
{
  "apps": [
    {
      "package": "com.google.android.gm",
      "defaultCategory": "work",
      "fieldMap": { "sender": "title", "subject": "text", "snippet": "big_text", "account": "sub_text" },
      "notes": "Gmail: sub_text carries the account address; map accounts to Work/Personal in Sources."
    }
  ]
}
```

**Test file** (`tests/*.yaml`) — the contract for every rule:

```yaml
- id: fa-promo-first-order-001
  now: "2026-09-24T18:00:00+03:30"
  input:
    package: "test.shop"
    channel_name: "Promotions"
    title: "هدیه اولین سفارش"
    text: "کد تخفیف ۳۰٪ اولین سفارش شما: SNAPP30 — تا پایان امشب فرصت دارید"
  expect:
    category: offers
    tags: [promo-code, discount, expiring]
    entities:
      - { type: promo_code, code: SNAPP30, first_order: true,
          discount: { type: percent, value: 30 },
          valid_until: "2026-09-24T23:59:59+03:30" }
- id: fa-negative-postal-code-001
  input: { package: "test.shop", title: "ثبت آدرس", text: "کد پستی شما ۱۲۳۴۵۶۷۸۹۰ ثبت شد" }
  expect: { not_entities: [promo_code] }
```

| ID | Requirement | Acceptance |
|---|---|---|
| PCK-001 | Packs MUST validate against `schemaVersion: 1` JSON Schemas shipped in `docs/schemas/`. | `alak-pack validate` passes for bundled packs. |
| PCK-002 | **Bundled packs:** `en`, `fa`. **Extension pack (feature-flagged, experimental):** `fa-latn` (Finglish). | Packs load at start; disabled ones cost nothing. |
| PCK-003 | **User-installed packs:** import a `.alakpack` via the Android file picker; validate; run the pack's own `tests/` in a sandbox; show pass rate; enable only after the user confirms. Installed packs get an "unreviewed" badge unless their SHA-256 matches `catalog/trusted-packs.json`. | UI test with a valid and a malformed pack. |
| PCK-004 | **Safety:** reject packs with regexes that fail NonBacktracking compilation, exceed length limits (≤ 2 000 chars per regex, ≤ 5 000 rules per pack), or conflict with another pack's `normalization` overrides. Packs cannot reference files outside their folder. | Fuzz tests on malformed packs. |
| PCK-005 | Hot reload of packs without restarting the service; the AC automaton is rebuilt off-thread and swapped atomically. | Test: install pack while capturing. |
| PCK-006 | Every concept a pack fills MUST have at least 5 positive and 3 negative test cases, or the pack is flagged "incomplete" by `alak-pack coverage`. | CLI output. |
| PCK-007 | Packs declare a `version`; changing it triggers lazy re-enrichment (STO-005). | Test. |

### 8.6 Concept catalog (the contract between engine and packs)

Packs fill these concept IDs with words. The engine never contains natural-language words; it only knows concept IDs. New concepts require an app release; new *words* do not.

| Group | Concept IDs |
|---|---|
| Promo codes | `promo.code.trigger`, `promo.code.trigger.weak`, `promo.code.negative`, `promo.code.referral`, `promo.code.gift`, `promo.apply`, `promo.claim`, `promo.no_code`, `promo.single_use`, `promo.first_order`, `promo.min_spend`, `promo.cap` |
| Offers | `promo.discount.word`, `promo.free_shipping`, `promo.bogo`, `promo.cashback`, `promo.credit`, `promo.limited`, `promo.urgency.time`, `promo.scarcity`, `promo.event`, `ad.marker`, `ad.unsubscribe`, `ad.newsletter`, `ad.cta`, `ad.recommendation` |
| Security | `otp.trigger`, `otp.warning`, `otp.negative`, `security.login`, `security.password`, `security.alert`, `security.2fa` |
| Money | `money.debit`, `money.credit`, `money.transfer`, `money.balance`, `money.bill`, `money.tax`, `money.card`, `money.fraud`, `money.refund` |
| Work | `work.meeting`, `work.calendar`, `work.deadline`, `work.review`, `work.mention`, `work.assign`, `work.build`, `work.incident`, `work.ticket`, `work.doc`, `work.email_marker` |
| People | `people.missed_call`, `people.voice_message`, `people.group`, `people.greeting` |
| Orders & trips | `orders.status`, `orders.tracking`, `orders.delivery_person`, `orders.ride`, `orders.pin`, `orders.flight`, `orders.booking`, `orders.receipt` |
| Social | `social.reaction`, `social.follow`, `social.feed`, `social.live` |
| Updates | `updates.app`, `updates.system`, `updates.battery`, `updates.storage`, `updates.weather`, `updates.news` |
| Channel names | `channel.promo`, `channel.messages`, `channel.transactional`, `channel.security`, `channel.updates`, `channel.social` (matched against notification channel ids/names such as `promotions`, `offers`, `marketing`, `تبلیغات`, `پیشنهادها`) |
| Time words | `time.tonight`, `time.today`, `time.tomorrow`, `time.weekend`, `time.midnight`, `time.noon`, `time.hours_left`, `time.expires`, `time.starts`, `time.until` |

### 8.7 Extractor catalog

Extractors are C# code driven by concept hits and pack patterns. Each extractor lists what it MUST handle. "Edge cases" are mandatory test material.

#### E-01 Promo code (the flagship)

**Algorithm**

1. **Anchors.** Find `promo.code.trigger` (strong) and `promo.code.trigger.weak` hits. Also collect *anchorless* candidates from tokens that look like codes but only when an offer context exists (`promo.discount.word`, percent/amount discount entity, or `promo.apply`).
2. **Windows.** For each anchor, examine candidates in a window: up to 6 tokens after; up to 4 tokens before (for `SAVE20 is your code`, `X کد تخفیف شماست`). Windows do not cross a line break unless the anchor ends a line.
3. **Candidate shape scoring** (on the case-preserved `Text`):
   - Length 4–24 after trimming delimiters; hyphen/underscore-grouped forms allowed (`AB12-CD34-EF56`).
   - Positive: mixes letters and digits; all-uppercase; explicit delimiter (`:`, quotes `"`, `'`, `«»`, backticks, brackets, bold markers); position right after the anchor; standing alone on its own line.
   - Negative: dictionary word or a known stopword in caps (`SALE`, `OFF`, `FREE`, `NEW`, `TODAY`, `NOW`, `CODE`, `APP`, `SHOP`, plus per-pack `promo.code.stopwords`); purely alphabetic lowercase word; a URL fragment; a date; a price; a hashtag; a phone number.
   - **Numeric-only codes** (`482913`) are accepted only with a *strong* anchor and offer context, and **never** when any `otp.*` concept is present.
   - **Non-Latin codes** (e.g., `تابستان۱۴۰۵` after digit normalization) are accepted only when immediately after a strong anchor plus a delimiter, or quoted.
4. **Negative contexts** subtract confidence: `promo.code.negative` (`zip code`, `postal code`, `area code`, `tracking code`, `order code`, `کد پستی`, `کد ملی`, `کد رهگیری`, `کد پیگیری`, `کد سفارش`), and `otp.*`.
5. **Confidence** = logistic(anchor strength + shape score + delimiter bonus + offer-context bonus − negatives). `≥ 0.60` → create `promo_code` entity. `0.40–0.60` → *possible code*: shown with "Is this a code?" and used as a Tier 2 training signal. Below → discarded.
6. **Attach facts** from nearby entities: discount (percent/amount/free shipping…), `min_spend`, `cap` (`up to`, `حداکثر تا`), `first_order`, `single_use`, `valid_until`, store hint (app label). Multiple codes in one notification produce multiple entities; each gets the nearest discount/expiry.
7. **Kind:** `referral` when `promo.code.referral` is present (`کد معرف`, `invite code`); `gift` for gift/voucher terms; else `promo`.
8. **No code needed:** if `promo.no_code`/`promo.claim` is present, emit tag `no-code-needed` and *no* code.

**Mandatory edge cases (each needs positive and negative tests in every language)**

| Family | Examples |
|---|---|
| Shapes | `SAVE20`, `WELCOME-50`, `AB12-CD34-EF56`, `Spring26`, `FREESHIP`, `BF2026`, `x9K2mQ`, `10OFF`, lowercase-when-quoted `"save20"` |
| Position | code before anchor, after anchor, inline, on its own line, in bigText but not text, in a bullet, inside a URL query (`?coupon=SAVE20`, `?promo=`, `?code=`) |
| Punctuation glue | `کد:SAVE20`, `code:SAVE20.`, `(SAVE20)`, `«SAVE20»`, `SAVE20،`, `SAVE20!`, `‏SAVE20‏` (RLM), trailing emoji |
| Direction | Persian sentence with Latin code (RTL context), Latin sentence with Persian code, code at line boundary |
| Multiple | two codes with different discounts; a code and a referral code |
| Negatives | ZIP/postal/area code, tracking/order/invoice numbers, OTP, PINs, hashtags, product SKUs, coupon *words* in caps (`SALE`), dates like `20SEP` |
| Collisions | `Use code 482913 for 10% off` (promo, numeric, strong anchor) vs `Your code is 482913` (OTP) |
| Repetition | the same code re-sent hourly (dedupe to one wallet row, `times_seen++`, extend `last_seen_at`; expiry taken from the newest sighting only if it is later) |
| Updates | a later notification says `code SAVE20 expired` / `کد منقضی شد` → mark wallet row `expired` |

#### E-02 Discount / offer facts

Percent (`50%`, `50 percent`, `half off`, `تخفیف ۵۰ درصدی`, `۵۰٪ تخفیف`, `نصف قیمت`), amount off (`$10 off`, `save €5`, `۵۰ هزار تومان تخفیف`), up-to and ranges (`up to 70% off`, `تا ۷۰٪ تخفیف`, `30–70%`, `۳۰ تا ۷۰ درصد`), tiers (`10% off $50, 20% off $100`), BOGO (`buy one get one`, `۱+۱`, `یکی بخر یکی ببر`), free shipping (with `min_spend`), cashback (`۱۰٪ کش‌بک`, `cashback`), gift credit (`$5 credit`, `۲۰ هزار تومان اعتبار هدیه`), free gift. Emit `discount` entities and tags `discount`, `free-shipping`, `cashback`, `gift-credit`.

#### E-03 Money

Amounts with currency (E-03 uses NUM-003), direction from `money.*` concepts within 6 tokens (`debited`, `برداشت` = debit; `credited`, `واریز` = credit; `balance`, `مانده`, `موجودی` = balance), account hints (`ending 4417`, `card *4417`, `کارت ...۴۴۱۷` → last 4 only). Never store full card or account numbers (SEC-004).

#### E-04 Date/time and expiry

As DAT-001…006. Also `expiring` tag when `valid_until` is within 48 h of `posted_at`, and again at read time when a stored code approaches expiry.

#### E-05 Order / tracking / PIN references

`order #A1234`, `شماره سفارش ۱۲۳۴۵`, `کد رهگیری ۱۲۳۴۵۶`, common courier formats (UPS `1Z…`, 13-character postal S10 like `RR123456789XX`), ride PINs (`share PIN 4821 with your driver`). These entities *veto* promo-code candidates at the same span.

#### E-06 OTP detection (privacy critical)

Numeric (4–8 digits) or alphanumeric (`G-123456`) tokens near `otp.trigger` or `otp.warning`. Recall target ≥ 99.5 %. OTP entities keep only `{length}`. The notification is forced to `security`; content is scrubbed before persistence (CAP-008).

#### E-07 URL

Extract URLs and bare domains (`bit.ly/…`, `snapp.ir/…`). Highlight only; never fetch, never resolve.

#### E-08 Sender / conversation

Per-app `fieldMap` (Gmail: `sender=title`, `subject=text`, `snippet=big_text`, `account=sub_text`). For messaging apps use `ConversationTitle`/title as sender. Email account → Work/Personal mapping is a user setting (§11.2).

#### E-09 Phone and SMS sender numbers

E.164 and national formats (`+98 21 …`, `0912 …`). Pack `apps.json` MAY carry *regional hints* about SMS sender-number ranges used for ads or services. VERIFY any such hint against real, scrubbed corpora before shipping; do not invent number ranges.

### 8.8 Classification and decision

Tier 1 turns evidence into a category. Everything is additive in **log-odds units** so that rules, metadata, and app history combine predictably.

**Evidence** = `{ category, weight, ruleId, reason: { key, args }, span? }`. Weights may be negative. Evidence is grouped into **families** (`lexicon.promo`, `lexicon.work`, `entity`, `metadata`, `channel`, `app-prior`, `emoji`, `user`); each family has a cap (default ±3.0) so ten synonyms of "sale" cannot outvote a strong contrary signal.

| Signal source | Default contribution (initial values; calibrated by the offline tool, RUL-019) |
|---|---|
| `Notification.category == CATEGORY_PROMO` | offers +2.0 |
| `CATEGORY_RECOMMENDATION` | offers +1.0, social +0.4 |
| `CATEGORY_MESSAGE` | people +1.6 |
| `CATEGORY_EMAIL` | work +0.7, people +0.4 (refined by account mapping, §11.2) |
| `CATEGORY_SOCIAL` | social +1.6 |
| `CATEGORY_EVENT`, `CATEGORY_REMINDER` | work +0.6, updates +0.3 |
| `CATEGORY_MISSED_CALL` (or `missed_call` concept) | people +2.4 |
| `CATEGORY_STATUS`, `CATEGORY_SYSTEM`, `CATEGORY_ERROR` | updates +1.0 |
| Channel id/name matches `channel.promo` | offers +1.8 (similar for other `channel.*` concepts: messages→people, transactional→orders/money, security→security, updates→updates, social→social) |
| Importance is `MIN`/`LOW` and channel unknown | updates +0.2, offers +0.2 (mild) |
| Lexicon family hit | concept weight × term weight × position factor (title 1.0, text 0.9, big text 0.8, lines 0.7) |
| Entities | `promo_code` (conf ≥ 0.6) offers +2.4; percent/amount `discount` offers +1.2; `money` debit/credit money +1.6; `order_ref` orders +1.4; `datetime` role `due` work/money +0.3; `otp` security +6.0 |
| App prior | `λ · log((n_c + α)/(N + 9α))`, α = 1, λ = 0.6, clamped to ±1.5. Mixed-content apps (a marketplace that sends orders *and* offers) therefore never lock to one category. |
| User rule / app default set by the user | Overrides (below). |

**Decision**

1. `score[c] = Σ evidence weights` (after family caps).
2. `p = softmax(score)` across the nine categories.
3. Category = `argmax p`; ties broken by the priority order in §6.1. `confidence = p[top]`, `margin = p[top] − p[second]`.
4. `needs_review = confidence < 0.55 OR margin < 0.12` → category stays the best guess but the UI flags it; if `confidence < 0.35` the category is `other`.
5. Tags come from entities and concept hits (§6.2). Expiry-related tags are recomputed at read time.
6. **Hard overrides** (RUL-018) are applied last.

| ID | Requirement | Acceptance |
|---|---|---|
| RUL-001 | Tier 1 is **deterministic**: identical input, packs, and app-profile state produce identical output. | Golden-file tests. |
| RUL-002 | Budget: ≤ 20 ms p95 and ≤ 40 ms p99 per notification (≤ 1.2 KB text) on R1, including normalization. | On-device benchmark harness (M3). |
| RUL-003 | **Concept matching uses one Aho–Corasick automaton** built from all enabled packs (rebuilt on pack change). | Complexity test; multi-pack test. |
| RUL-004 | **Fuzzy pass:** single-word terms with length ≥ 5 tolerate edit distance 1 (≥ 10 letters: 2), using a deletion-index (SymSpell-style) or BK-tree built at load. Fuzzy hits carry ×0.7 weight and are marked in the trace. Multi-word terms are exact only. | `discunt`, `promocode`, `تخفیفف` match; short words never fuzzy-match. |
| RUL-005 | **Morphology:** `suffixSet` on a term allows the pack-declared suffixes (`تخفیف` → `تخفیف‌ها`, `تخفیفی`, `تخفیفتان`; `discount` → `discounts`, `discounted`). | Tests. |
| RUL-006 | **Regex safety:** every pack regex compiles with `RegexOptions.NonBacktracking` and a 20 ms timeout; total pack-rule budget per notification is 25 ms. If exceeded, abort remaining pack rules, set `needs_review`, and log locally. | Adversarial ReDoS-style tests. |
| RUL-007 | **Activation:** pattern rules list `requires` concepts; a rule runs only if its required concepts hit. | Perf test. |
| RUL-008 | Extractors and evidence are pure functions of `(RawNotification, NormalizedText, ConceptHits, AppProfile, UserRules, Packs)`. | Unit tests without Android. |
| RUL-009 | Family caps and the softmax mapping are configurable in `weights.json`. | Config test. |
| RUL-010 | Metadata evidence table above is implemented as data (`weights.json`), not inline constants. | Review. |
| RUL-011 | App prior updates at ingestion time from the *final* category (including user corrections) with recency decay (half-life 60 days). | Test. |
| RUL-012 | **User rules** (§11.1) are evaluated first; a matching rule can set category, add tags, mute, archive, or delete. User decisions are never overwritten by automation. | Test. |
| RUL-013 | Tags derive deterministically from entities/concepts (mapping table in `docs/`). | Test. |
| RUL-014 | **OTP hard override:** any OTP entity ⇒ category `security`, tag `verification-code`, promo candidates at that span discarded, content scrubbed (CAP-008). | Corpus test with zero tolerance for leaked OTPs. |
| RUL-015 | `needs_review` thresholds (above) are configurable. | Test. |
| RUL-016 | Conflict resolution follows the priority order only when scores are within `margin < 0.12`. | Test. |
| RUL-017 | Negative evidence is supported (e.g., `promo.code.negative` near a candidate; `work.meeting` inside an ad context lowers `work`). | Tests (`Meeting your goals with 30% off…` stays `offers`). |
| RUL-018 | Hard overrides, in order: (1) OS-redacted content ⇒ `security`; (2) OTP entity ⇒ `security`; (3) user rule; (4) user-set app default; (5) ignored Android categories were already filtered (CAP-005). | Tests. |
| RUL-019 | An offline **calibration tool** (`tools/calibrate`, .NET console) fits `weights.json` (evidence and family scales, softmax temperature) from a labelled corpus using regularized logistic regression, and reports macro-F1 and calibration error. Weights are committed; the app never trains Tier 1. | CI job runs calibration on the corpus and fails if F1 regresses > 1 point. |
| RUL-020 | **Explainability:** store the top ≤ 6 evidence items (`evidence_json`) as `{category, weight, reasonKey, args}`. The UI renders localized sentences such as *Matched "up to 70% off" · This app usually sends offers (86 % of 212)*. Fuzzy and emoji evidence is labelled as such. | UI test shows trace for a sample. |
| RUL-021 | Persist `rules_version` (engine + pack versions hash) per row (STO-005). | Test. |
| RUL-022 | Tier 1 never learns at runtime and never calls Tier 2/3. | Architecture test. |

### 8.9 Seed lexicons (starting point, not the finish line)

The tables below are **minimum seeds** for the first commit of each pack. The pack is not done until it meets the coverage targets in §8.11. `~` = optional separator. Persian entries are shown in normalized form. Every term needs test cases (R5).

#### 8.9.1 English seeds (`packs/en`)

| Concept | Seeds |
|---|---|
| `promo.code.trigger` | promo code, promo-code, coupon code, coupon, discount code, voucher code, voucher, gift code, redeem code, use code, with code, enter code, apply code |
| `promo.code.trigger.weak` | code |
| `promo.code.negative` | zip code, postal code, area code, country code, tracking code, tracking number, order code, order number, confirmation code, reference code, security code, verification code |
| `promo.code.referral` | referral code, invite code, refer a friend, your friend code |
| `promo.apply` | at checkout, in your cart, apply at checkout, enter at checkout, redeem now, valid on, applies to |
| `promo.discount.word` | discount, sale, off, deal, deals, savings, save, bargain, clearance, markdown, offer, special offer, price drop, mega sale, flash sale, doorbuster, member price |
| `promo.free_shipping` | free shipping, free delivery, ships free, shipping on us |
| `promo.bogo` | bogo, buy one get one, buy 1 get 1, 2 for 1, two for one |
| `promo.cashback` | cashback, cash back, money back, rewards, points back |
| `promo.credit` | credit, gift card, store credit, wallet credit, bonus |
| `promo.limited` | limited time, today only, last chance, final hours, while supplies last, don't miss, hurry, act now, ends soon, one day only, exclusive |
| `promo.urgency.time` | ends tonight, ends today, ends midnight, hours left, last day, expires soon, only today, closing soon |
| `promo.first_order` | first order, first purchase, new customers, new users, welcome offer |
| `promo.min_spend` | on orders over, orders above, minimum purchase, min. order, spend $, when you spend |
| `promo.cap` | up to, max discount, maximum discount, capped at |
| `promo.no_code` | no code needed, no code required, automatically applied, auto-applied |
| `promo.claim` | tap to claim, claim now, claim your, unlock your |
| `promo.event` | black friday, cyber monday, boxing day, prime day, back to school, summer sale, christmas sale, new year sale, ramadan, eid |
| `ad.marker` | ad, advertisement, sponsored, promoted, promotional |
| `ad.unsubscribe` | unsubscribe, opt out, manage preferences, stop receiving |
| `ad.cta` | shop now, buy now, order now, get it now, download the app, install now, sign up, book now |
| `otp.trigger` | verification code, security code, one-time password, one time password, otp, login code, sign-in code, passcode, your code is, code is |
| `otp.warning` | don't share, do not share, never share, will never ask you for, expires in 10 minutes |
| `security.login` | new login, new sign-in, sign-in attempt, logged in from, new device |
| `security.password` | reset your password, password changed, password reset |
| `money.debit` | debited, charged, purchase of, spent, withdrawn, paid, payment of, sent |
| `money.credit` | credited, deposit, received, refund, payment received, transferred to you |
| `money.balance` | balance, available balance, low balance |
| `money.bill` | bill due, payment due, due date, invoice, statement ready, overdue |
| `money.tax` | tax, tax return, vat, tax refund, filing deadline |
| `money.card` | card ending, card *, ending in, debit card, credit card |
| `work.meeting` | meeting, standup, stand-up, call at, invited you, invitation, starts in |
| `work.deadline` | due today, deadline, due tomorrow, overdue task |
| `work.review` | review requested, pull request, merge request, approved, requested changes |
| `work.mention` | mentioned you, replied to your, commented on |
| `work.assign` | assigned to you, assigned you |
| `work.build` | build failed, build succeeded, pipeline failed, deploy, deployment, incident, alert firing, on-call |
| `orders.status` | shipped, out for delivery, delivered, arriving, has been dispatched, order confirmed, ready for pickup |
| `orders.tracking` | tracking, track your package, track order |
| `orders.ride` | your driver, driver is arriving, ride, trip, pickup |
| `orders.pin` | pin, ride pin, share this pin |
| `orders.flight` | boarding, gate, flight, check-in |
| `social.reaction` | liked, loved, reacted to, commented, followed you, tagged you |
| `updates.app` | update available, new version, update now |
| `updates.system` | battery low, storage almost full, connected to, software update |
| `updates.weather` | forecast, rain, storm, weather alert, temperature |
| `updates.news` | breaking, breaking news, headline, top stories |
| `time.tonight` | tonight, this evening, by midnight, until midnight |

#### 8.9.2 Persian seeds (`packs/fa`)

| Concept | Seeds |
|---|---|
| `promo.code.trigger` | کد~تخفیف، کد هدیه، کد~کوپن، کوپن، کوپن~تخفیف، کد~ویژه، کد~اختصاصی، کد~پیشنهادی، با کد، با~استفاده~از~کد، کد را وارد کنید |
| `promo.code.trigger.weak` | کد |
| `promo.code.negative` | کد~پستی، کد~ملی، کد~رهگیری، کد~پیگیری، کد~سفارش، کد~پیش~شماره، کد~شعبه |
| `promo.code.referral` | کد~معرف، کد~دعوت، کد~معرفی، دوستان~را~دعوت~کنید |
| `promo.apply` | هنگام~پرداخت، در~سبد~خرید، در~مرحله~پرداخت، وارد~کنید |
| `promo.discount.word` | تخفیف، حراج، حراجی، فروش~ویژه، پیشنهاد~ویژه، پیشنهاد~شگفت~انگیز، شگفت~انگیز، جشنواره، نصف~قیمت، نیم~بها، قیمت~ویژه، کاهش~قیمت، ارزان~تر، جایزه، هدیه، سوپرآفر |
| `promo.free_shipping` | ارسال~رایگان، پست~رایگان، ارسال~مجانی |
| `promo.bogo` | یکی~بخر~یکی~ببر، 1+1 |
| `promo.cashback` | کش~بک، بازگشت~وجه، بازگشت~پول |
| `promo.credit` | اعتبار~هدیه، شارژ~هدیه، کیف~پول |
| `promo.limited` | فرصت~محدود، فقط~امروز، آخرین~فرصت، ساعات~پایانی، از~دست~ندهید، زودتر~از~همه، موجودی~محدود، تا~پایان~موجودی، همین~حالا |
| `promo.urgency.time` | تا~پایان~امشب، تا~پایان~امروز، امشب~آخرین~فرصت، ساعت~باقی~مانده، ساعت~دیگر |
| `promo.first_order` | اولین~سفارش، اولین~خرید، کاربران~جدید، مشتریان~جدید |
| `promo.min_spend` | برای~خرید~بالای، خرید~بالای، حداقل~خرید، حداقل~سفارش |
| `promo.cap` | تا~سقف، حداکثر~تا، حداکثر~تخفیف (the bare word `تا` is far too ambiguous; "up to" phrasing is handled by a pattern rule that requires a number and a discount word next to it) |
| `promo.no_code` | بدون~نیاز~به~کد، نیازی~به~کد~نیست، به~صورت~خودکار~اعمال |
| `promo.claim` | دریافت~هدیه، همین~حالا~دریافت~کنید، فعال~کنید |
| `promo.event` | یلدا، نوروز، بلک~فرایدی، جمعه~سیاه، عید~فطر، رمضان، مهرگان، بازگشت~به~مدرسه |
| `ad.marker` | تبلیغ، تبلیغات، حمایت~شده |
| `ad.unsubscribe` | لغو~عضویت، لغو~دریافت (plus a pattern rule for `لغو\s?\d{1,3}` at the end of an SMS-style ad; VERIFY the convention against real scrubbed corpora) |
| `ad.cta` | سفارش~دهید، همین~حالا~سفارش~دهید، خرید~کنید، ثبت~نام~کنید، دریافت~کنید، نصب~کنید |
| `otp.trigger` | کد~تایید، رمز~یک~بار~مصرف، رمز~یکبار~مصرف، رمز~پویا، رمز~دوم~پویا، کد~ورود، کد~فعال~سازی، کد~امنیتی، کد~احراز~هویت، رمز~ورود~موقت |
| `otp.warning` | این~کد~را~به~کسی~ندهید، کد~را~در~اختیار~دیگران~قرار~ندهید، به~هیچ~عنوان~در~اختیار |
| `security.login` | ورود~جدید، ورود~به~حساب، دستگاه~جدید |
| `security.password` | تغییر~رمز، بازیابی~رمز، رمز~عبور~شما~تغییر |
| `money.debit` | برداشت، خرید~از، پرداخت، کسر~شد، برداشت~شد |
| `money.credit` | واریز، واریز~شد، دریافت~وجه، بازگشت~وجه، افزایش~موجودی |
| `money.transfer` | انتقال~وجه، کارت~به~کارت، حواله، پایا، ساتنا |
| `money.balance` | مانده، مانده~حساب، موجودی |
| `money.bill` | قبض، سررسید، قسط، صورتحساب |
| `money.tax` | مالیات، اظهارنامه، ارزش~افزوده، مودیان |
| `money.card` | کارت، شماره~کارت، پایان~کارت |
| `work.meeting` | جلسه، دعوت~نامه، شروع~می~شود، ملاقات، وبینار |
| `work.deadline` | مهلت، ددلاین، مهلت~تحویل، سررسید |
| `work.review` | درخواست~ادغام، بازبینی، تایید~شد |
| `work.mention` | شما~را~منشن~کرد، به~شما~پاسخ~داد، نظر~داد |
| `work.assign` | به~شما~اختصاص~داده~شد، تسک~جدید |
| `work.build` | ساخت~ناموفق، استقرار، خطای~سرور، حادثه |
| `orders.status` | ارسال~شد، تحویل~پیک، تحویل~داده~شد، در~مسیر، در~حال~ارسال، سفارش~شما، مرسوله |
| `orders.tracking` | کد~رهگیری، شماره~رهگیری، پیگیری~مرسوله |
| `orders.ride` | راننده، سفر~شما، در~راه~است، مبدا |
| `orders.flight` | پرواز، بلیط، بوردینگ، گیت |
| `people.missed_call` | تماس~بی~پاسخ |
| `people.voice_message` | پیام~صوتی |
| `social.reaction` | لایک~کرد، دنبال~کرد، نظر~داد، شما~را~تگ~کرد |
| `updates.app` | به~روز~رسانی، نسخه~جدید |
| `updates.system` | باتری~ضعیف، حافظه~پر، شارژ~شد |
| `updates.weather` | آب~و~هوا، هشدار~جوی، بارش |
| `updates.news` | خبر~فوری، مهم~ترین~اخبار |
| `time.tonight` | امشب، امشب~تا~ساعت، تا~نیمه~شب، تا~پایان~شب |
| `time.today` / `time.tomorrow` | امروز، فردا، پس~فردا |

> Persian-specific phenomena that the tests MUST cover: ZWNJ vs space vs none (`می‌شود`/`میشود`/`می شود`); Arabic vs Persian `ي/ی` and `ك/ک`; Persian and Arabic-Indic digits; `٬` and `,` thousand separators; `٪`; `هزار`/`میلیون` scaling and `ک` shorthand; possessive and plural suffixes (`تخفیف‌هاتون`, `تخفیفتان`); formal vs colloquial phrasing (`می‌خواهید`/`می‌خوای`, `بزن بریم`); brand names in Latin inside Persian sentences; RTL marks glued to Latin tokens.

#### 8.9.3 Extension pack: Finglish (`packs/fa-latn`, experimental)

Persian written in Latin letters is common in SMS and some app notifications. Seeds (all folded, `~` optional separator; many spellings via fuzzy match): `kod~takhfif`, `kod~hediye`, `takhfif`, `ersal~rayegan`, `rayegan`, `foroosh~vizhe`, `forush~vizhe`, `ta~payan~emshab`, `ta~emshab`, `fori`, `variz`, `bardasht`, `mojoodi`, `kart~be~kart`, `sefaresh`, `code~takhfif`. Finglish overlaps English words (`code`, `sale`); this pack MUST NOT reduce English precision: it activates only when at least one Finglish-only term hits.

### 8.10 Worked examples (become the first tests)

| # | Input (title / text) | Expected |
|---|---|---|
| 1 | `Spring Sale 🌸` / `Extra 25% off with code SPRING25 · ends tonight at midnight` (channel *Promotions*) | offers · tags `promo-code, discount, expiring` · code `SPRING25` (25 % off) · expiry 24:00 local of `posted_at` day |
| 2 | `Bank` / `Your verification code is 482913. Don't share it.` | **security** · tag `verification-code` · `otp{length:6}` · body **not stored** · no promo code |
| 3 | `Deal` / `Use code 482913 at checkout for 10% off` | offers · numeric promo code accepted (strong anchor + offer context, no `otp.*`) |
| 4 | `Your order has shipped` / `Order #A1234 · Track: 1Z999AA10123456784` | orders · tags `delivery` · `order_ref` order + tracking · **no** promo code |
| 5 | Slack: `Sarah` / `mentioned you in #design-review` | work · tag `mention` |
| 6 | `Card alert` / `$45.00 charged to card ending 4417 at STARBUCKS` | money · `money{45.00 USD, debit, account_hint 4417}` |
| 7 | `Your driver is arriving` / `Share PIN 4821 with your driver` | orders · tags `ride, ride-pin` · **no** `otp`, **no** promo code |
| 8 | `هدیه اولین سفارش` / `کد تخفیف ۳۰٪ اولین سفارش شما: SNAPP30 — تا پایان امشب فرصت دارید` | offers · `SNAPP30` 30 % · `first_order` · expiry 23:59:59 local |
| 9 | `ارسال رایگان` / `ارسال رایگان برای خرید بالای ۵۰۰ هزار تومان` | offers · tag `free-shipping` · `min_spend 500 000 IRT` · **no** code |
| 10 | `کد ورود` / `کد ورود شما: ۴۵۸۲۱۳ — این کد را به کسی ندهید` | security · `otp{length:6}` · body not stored |
| 11 | `بانک` / `مبلغ ۲٬۵۰۰٬۰۰۰ ریال از حساب شما برداشت شد` | money · debit · 2 500 000 IRR (= 250 000 IRT) |
| 12 | `دعوت دوستان` / `کد معرف شما: ALI123 — دوستانتان را دعوت کنید و ۵۰ هزار تومان هدیه بگیرید` | offers · `ALI123` kind `referral` · credit 50 000 IRT |
| 13 | `ثبت آدرس` / `کد پستی شما ۱۲۳۴۵۶۷۸۹۰ ثبت شد` | **no** promo code · low confidence → `needs_review` |
| 14 | `یادآوری` / `جلسه تیم ساعت ۱۰:۳۰ شروع می‌شود` | work · tag `meeting` · `datetime{start, 10:30 local}` |
| 15 | `Fit` / `Meeting your fitness goals is easier with 30% off — code FIT30` | **offers** (not work) · `FIT30` · negative evidence lowers `work.meeting` |
| 16 | `سبد خرید` / `کد SAVE20 را در سبد خرید وارد کنید` | offers · `SAVE20` (mixed script, RTL context) |

### 8.11 Quality bar, corpus, and contribution workflow

| ID | Requirement | Acceptance |
|---|---|---|
| QLT-001 | **Corpus:** `tests/corpus/<lang>/*.yaml` of labelled notifications (input + expected category/tags/entities). v1.0 minimum: **600 EN, 600 FA**, balanced across categories, including **≥ 150 hard negatives** and **≥ 100 adversarial** cases (obfuscation, mixed scripts, punctuation glue, RTL marks, emoji-only, empty title, 5 KB text, truncated text, InboxStyle lines, MessagingStyle threads). | `alak-pack coverage` report in CI. |
| QLT-002 | **Sources:** hand-written, synthetic (templated from real-world *patterns*), and donated real notifications only after **scrubbing** (names, numbers ≥ 5 digits, emails, URLs replaced). A scrubber tool ships in the repo. No personal data in the repo, ever. | Scrubber test; CI secret/PII scan. |
| QLT-003 | **Metrics on a held-out split:** category macro-F1 ≥ **0.92 (EN)** and ≥ **0.90 (FA)**; promo-code precision ≥ **0.98**, recall ≥ **0.92**; **OTP recall ≥ 0.995** and *zero* stored OTPs in the corpus run; expiry accuracy ≥ 0.90 where an expiry exists. | CI gate. |
| QLT-004 | **Coverage targets per pack (v1.0):** ≥ 40 terms for each of `promo.discount.word`, `promo.limited`, `ad.cta`, `otp.trigger`, `money.*`, `orders.status`; ≥ 15 for all other filled concepts; every term has a test that would fail if the term were removed. | Coverage tool. |
| QLT-005 | **Regression policy:** a bug report becomes a corpus case *before* the fix. | PR template. |
| PCK-008 | **"Copy as test case"** in the notification detail menu produces a scrubbed YAML test (editable before sharing) using the share sheet or clipboard. This is how users contribute corner cases without any network in the app. | UI test. |
| PCK-009 | `alak-pack` CLI commands: `new <id>`, `validate`, `test [--corpus]`, `coverage`, `lint` (style, duplicates, conflicts), `bundle` (→ `.alakpack` + SHA-256). | CLI tests; documented in `docs/contributing/packs.md`. |
| PCK-010 | Contribution docs include a "how to add a language in one afternoon" walkthrough using a tiny sample language. | Doc exists; reviewed by a non-author. |

---

## 9. Tier 2 (adaptive classifier) and Tier 3 (small language model)

### 9.1 Tier 2 — on-device adaptive classifier

**Purpose:** personalize to the user without any cloud and without a heavy model. It learns from corrections and from high-confidence Tier 1 results, and it is cheap enough to run on every notification.

**Design**

- **Features** (all hashed into a fixed space, signed hashing trick): word unigrams and bigrams from the `Folded` text; character 3–5-grams inside tokens (helps Persian morphology and Finglish); package id; channel id; Android category; the set of Tier 1 concept hits and entity types; emoji signal classes; script flags; a coarse length bucket.
- **Model:** multinomial logistic regression (softmax), `D = 2^16` feature slots × `C = 9` categories, `float32` (~2.4 MB), trained online with AdaGrad and light L2.
- **Training data:** (a) **user corrections** (weight ×8); (b) **pseudo-labels** from Tier 1 results with confidence ≥ 0.90 (weight ×1, capped to 30 % of updates so they cannot drown corrections); (c) a **seed model** trained offline on the open corpus and shipped with the app.
- **Blending:** `logit_final = logit_tier1 + β · logit_tier2`, where `β = 0.3` with only the seed model and rises linearly to `0.8` after 50 corrections. Tier 2 can never override a hard override (RUL-018) and can never move an item *out of* `security`.
- **Privacy:** the `corrections` table stores **hashed feature indices**, not text (§6.6). "Reset personalization" deletes the personal weights and the corrections.
- **Storage:** `adaptive-v1.bin` in app-private storage: magic, version, `D`, `C`, weights, checksum; atomic write (write temp + rename).

| ID | Requirement | Acceptance |
|---|---|---|
| ADP-001 | Inference ≤ 2 ms and one online update ≤ 3 ms on R1. | Benchmark. |
| ADP-002 | Model file ≤ 4 MB; loading ≤ 50 ms. | Test. |
| ADP-003 | Deterministic: same sequence of updates from the same seed yields the same weights. | Test. |
| ADP-004 | After a correction, the same *kind* of notification (same sender/channel or shared n-grams) is classified as corrected in ≥ 90 % of simulated repeats. | "Personalization benchmark" test: simulate 50 corrections on the corpus; report F1 gain. |
| ADP-005 | The corrected notification itself always keeps the user's category (`category_source = user`). | Test. |
| ADP-006 | A user setting disables Tier 2 entirely (feature flag ARC-007). | Test. |
| ADP-007 | Export/import of personalization is part of the encrypted backup (§11.5). | Test. |
| ADP-008 | Evidence trace shows a Tier 2 item as *Learned from your corrections* (never a mystery score). | UI test. |

### 9.2 Tier 3 — small language model (SLM), user-triggered only

**Rules that never bend**

- Runs **only** after an explicit tap on a button. No schedules, no background work, no automatic runs (D-08).
- Uses a model the **user** imported (D-03). If none is installed, the buttons explain how to add one instead of failing.
- Never replaces the user's category. It produces **suggestions** the user can accept.
- Never leaves the device. There is no network permission to leak with.

**Bounded tasks** (this is not a chat feature; §2.2)

| Task | Trigger | Input assembled by the app | Output (validated JSON) |
|---|---|---|---|
| `T3-01 explain` | "Explain" on a notification | Title/text (≤ 1 200 chars), Tier 1 category, entities, language hint | `{ "summary": string ≤ 25 words, "importance": "low"\|"normal"\|"high", "action": string\|null, "category_suggestion": enum\|null, "tags": string[≤4], "facts": [{ "label", "value" }]≤5 }` |
| `T3-02 review` | "Review uncertain" in Inbox | Up to 20 `needs_review` items, each ≤ 300 chars | `{ "items": [{ "id": int, "category": enum, "confidence": "low"\|"medium"\|"high" }] }` |
| `T3-03 digest` | "Summarize my day" in Digest | Top items selected by rules (money, work, orders, people, expiring codes; never OTP content), trimmed to ≤ 1 500 tokens | `{ "headline": string, "sections": [{ "category": enum, "bullets": string[≤5 each ≤ 20 words] }], "follow_ups": string[≤3] }` |
| `T3-04 code_details` | "Fill in details" on a wallet code with gaps | The source notification text | `{ "min_spend": …\|null, "cap": …\|null, "valid_until": ISO\|null, "conditions": string[≤4] }` |

| ID | Requirement | Acceptance |
|---|---|---|
| SLM-001 | Each task has a **JSON Schema** and, where the engine supports it, a **precompiled GBNF grammar** in `assets/grammars/`. Without grammar support, use a JSON-only prompt plus a tolerant parser (strip fences, extract the first balanced object, repair trailing commas). | Schema tests; malformed-output fuzz tests. |
| SLM-002 | Validate every output against its schema. On failure, retry **once** with a shorter, stricter prompt. If it still fails, show a plain error with a retry option; never show raw model text as if it were a result. | Test with a fake engine that emits garbage. |
| SLM-003 | **Grounding guard:** any promo code, amount, date, or URL in a model output MUST appear verbatim (after normalization) in the source text; otherwise drop that field. | Test with a hallucinating fake engine. |
| SLM-004 | **OTP safety:** notifications in `security` or with `otp`/`redacted` are excluded from every SLM input. | Test. |
| SLM-005 | Prompts use the model's own chat template (from GGUF metadata, or the catalog override). Prompts are versioned and stored in `assets/prompts/<task>.<version>.md` with English and Persian few-shot examples (2 each, short). | Prompt snapshot tests. |
| SLM-006 | Default sampling: temperature 0.2, top-p 0.9, repeat penalty 1.05, max tokens per task (explain 200, review 400, digest 500, code_details 200). Default context 2 048 tokens (3 072 if RAM allows). | Config test. |
| SLM-007 | Results are cached in `slm_results` keyed by `(task, model, input_hash)`; repeated taps reuse the cache unless the user chooses "Run again". | Test. |
| SLM-008 | UI shows a progress state with a **Cancel** button; cancellation must stop native generation within 500 ms. | Test with the fake engine and a native smoke test. |
| SLM-009 | Output language: the UI language by default; a setting offers "same as the notification". | Prompt test. |
| SLM-010 | A per-model **language quality label** is shown ("Works best in English; Persian varies"), coming from catalog metadata and the on-device benchmark, never invented at runtime. | UI test. |

**Resource guard** (checked before loading and before each run)

| ID | Rule |
|---|---|
| SLM-020 | Refuse to load when `availMem < modelFileSize × 1.15 + 350 MB` (ActivityManager memory info) and show a plain message naming a smaller model tier. |
| SLM-021 | Refuse or pause when thermal status ≥ *Severe*. Warn when battery saver is on or battery < 15 % and not charging. |
| SLM-022 | Generation runs off the main thread with normal-or-lower priority. If the app goes to the background, pause; cancel after 60 s. (No foreground service; that would need a permanent notification.) |
| SLM-023 | Unload the model after 60 s of inactivity (setting; default on) to free RAM. Prefer `mmap` for model files. |
| SLM-024 | A "Performance" setting (Gentle / Balanced / Fast) maps to thread count (2 / 4 / 6 capped by cores) and batch size. |

### 9.3 Model manager

The app **never downloads** models (D-03). The user obtains a model file elsewhere and imports it.

| ID | Requirement | Acceptance |
|---|---|---|
| MOD-001 | **Import** with the Android file picker (Storage Access Framework, no storage permission). Copy into app-private storage (`files/models/<sha256>.<ext>`) with progress, cancel, and a free-space pre-check (need ≥ file size + 200 MB). *Optimization to VERIFY:* opening through a file-descriptor path to avoid the copy. | Import test with a 1 GB file; cancellation leaves no partial file. |
| MOD-002 | **Probe:** identify the format by magic bytes and extension (`GGUF` header for `.gguf`); parse GGUF metadata in managed code (architecture, parameter count, quantization type, context length, chat-template presence, vocabulary size). Provide the same for other formats through the engine's `ProbeAsync`. | Unit tests with tiny fixture files. |
| MOD-003 | **Integrity:** compute SHA-256 while copying. If the hash matches a catalog entry show *Verified against the catalog*; otherwise show *Unknown file* and the hash, and allow use. | Test. |
| MOD-004 | **Compatibility:** the engine reports support for the model's architecture and quantization. Unsupported → a specific message ("This engine build does not support architecture X") and a link to catalog guidance. | Test with a fixture. |
| MOD-005 | **RAM-aware recommendations** from `totalMem`: < 3.5 GB → ≤ 0.6 B parameters; 3.5–5.5 GB → ≤ 1.7 B; 5.5–8 GB → ≤ 4 B; > 8 GB → ≤ 8 B (4-bit quantization). Models above the tier are shown as *Too large for this phone*. | UI test with mocked memory. |
| MOD-006 | **Speed test:** a fixed EN and FA prompt measures load time, time-to-first-token, tokens/second and peak PSS, stores them in `benchmark_json`, and labels the model *Good / Usable / Slow* against thresholds (§14). | Test with the fake engine. |
| MOD-007 | **Catalog** (`catalog/models.json`, CC0): curated entries with `id, name, family, params_b, quant, format, engine, size_bytes, sha256, license, license_url, source_url, min_ram_gb, context_length, chat_template, languages{en,fa}, notes, verified_on[]`. The Models screen shows *Open in browser* for `source_url` (an Android view intent handled by the browser, so the app itself needs no network permission). | Catalog validator in CI. |
| MOD-008 | **Only verified entries** may be in the catalog (R1): the agent must open each model page and record license, file name, size and SHA-256. Families likely worth evaluating (all unverified until checked): Qwen2.5 0.5B/1.5B Instruct, Qwen3 0.6B/1.7B, Gemma 3 270M/1B, Llama 3.2 1B Instruct, SmolLM2 360M/1.7B. Watch each model's license terms; the app never bundles or redistributes weights. | Review checklist. |
| MOD-009 | Delete, rename, set active, and view storage used. Only one model is loaded at a time. | UI test. |
| MOD-010 | Users may import **several** models and switch; the engine is chosen by model format (`gguf` → llama.cpp; `litertlm`/`task` → LiteRT-LM when that engine is installed). | Test with two fake engines. |

### 9.4 AI runtime decision gate (ADR-0002)

The maintainer has not chosen between **llama.cpp** and **Google's official runtime**. This section records what is known, defines the abstraction so the choice is reversible, and sets an evidence-based gate.

**What the research found (as of 2026-09; VERIFY again at M8)**

- **"Google's official runtime" now means LiteRT-LM.** The older MediaPipe LLM Inference API for Android/iOS is officially **deprecated / maintenance-only**; Google's docs tell developers to migrate to LiteRT-LM. **Do not build on MediaPipe LLM Inference.**
- **LiteRT-LM** ships an Android Maven artifact (`com.google.ai.edge.litertlm:litertlm-android`), uses `.litertlm` model bundles, and is described as stable for Kotlin, Python and C++ (Swift/JS in preview). Pre-converted models are published under the *LiteRT Community* organization on Hugging Face (for example a Qwen2.5-1.5B-Instruct variant under Apache-2.0). The older MediaPipe docs described the API as optimized for high-end phones (Pixel 8 / Galaxy S23 and newer); whether LiteRT-LM is better on low-end hardware is unproven for this project.
- **llama.cpp** documents a supported Android NDK build (arm64-v8a, `android-28`), optional KleidiAI acceleration, and runtime CPU-feature detection so one binary works on older and newer devices. Its model format is **GGUF**, by far the widest set of downloadable models on Hugging Face.
- **LLamaSharp** (the .NET wrapper for llama.cpp) is mature on desktop, but Android/.NET MAUI support was not established at spec time (a community pull request for a MAUI Android sample existed). Treat it as something to *evaluate*, not to assume.
- **.NET integration paths:** for llama.cpp, P/Invoke to a thin C shim (recommended) or LLamaSharp if the spike proves it; for LiteRT-LM, a small Kotlin/Java wrapper AAR bound through a .NET Android binding library (direct binding of Kotlin suspend/Flow APIs is awkward), or a C shim over the C++ API if the build story allows.

**Decision matrix**

| Criterion | llama.cpp | LiteRT-LM |
|---|---|---|
| User can pick "any" Hugging Face model | **Strong** (GGUF is the common format) | Narrower (needs `.litertlm`/`.task` conversions from the LiteRT Community or your own conversion) |
| Works on low-end CPUs | Documented, portable build | Unproven here; test on R1 |
| GPU/NPU acceleration | Experimental (OpenCL/Vulkan builds) | **Stronger story** via LiteRT accelerators; device-dependent |
| Structured JSON output | **Grammar-constrained decoding (GBNF)** | Unknown; assume prompt-only until verified |
| .NET integration effort | Own C shim + NDK build (well documented) | Kotlin wrapper AAR + binding library (medium) |
| Official support | Community-maintained, extremely active | **Google-maintained**, pre-1.0 versioning at spec time |
| APK size impact | Small `.so` (measure) | Larger AAR (measure) |
| Licenses | MIT | Apache-2.0 (verify) |

**Default (D-04): llama.cpp first, behind `IInferenceEngine`.** Rationale: it best matches "the user picks a ready-made Hugging Face model", low-end CPUs, and grammar-constrained JSON. LiteRT-LM becomes a **second engine plugin** if the spike shows a clear win on R1 or on GPU-capable phones.

**Spike protocol (M8) — the agent executes this and records numbers in `docs/adr/0002-inference-runtime.md`:**

1. Build the C shim for `arm64-v8a` with the NDK; confirm 16 KB page alignment (PF-07). Pin the llama.cpp commit in `native/llama-shim/LLAMA_CPP_VERSION`.
2. On **R1** and one mid-range phone, load a Qwen2.5 0.5B and 1.5B Instruct Q4 GGUF. Record: cold/warm load time; time-to-first-token; decode tokens/second (128 tokens); peak PSS; battery drain per 100 generated tokens (`dumpsys batterystats`); thermal behaviour after 5 consecutive runs; APK size delta; crash count over 200 generations.
3. Run **10 golden prompts** (5 EN, 5 FA) for `T3-01`, 2 for `T3-03`. Record JSON-validity rate (target ≥ 95 % with grammar, ≥ 85 % without) and grounding-guard pass rate (SLM-003).
4. Repeat with LiteRT-LM using `.litertlm` builds of the same model families, time-boxing the .NET integration to three working days. If it cannot be wrapped in that time, record why and stop.
5. **Pass thresholds for an engine to be primary:** on R1 with ≤ 1.5 B Q4 → ≥ 3 tokens/s decode, load ≤ 15 s, no out-of-memory, JSON validity as above, and an integration the maintainers can support.
6. If both pass, choose the one with broader model choice (expected: llama.cpp). If neither passes on R1, gate Tier 3 behind a RAM check (≥ 4 GB) and keep everything else unchanged.

### 9.5 Engine abstraction

```csharp
public interface IInferenceEngine : IAsyncDisposable
{
    string EngineId { get; }                                   // "llamacpp", "litertlm"
    IReadOnlyCollection<string> SupportedFormats { get; }      // "gguf", "litertlm"
    EngineCapabilities Capabilities { get; }

    Task<ModelProbeResult> ProbeAsync(ModelFileRef file, CancellationToken ct);
    Task LoadAsync(ModelFileRef file, ModelLoadOptions options,
                   IProgress<double>? progress, CancellationToken ct);
    IAsyncEnumerable<GenerationChunk> GenerateAsync(GenerationRequest request, CancellationToken ct);
    Task UnloadAsync();
    EngineResourceSnapshot GetResourceSnapshot();              // PSS, model bytes, threads
}

public sealed record EngineCapabilities(
    bool SupportsGrammar, bool SupportsJsonSchema, bool SupportsGpu,
    bool ExposesChatTemplate, int MaxContext);

public sealed record GenerationRequest(
    IReadOnlyList<ChatMessage> Messages, int MaxTokens, float Temperature, float TopP,
    IReadOnlyList<string>? Stop, ResponseFormat Format);

public abstract record ResponseFormat
{
    public sealed record Text : ResponseFormat;
    public sealed record Grammar(string Gbnf) : ResponseFormat;
    public sealed record JsonSchema(string Schema) : ResponseFormat;
}
```

| ID | Requirement | Acceptance |
|---|---|---|
| ENG-001 | All Tier 3 code depends only on `IInferenceEngine`. A **fake engine** (scripted outputs, latency, failures) lives in the abstractions project and powers all unit tests. | Tests run with no native code. |
| ENG-002 | Engines are registered by format. Loading the wrong format fails at `ProbeAsync`, not deep in native code. | Test. |
| ENG-003 | Native calls happen on a dedicated worker thread; the managed API is async; cancellation propagates to a native abort flag polled between tokens. | Native smoke test. |
| ENG-004 | **llama.cpp shim contract** (C): `alak_load(path, params)`, `alak_generate(handle, request_json, on_token, user_data)`, `alak_cancel(handle)`, `alak_free(handle)`, `alak_system_info()`. Keep the surface tiny so upstream API churn stays inside the shim. | Header review; ABI test. |
| ENG-005 | Build flags follow the upstream Android docs: `-DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-28 -DGGML_NATIVE=OFF -DGGML_OPENMP=OFF -DGGML_LLAMAFILE=OFF -DLLAMA_OPENSSL=OFF` (optionally `-DGGML_CPU_KLEIDIAI=ON`). Ship `arm64-v8a` only for v1 (x86_64 optional for emulator development). | CI builds the shim. |
| ENG-006 | Upgrading llama.cpp requires re-running the golden-prompt suite and updating `LLAMA_CPP_VERSION` in the same PR. | PR template. |

---

## 10. Codes wallet and reminders

The wallet is a derived view over `promo_codes` (§6.6), not a separate capture path. Every code shown here traces back to a notification.

### 10.1 Behaviour

| ID | Requirement | Acceptance |
|---|---|---|
| WAL-001 | A new `promo_code` entity with confidence ≥ 0.60 (E-01) upserts a `promo_codes` row keyed on `(code_norm, package)`. A repeat sighting (CAP-006) updates `last_seen_at`, `times_seen`, and extends `valid_until` only if the new sighting's expiry is later and explicit. | Test: three re-sends of the same code → one row, `times_seen = 3`. |
| WAL-002 | **Status** is `active` until: the user marks it `used` or `dismissed`; `valid_until` passes (→ `expired`, checked lazily on read and by the daily prune job, STO-002); or a later notification says the code expired/ended (E-01 "Updates" family) (→ `expired`). | Tests per transition. |
| WAL-003 | **Sections in the Codes screen:** *Expiring soon* (valid within 48 h, soonest first), *Active*, *Used*, *Expired* (collapsed by default). Within a section, newest `last_seen_at` first. | UI test. |
| WAL-004 | **Copy to clipboard** copies `code` (original case) and marks the clipboard entry sensitive where the platform supports it (PF-08), so the OS does not preview it. Copying does **not** change status; a separate "Mark used" action does. | Test. |
| WAL-005 | A **possible code** (confidence 0.40–0.60, §8.7 E-01) appears in a small *Maybe a code?* row with **Keep** / **Not a code** actions. Keep creates the wallet row and a `corrections`-style positive signal for Tier 2's shape features (not text); Not a code discards it and never resurfaces that exact string for that app. | Test both paths. |
| WAL-006 | **Reminders:** for a code with a known `valid_until`, offer a local reminder at "24 hours before", "on expiry morning" (09:00 local), or a custom offset, scheduled with `AlarmManager.setAndAllowWhileIdle` (inexact; no `SCHEDULE_EXACT_ALARM` permission — a promo reminder does not need to-the-second precision). Tapping the reminder opens the code. | Test with a fake alarm scheduler. |
| WAL-007 | Editing a code (store name, expiry, note) is always possible; edits set `category_source`-equivalent protection so automation never overwrites a user edit. | Test. |
| WAL-008 | **Search and filter** the wallet by store, status, and "has expiry". | UI test. |
| WAL-009 | **Gaps:** when `discount`, `valid_until`, or conditions are missing, the code card shows a subtle "Fill in details" action that runs `T3-04` (§9.2) if an SLM is installed, or simply lets the user type them in if not. | UI test with and without a model. |
| WAL-010 | Deleting a code deletes the wallet row only; the source notification (if still retained) is untouched. | Test. |

### 10.2 Merge and store-name resolution

`store_name` defaults to the app label (CAP-013). If the pack's `apps.json` gives a nicer known name, or the user renames it once, that name is remembered per package (`apps.default_category`-style override, stored in `apps` table via a `display_name` the agent should add to that table in the same migration as this section) and reused for future codes from that app.

---

## 11. User rules and personalization

### 11.1 User rules (`user_rules`)

A rule is: **match** (conditions, all must hold) → **action**. Rules run before Tier 1's own decision (RUL-012) and their result is authoritative (`category_source = user`, never overwritten).

```json
{
  "match": {
    "package": "com.google.android.gm",
    "titleContains": null,
    "textContains": null,
    "senderContains": null,
    "channelId": null,
    "accountEmail": "work@company.com"
  },
  "action": {
    "setCategory": "work",
    "addTags": [],
    "mute": false,
    "archiveImmediately": false,
    "captureDepth": null
  }
}
```

| ID | Requirement | Acceptance |
|---|---|---|
| PER-001 | Rules are created from the notification detail screen ("Always sort mail from this address as Work") or from Settings → Rules (full editor: package picker, text-contains conditions, action). | UI test both entry points. |
| PER-002 | Rule evaluation order: `priority` ascending, first match wins; ties broken by rule id (creation order). | Test. |
| PER-003 | A rule with **no** action fields set besides `mute: true` silently drops matching notifications before persistence (never stored, never counted) — used for muting a noisy app or a specific promo channel without a full app-level mute. | Test. |
| PER-004 | Rules are exportable/importable as JSON (part of backup, §11.5, and shareable on their own — useful for "here's my rule for Bank X" tips between users, copied by hand since there is no in-app sharing channel). | Test round-trip. |

### 11.2 Account / sender mapping (email and multi-account apps)

For apps that carry multiple identities in one package (Gmail, Outlook, Telegram business vs personal), `apps.json`'s `fieldMap.account` (E-08) extracts the account string. The **Sources** screen lists distinct `(package, account)` pairs seen and lets the user assign each a category default and a display label ("Work Gmail" vs "Personal Gmail"). This assignment is stored as a `user_rules` row with `match.accountEmail` and is the primary mechanism behind US-03 for professionals whose one email app mixes both worlds.

| ID | Requirement | Acceptance |
|---|---|---|
| PER-010 | The Sources screen groups notifications by `(package, account\|channel)` in the last 30 days, showing count and current default, sorted by volume. | UI test. |
| PER-011 | Assigning a default from Sources creates or updates exactly one `user_rules` row; it does not retroactively re-categorize old notifications unless the user taps "Apply to existing" (bounded to the last 500 matching rows, run as a background batch). | Test. |

### 11.3 App-level settings

Per app: capture depth override (§6.4), mute, auto-archive-on-read, default category override, retention override, "always ignore promo/possible-code detection" (for apps that only ever produce false positives for a given user).

### 11.4 Global settings surface (data model only here; UI in §13)

`settings` table holds: theme (system/light/dark), UI language (system/en/fa), calendar (auto/gregorian/jalali, D-09), digits (auto/latin/persian), retention overrides per category, global size cap (STO-003), Tier 2 on/off, SLM performance profile (SLM-024), app-lock (biometric/PIN) on/off, "hide amounts in lists" (money), notification-for-reminders on/off.

### 11.5 Backup and restore

| ID | Requirement | Acceptance |
|---|---|---|
| BAK-001 | "Back up now" writes a single encrypted file (`.alakbackup`): schema version, all tables except `slm_results` (regenerable) and cached icons, encrypted with a key derived (Argon2id) from a passphrase the user enters at export time. Saved via the Android file picker (Storage Access Framework) — the user chooses where; the app does not manage cloud accounts. | Round-trip test: export, wipe app data, import, verify row counts and a sample of content match. |
| BAK-002 | Restore validates schema version and offers "merge" (default, skips rows whose `uid` already exists) or "replace all". | Test both modes. |
| BAK-003 | The passphrase is never stored. A lost passphrase means a lost backup; the UI says this plainly before export. | Copy review against §12's writing guidance. |

---

## 12. Design system

### 12.1 Design plan (the brief, worked through before building)

**Subject matter.** Alak is a sieve for a torrent of push notifications: a private, on-device clerk that sorts mail from a desk full of paper into labelled trays, and keeps a small ticket stub for every coupon so it never gets lost in the shuffle. The product's own name is the metaphor — الک, a sieve — so the design should feel like *sifted, settled paper*, not like a SaaS dashboard. It is used in short bursts throughout the day (checking a code before checkout, glancing at what came in), by two audiences at once: someone who wants calm and speed, and a privacy-minded, technical user who wants to see the machinery (evidence, rules, an open corpus) when they go looking for it.

**Rejecting the generic defaults for this brief.** Run the same brief through a generic pass and it lands on: a warm cream/terracotta SaaS aesthetic with rounded cards, a hairline-and-grid "developer tool" look, or a loud neon-on-black "AI product" look. All three are wrong for a *sieve*: SaaS-card kit implies a dashboard product, not a quiet daily-use utility; the terracotta accent is explicitly called out as an AI tell; the neon-on-black look fights the "calm" principle head-on. What is specific to this brief and not a default: (a) the **ticket/stub shape** for a code, borrowed directly from a physical coupon stub, torn edge and all — not a rounded card; (b) a **paper-and-ink** palette (grey-white "salt" paper, not cream) that reads as sorted mail rather than a marketing surface; (c) **category as color-coded tabs/spines**, like index-card dividers, giving the categorization itself a physical form instead of hiding it behind a dropdown; (d) bilingual type handled as a first-class constraint (a Persian display face carrying real personality, not a fallback), since most privacy/utility apps treat RTL as an afterthought.

**Color.** Base tokens (light):
- `salt` `#F3F5F6` — page background. A cool, slightly grey white — "sorted paper", not cream.
- `paper` `#FFFFFF` — raised surfaces (cards, the Stub, sheets).
- `ink` `#12252B` — primary text and icons. A near-black with a green-blue cast, not pure black (avoids the "tinted near-black is a tell" trap by being a deliberate, slightly desaturated color rather than `#0B0B0B`).
- `ink-2` `#4A5D64` — secondary text.
- `hairline` `#DCE2E5` — dividers, the tear-edge of the Stub.

Base tokens (dark) — not an inversion, a genuinely different surface (a desk lamp at night, not paper under fluorescent light):
- `night` `#0C1417` — page background.
- `surface` `#131E22` — raised surfaces.
- `raised` `#1B2A2F` — the Stub, sheets, elevated dialogs.
- `txt` `#E7EEF0` — primary text.
- `txt-2` `#A3B5BB` — secondary text.

**Category accents** are the one place the palette gets loud, and only there — nine hues, one per category (§12.2), used as small marks (spines, dots, icon tints) rather than large fills, so the calm base is never overwhelmed.

**Type.** Two families:
- **Vazirmatn** for all UI text (Persian and Latin both — it has a genuinely complete, humanist Latin companion, so switching UI language never switches "voice"). Weights: Medium for headings, Regular for body, SemiBold sparingly for emphasis (never bold-and-color together, per the anti-tell list).
- **JetBrains Mono** for one thing only: the code string itself on the Stub, receipts, and any raw value (amounts, dates) where exactness matters. This is a functional choice (codes must be unambiguous — `0` vs `O`, `1` vs `l`) not a decorative "tech" signal, and it appears nowhere else.

**Layout.** Single-column, generously spaced list as the primary structure (a stack of settled paper), not a card grid. Left-aligned in LTR, right-aligned in RTL (never centered body text — centering reads as marketing, and this is a utility). The Stub is the one deliberately shaped object breaking the grid, positioned as if physically tucked into the list.

```
┌──────────────────────────────┐        ┌──────────────────────────────┐
│  Today                       │        │  ⚑ Offers            9:41   │
│ ┌───────────────────────────┐│        │  Snapp                       │
│ │ ┃ Snapp          9:41    ││        │  25% off · code SPRING25     │
│ │ ┃ 25% off with SPRING25  ││   OR   │  ┌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌┐   │
│ │ ┃ ends tonight     ⚑    ││        │  ╎ SPRING25         25%  ╎   │
│ └───────────────────────────┘│        │  ╎ ends tonight ⏰       ╎   │
│ ┌───────────────────────────┐│        │  └╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌┘   │
│ │ ┃ Bank            9:22   ││        │  Why: matched "25% off",    │
│ │ ┃ ● security (metadata)  ││        │  this app usually sends     │
│ └───────────────────────────┘│        │  offers                     │
└──────────────────────────────┘        └──────────────────────────────┘
   List: colored spine (┃) = category        Detail: full Stub inline,
   marks each row, left-aligned, RTL           evidence trace at the
   mirrors to the right.                       bottom (never hidden).
```

**Principles.**
1. **Category has a physical form.** A thin colored spine on every row and a matching tinted tab in navigation — sorting is always visible, never just a filter menu.
2. **The Stub is the signature object.** One shape, used everywhere a code appears (list, detail, wallet), never reused for anything else, so it stays special.
3. **Show the reasoning.** The evidence trace (RUL-020) is typeset like a receipt line, not hidden in a tooltip — it is proof of the "no black box" promise.
4. **Quiet base, loud accents, nowhere else.** Neutrals do the heavy lifting; the nine category hues are the only saturated color in the system.
5. **One script, one voice.** Vazirmatn in both languages means Persian is never the "translated" version of the app.

### 12.2 Color tokens

Contrast ratios below are computed WCAG 2 values; "AA text" means ≥ 4.5:1 (body/small text), "AA large" means ≥ 3:1 (≥ 24px or bold ≥ 19px, and non-text UI components).

**Light theme**

| Token | Hex | Role | On `salt` | On `paper` |
|---|---|---|---|---|
| `salt` | `#F3F5F6` | Page background | — | — |
| `paper` | `#FFFFFF` | Card / Stub / sheet surface | — | — |
| `ink` | `#12252B` | Primary text/icons | 14.5:1 | 15.9:1 |
| `ink-2` | `#4A5D64` | Secondary text | 6.3:1 | 6.9:1 |
| `ink-3` | `#5B6B72` | Tertiary text, placeholders, timestamps | 5.1:1 | 5.5:1 |
| `hairline` | `#DCE2E5` | Dividers, Stub tear-edge | decorative (non-text; not used for text) | |
| `accent-ink` (buttons) | `#12252B` on white text | Primary CTA fill | white on ink = 15.8:1 | |
| `error` | `#B3261E` | Destructive actions, validation | 6.0:1 | 6.5:1 |

**Dark theme**

| Token | Hex | Role | On `night` | On `raised` |
|---|---|---|---|---|
| `night` | `#0C1417` | Page background | — | — |
| `surface` | `#131E22` | Raised surface, one step up | — | — |
| `raised` | `#1B2A2F` | Card / Stub / sheet, two steps up | — | — |
| `txt` | `#E7EEF0` | Primary text/icons | 15.9:1 | 13.0:1 |
| `txt-2` | `#A3B5BB` | Secondary text | 8.8:1 | 7.0:1 |
| `txt-3` | `#8195 9B` (see note) | Tertiary text | — | — |
| `hairline-d` | `#243339` | Dividers | decorative | |
| `error-d` | `#FF8A80` | Destructive, on dark | 8.2:1 | 7.4:1 |

> `txt-3` note: use `#81959B` (no internal space; typo artifact of the working notes) — 5.9:1 on `night`, still AA for normal text; reserve for the least important metadata only.

**Category accents.** Each category has a **mark** (the solid color used for spines, dots, small icon fills — sized so WCAG's "large text/graphics" 3:1 threshold applies, never for small body text) and a **tint** (a pale background wash used only behind the mark, e.g. a chip or the Stub's paper color for that category) — light and dark variants of each.

| Category | Mark (light) | Tint (light) | Ink-on-tint (light) | Mark (dark) | Tint (dark, 18% over `surface`) | Mark-tone-on-tint (dark) |
|---|---|---|---|---|---|---|
| Codes (wallet accent) | `#B77900` | `#FFEDB5` | 13.6:1 (ink) | `#FFC93C` | `#3D3D27` | 8.2:1 |
| Offers | `#8248D6` | `#F0E7FC` | 9.1:1 | `#B98CFF` | `#31324A` | 6.9:1 |
| Work | `#2F5BE0` | `#E3EAFD` | 8.4:1 | `#7C9CFF` | `#26354A` | 6.8:1 |
| People | `#12895B` | `#DBF3E8` | 7.9:1 | `#3ED598` | `#1B3F37` | 7.9:1 |
| Money | `#0C8397` | `#D8F0F4` | 7.7:1 | `#3CC8DD` | `#1A3D44` | 7.6:1 |
| Orders & trips | `#C4581A` | `#FDE8D6` | 7.6:1 | `#FF9A57` | `#3D342C` | 7.4:1 |
| Security | `#D0325D` | `#FBDFE6` | 7.7:1 | `#FF7A9C` | `#3D2F38` | 7.0:1 |
| Social & media | `#6B8E00` | `#EBF4CC` | 7.4:1 | `#B4DC3B` | `#304026` | 8.1:1 |
| Updates | `#5F6F76` | `#E7ECEE` | 8.1:1 | `#98AAB1` | `#2B373C` | 7.0:1 |

Rules for using accents:
- The **mark** on `salt`/`night` directly (e.g., an icon with no background) only qualifies as AA for *large* graphics (3:1) — every mark above clears 3.3:1 on light and 7.1:1+ on dark, so this is safe for icons ≥ 24px and spines ≥ 3px wide, but marks are **never** used as small body text on the plain background. For text-on-color (a filled chip, a badge), use ink/`ink`-on-`mark` is not guaranteed AA for every hue at small size — always pair text with the **tint**, not the raw mark, using the "ink-on-tint" column above (all ≥ 7.4:1).
- Exactly one accent is visible per notification row (its category). Multiple accents never appear together except in the category-filter tab bar, where each tab shows its own color only when active.

### 12.3 Typography

| Role | Family | Weight | Size / line-height (base 16px scale) | Notes |
|---|---|---|---|---|
| Display (empty states, onboarding headline) | Vazirmatn | Medium (500) | 28/36 | Used once per screen at most. |
| Title (screen header, section header) | Vazirmatn | Medium (500) | 20/28 | No all-caps, no letter-spacing tricks (anti-tell). |
| Body | Vazirmatn | Regular (400) | 16/24 (Latin), 16/26 (Persian — Arabic script wants a touch more leading) | Line length target ≤ 72 characters in a detail sheet; lists are single-line/two-line truncated by nature. |
| Body secondary | Vazirmatn | Regular (400) | 14/22 | Timestamps, sender lines. |
| Label (button, tab, chip) | Vazirmatn | SemiBold (600) | 14/20 | Sentence case always — "Mark as used", never "MARK AS USED". |
| Code / mono | JetBrains Mono | Medium (500) | 18/24, letter-spacing +0.5px | Promo codes, amounts in the Stub, raw hashes in Settings → About. |
| Caption | Vazirmatn | Regular (400) | 12/18 | Evidence trace, fine print. Minimum size in the app; never smaller. |

Numerals: tabular (lining) figures for all amounts and codes so columns of numbers align; Persian digit rendering follows the D-09 setting (Persian digits when UI language is Persian and the setting allows it) but **codes are always shown in their original captured form**, digits included — never digit-converted, since a code is a literal string to copy.

### 12.4 Iconography

Phosphor Icons, **Regular** weight for inactive/default state, **Fill** or **Bold** for the active/selected state of the same glyph — no mixed icon sets. One glyph per category (§6.1 table), reused identically in the row spine, the tab bar, and Settings → Sources.

### 12.5 Spacing, radius, elevation

- **Spacing scale** (4px base): 4, 8, 12, 16, 24, 32, 48.
- **Radius:** two values only, used with intent, not decoration. `radius-sm` (8px) for chips, buttons, input fields — small interactive controls. `radius-card` (14px) for list rows and sheets. The **Stub is not rounded** in the ordinary sense — it uses a **die-cut ticket outline** (a perforated/torn edge on one side, drawn as a repeating small scallop or triangle notch, plus one squared corner and one clipped corner) so it never gets mistaken for "just another rounded card" (this is the deliberate anti-SaaS-card-kit choice named in 12.1).
- **Elevation:** no soft ambient drop-shadow kit reused everywhere (the anti-tell). Light theme uses a **1px hairline border** (`hairline` token) instead of shadow for ordinary cards; only true overlays (bottom sheets, dialogs) get a real shadow, and it is a single deliberate value (`0 8px 24px rgba(18,37,43,0.16)`), not per-card. Dark theme uses a lightness step (`surface` → `raised`) instead of shadow, since shadows barely read on dark backgrounds.

### 12.6 Motion

One orchestrated moment, not scattered transitions (per the skill's guidance): when a notification's category is decided, the row's spine **draws in** from top to bottom over 180ms as the row settles into the list — the one signature animation, evoking a card being filed. Everything else is answer-to-action only: a code Stub flips over 220ms when "Fill in details" resolves it; a swipe-to-archive follows the finger with no extra flourish; a copied code shows a small checkmark morph on the copy button for 600ms. No entrance animation is applied to static screens on load, and no hover/press animation is applied uniformly to every card — press states use a simple 6% opacity scrim, not a scale transform.

### 12.7 Component: the Stub

The Stub is the one recurring, memorable shape (12.1's "spend your boldness in one place"). It appears in three sizes: **inline** (in a list row, compact), **detail** (full width in the notification detail screen), and **wallet** (the Codes screen).

- Shape: rectangle with a squared top-left corner, a clipped (45°) bottom-right corner, and a perforated right edge (small triangular notches, 4px pitch) suggesting a tear-off stub — evoking a raffle or coat-check ticket.
- Surface: `paper` (light) / `raised` (dark), with a **1px hairline** border in the category's mark color at 40% opacity (not the full saturated mark, to stay quiet).
- Content, top to bottom: category glyph + store name (Body secondary) · the code itself, large, mono, selectable, with a copy-button trailing · discount/value (Title-weight, category-colored text using the "ink-on-tint" pairing, on the tint background as a small inset chip) · a thin expiry bar: a 3px horizontal bar that visually drains left-to-right as `now` approaches `valid_until` (color shifts mark → `error` in the final 6 hours) · fine-print conditions (Caption).
- States: `active` (full color), `expiring` (expiry bar animates a slow 1.5s pulse, respecting reduced-motion), `used` (desaturated, checkmark stamp diagonally across, still legible), `expired` (desaturated, "Expired" stamp).

### 12.8 RTL and bidi

| ID | Requirement | Acceptance |
|---|---|---|
| UX-001 | The whole app mirrors under `FlowDirection.RightToLeft` when the UI language is Persian: navigation icons (back arrow), swipe-to-archive direction, the Stub's clipped corner (moves to bottom-left), progress/expiry bars, and chevrons all mirror. Numerals, the code string, and Latin brand names inside Persian text do **not** mirror internally — only the containment direction flips (standard Unicode bidi). | Snapshot tests: every screen in both directions. |
| UX-002 | Per LNG-005, each text **block** (title, body) picks its own direction by first-strong character, independent of the UI language — a Persian body containing only a Latin code still lays out RTL as a block, with the code itself LTR inline (standard bidi runs), so `Use code SAVE20 today` embedded in a Persian sentence reads correctly. | Test with mixed strings from §8.10. |
| UX-003 | The category **priority order** (§6.1) and any ranked list render top-to-bottom regardless of direction; only horizontal layout mirrors. | Test. |
| UX-004 | Mono/code text is always rendered LTR internally even inside an RTL paragraph (`\u2066`…`\u2069` isolation or the platform's numeric/LTR run handling), so a code never visually reverses. | Test with a code adjacent to Persian digits and punctuation. |

### 12.9 Accessibility floor

Dynamic type up to 130% without truncation in the Stub (test at that scale explicitly, since it is the least standard component); every interactive control ≥ 44×44dp touch target; visible focus ring (2px, `ink`/`txt` colored) for keyboard/switch-access navigation; `prefers-reduced-motion`-equivalent (a system setting check) disables the spine-draw and expiry-pulse animations, replacing them with an instant state change; all color pairings above meet AA; category is never conveyed by color alone — the glyph and the text label are always present alongside the spine color.

---

## 13. Pages / screens

Navigation shell: a bottom tab bar (Inbox, Codes, Digest, Settings) plus a Notification Detail sheet reachable from Inbox and search. RTL mirrors the tab bar order (12.8).

### 13.1 Onboarding

A short, honest sequence — no marketing tone (per the skill's writing guidance: plain, specific, from the user's point of view).

1. **Welcome.** One line naming what the app does ("Alak sorts your notifications and saves your promo codes — entirely on this phone") and one line on privacy ("No internet access. Nothing you don't choose to share ever leaves your phone.").
2. **Language and calendar.** Pick UI language (auto-detected, overridable); if Persian, offer the Jalali/Gregorian and digit choices (D-09) inline, not buried in Settings.
3. **Grant notification access.** Explains *why* (it is the only way the app can see notifications), then opens Android's Notification access settings screen directly. Handles the two failure modes from PF-03:
   - If the switch is greyed out ("Restricted setting"), show the exact fix: *Settings → Apps → Alak → tap the three-dot menu → Allow restricted settings*, with a button that opens the app's App info page.
   - After returning from Settings, detect the granted state (`NotificationManagerCompat.getEnabledListenerPackages`) and confirm on-screen; if still not granted, offer to retry rather than blocking forward progress silently.
4. **OEM battery guidance.** Detect the manufacturer (`Build.MANUFACTURER`) and show manufacturer-specific steps only for OEMs known to kill background listeners (PF-06) — link to Android's own battery-optimization exemption dialog (`ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`) as the primary fix; do not fetch or hardcode instructions from a third-party site.
5. **First capture-depth choices.** A short list of common app categories ("Chats", "Email", "Banking") with the default depth shown and editable; the user can accept defaults and change later.
6. **Done.** Lands on Inbox, empty state (13.2) if nothing has arrived yet.

### 13.2 Inbox

- A single reverse-chronological list, grouped by day headers ("Today", "Yesterday", a date otherwise — Jalali/Gregorian per setting).
- Each row: category spine (12.1), app icon, sender/title, one-line preview (respecting capture depth — a Headline-depth row shows only sender + time), relative time, and a small code glyph if the notification carries a wallet code.
- **Filter tabs** at the top: All + one tab per category (icon + count), horizontally scrollable, mirrored in RTL. Tapping a tab filters in place (no navigation).
- **Search** (icon in the header) searches the FTS index (§6.6) across title/body/sender, respecting capture depth (a Headline row is searchable only by sender/app).
- **Swipe actions:** swipe one direction to archive, the other to open quick-categorize (a small sheet to reassign category, which creates a correction, §9.1). Direction mirrors under RTL (UX-001).
- **Needs-review indicator:** a subtle dot on the "All" tab count when any `needs_review = 1` items exist, with a "Review uncertain" entry point that either opens a batch list (rules-only) or, if an SLM is installed, offers `T3-02`.
- **Empty state:** an illustration-free, text-led message: "Nothing sorted yet. Once notification access is on, new notifications will show up here automatically." (per the skill: empty states are an invitation to act, not a mood board).

### 13.3 Notification detail

Full content (per capture depth), the Stub inline if a code was found (12.7), extracted facts as small labelled rows (amount, expiry, tracking number…), the **evidence trace** (RUL-020) rendered like a receipt footer in Caption type, and actions: change category, add a rule from this sender/app (PER-001), copy code, "Explain" (`T3-01`, only shown if an SLM is installed and the item is not `security`/`redacted`), share as scrubbed test case (PCK-008), delete.

### 13.4 Codes (wallet)

Sections as in WAL-003 (Expiring soon / Active / Used / Expired), each code as a wallet-size Stub (12.7). Tapping opens the detail sheet with reminder controls (WAL-006), edit fields (WAL-007), and the source notification link. A top summary line states the count of active codes and the soonest expiry in plain language ("3 active codes · 1 expires in 6 hours"), not a decorative stat card.

### 13.5 Digest

Two variants, switched by a segmented control: **Rules digest** (always available, generated locally by aggregating the day's categorized items — no model needed) and **AI digest** (`T3-03`, only when a model is installed, with a visible "Generate" button — this screen is the clearest embodiment of D-08's "user-triggered only"). The AI digest shows its own generation state (progress, cancel, SLM-008) and a "Regenerate" action, and every fact in it is expected to be traceable back to a notification (SLM-003) — tapping a bullet opens the source item.

### 13.6 Sources

The `(package, account|channel)` list from PER-010, with per-source category default, depth, mute, and rename controls. This is where the professional persona (US-03) separates "work Gmail" from "personal Gmail".

### 13.7 Rules

List of `user_rules`, each showing its match summary and action in plain language ("Mail from work@company.com → always Work"), reorderable (drag to set `priority`), with a full editor (PER-001) and import/export (PER-004).

### 13.8 Models (SLM)

Import flow (MOD-001), the curated catalog with "Open in browser" links (MOD-007) grouped by RAM tier (MOD-005), installed models with their benchmark badge (MOD-006: Good/Usable/Slow) and a "Set active" / delete action, and the performance profile setting (SLM-024). A persistent, calm note states plainly that models never download inside the app and nothing generated ever leaves the phone.

### 13.9 Settings

Grouped: **Appearance** (theme, language, calendar, digits), **Capture** (per-category depth defaults, default ignore filters from CAP-005, global retention, size cap), **Privacy & security** (app lock, "hide amounts in lists", reset personalization, view what's stored / delete all data), **Codes** (default reminder offsets), **Backup** (export/import, §11.5), **Language packs** (installed packs, import, "unreviewed" badges per PCK-003), **About** (open-source licenses, link to the repository, version and `rules_version`, the app's own privacy explanation in full).

A **"Check capture" banner** (CAP-009) appears at the top of Inbox (not as an interrupting dialog) whenever the health monitor detects the listener has been silent for 24h+ while other apps were plainly active. Its copy states the observation and the one likely fix ("Notification access may have been turned off by the system. Tap to check.") rather than a vague "something's wrong" — following the skill's guidance that errors are never vague about what happened.

---

## 14. Performance budgets (reference device R1, §3 D-10)

| Stage | Budget (p95) | Hard ceiling (p99) | Verified by |
|---|---|---|---|
| Capture copy (binder → queue) | ≤ 5 ms | 10 ms | CAP-003 |
| Normalization per field | ≤ 1 ms / 1 KB | 3 ms | NRM-012 |
| Full Tier 1 pipeline per notification | ≤ 20 ms | 40 ms | RUL-002 |
| Pack rule budget (regex phase) | ≤ 25 ms | abort-and-flag | RUL-006 |
| Tier 2 inference | ≤ 2 ms | 5 ms | ADP-001 |
| Tier 2 online update | ≤ 3 ms | 6 ms | ADP-001 |
| Cold service start to first capture | ≤ 2 s | 4 s | PF-11 |
| Inbox list scroll | 60 fps, no dropped-frame jank on a 500-row page | — | manual + Perfetto trace |
| SLM model load (≤ 1.5B, Q4) | ≤ 15 s | 25 s | §9.4 spike gate |
| SLM decode speed (≤ 1.5B, Q4) | ≥ 3 tokens/s | ≥ 1.5 tokens/s | §9.4 spike gate |
| SLM JSON validity rate (with grammar) | ≥ 95% | ≥ 85% without grammar | SLM-001/002 |
| App idle background footprint (listener bound, no UI) | ≤ 60 MB PSS | 90 MB | PF-11 |
| Global storage footprint before pruning kicks in | 200 MB default cap | user-configurable | STO-003 |
| APK size (arm64-v8a, no bundled model) | ≤ 40 MB | 60 MB | measured at each release |

All budgets are re-measured on R1 at the end of each milestone that could affect them (M2, M3, M8) and recorded in `docs/verification.md`; a regression beyond the p99 ceiling blocks merge for that milestone.

---

## 15. Open-source project hygiene

| ID | Requirement | Acceptance |
|---|---|---|
| OSS-001 | `LICENSE` (GPL-3.0-or-later for `src/`), `LICENSE-DATA` (CC0-1.0 for `packs/`, `catalog/`, `tests/corpus/`), SPDX headers, REUSE-compliant. | `reuse lint` passes in CI. |
| OSS-002 | `CONTRIBUTING.md` covers code and, separately, the much lower-friction pack-contribution path (§8.11), since most contributors will be language/pack authors, not C# developers. | Doc review. |
| OSS-003 | `CODE_OF_CONDUCT.md`, issue templates (bug, pack contribution, language request), PR template requiring a linked requirement ID and, for rule changes, a corpus case (QLT-005). | Templates exist. |
| OSS-004 | CI enforces R2 (no INTERNET permission, no analytics/ad/crash-reporter SDKs) by asserting the merged manifest and the dependency list on every PR. | CI job `privacy-guard` fails the build if violated. |
| OSS-005 | Reproducible-build notes: pinned SDK/workload versions (`global.json`), pinned llama.cpp commit, documented signing process (debug vs release keys kept separate, release key never committed). | Doc + CI check. |
| OSS-006 | A `docs/adr/` folder holds every **[DECISION NEEDED]** and D-0x resolution as a dated ADR, including the §9.4 spike results. | ADRs exist for D-01 through D-10 and ADR-0002 minimum. |

---

## 16. Milestones

Each milestone has a small, testable exit criterion. Do not start a milestone before the previous one's exit criterion is met. Milestones are sized for iterative delivery, not calendar time.

| # | Milestone | Scope | Exit criteria |
|---|---|---|---|
| **M1** | Capture spike | Repo skeleton (§5.2); `NotificationCaptureService` (§7) with no classification — raw rows only; verify PF-01, PF-02, PF-03, PF-05, PF-06, PF-12 on at least two real devices (one low-end, one OEM-skinned). | A notification posted by a test app appears as a raw row within 1s; access survives an app restart and a device reboot; findings for every PF-0x in §5.4 are written to `docs/verification.md`, including exactly how OTP redaction manifests. |
| **M2** | Storage and domain | SQLite/SQLCipher schema (§6.6), migrations, retention job (§6.7), FTS5 (verify PF-10). | 10k synthetic rows insert, query (keyset-paginated), full-text search in both languages, and prune correctly under test; encryption verified (DB file is not plaintext-readable). |
| **M3** | Rule engine core + EN pack | `Alak.Text` (normalization, script/lang detection, numbers/dates), `Alak.Rules` (pack loader, AC automaton, extractors E-01…E-09), the `en` pack seeded per §8.9.1, `alak-pack` CLI, the benchmark harness referenced by RUL-002. | English worked examples (§8.10, EN subset) pass; QLT-003 EN thresholds met on the EN corpus subset (build it alongside the pack, target ≥ 300 cases before this milestone closes, growing to 600 by v1.0); RUL-002 budget met on R1. |
| **M4** | Persian + Finglish | `fa` pack (§8.9.2), Jalali dates (verify PF-09 against a golden table), RTL text handling in `Alak.Text`, `fa-latn` extension pack (§8.9.3, flagged experimental). | Persian worked examples (§8.10) pass; FA corpus reaches 300+ cases; PF-09 golden-table test passes for 1400–1420. |
| **M5** | App shell + design system | MAUI app skeleton, theming (light/dark, §12.2), typography (§12.3, verify font licenses), the Stub component (§12.7), Inbox and Notification Detail screens (§13.2–13.3) wired to real data, RTL mirroring (§12.8) as a snapshot-tested baseline. | Inbox scrolls a 500-row synthetic dataset at the §14 budget; both languages and both themes screenshot-tested; a real captured notification (from M1+M3/M4) renders correctly end to end. |
| **M6** | Onboarding, Sources, Rules, Settings | §13.1, §13.6, §13.7, §13.9; user rules engine (§11.1–11.3); capture-depth policy gate wired into M1's service (CAP-007). | A fresh install walks through onboarding including the restricted-settings recovery path (manual test on a sideloaded APK); a user rule created from Sources correctly overrides Tier 1 on the next matching notification. |
| **M7** | Codes wallet + Tier 2 | Wallet screen and logic (§10), reminders (WAL-006), `Alak.Adaptive` (§9.1), personalization benchmark (ADP-004), backup/restore (§11.5). | WAL corpus scenarios pass; simulated-correction benchmark shows the F1 gain required by ADP-004; a full backup/restore round-trip preserves codes, rules, and personalization. |
| **M8** | AI runtime spike and decision | Execute the §9.4 spike protocol for both candidate engines on R1 and one mid-range device; write ADR-0002 with the numbers; build whichever engine(s) pass the gate. | ADR-0002 committed with measured numbers (not estimates) for every row in §9.4's spike protocol step 2–3; `IInferenceEngine` has at least one real (non-fake) implementation passing ENG-001–006. |
| **M9** | Tier 3 features | Model manager (§9.3) end to end (import → probe → benchmark → activate); the four bounded tasks (§9.2) with grammar/schema validation and the grounding guard (SLM-003); Digest AI variant (§13.5), Models screen (§13.8), "Explain" and "Fill in details" entry points. | Golden-prompt suite (§9.4 step 3) passes at the stated JSON-validity and grounding thresholds on-device; cancellation (SLM-008) verified; resource guard (SLM-020–024) verified with a deliberately oversized model. |
| **M10** | Quality, corpus completion, hardening | Corpus grown to QLT-001 targets (600/600 + adversarial), calibration tool (RUL-019) run and `weights.json` committed, full accessibility pass (§12.9), OSS hygiene (§15) finalized, privacy audit against R2/OSS-004. | QLT-003 macro-F1 and promo/OTP thresholds met on the full held-out corpus in CI; `reuse lint` and `privacy-guard` CI jobs green; manual accessibility pass checklist complete; no network permission anywhere in the merged manifest. |
| **M11** | Release candidate | Distribution packaging for GitHub Releases, F-Droid metadata, Cafe Bazaar/Myket packaging (D-06); performance budgets (§14) re-verified on R1 as a release gate; name/availability check (D-01). | A signed release build installs and functions from each planned channel's packaging format; §14 table fully re-measured and recorded; ADR confirms the final app name and package id. |

Later, post-v1.0 candidates (not scoped further here): a second inference engine plugin if M8 found both viable; additional bundled language packs contributed by the community; a desktop/companion "view only" export tool (would need a new privacy review, since it implies data leaving the phone in some form).