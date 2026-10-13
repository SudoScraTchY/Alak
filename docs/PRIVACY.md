# Alak — Privacy & Security Model

Alak reads other apps' notifications — one of the most sensitive capabilities Android grants. This
document states exactly what the app does with that access, and how each promise is *enforced*
rather than asked for politely. The authoritative requirement IDs (CAP-xxx, RUL-xxx, OSS-xxx,
QLT-xxx) live in [`spec/SPEC.md`](spec/SPEC.md).

## 1. The core promise: no network, ever

Alak is **offline by construction**:

- It never declares `android.permission.INTERNET` or `ACCESS_NETWORK_STATE`.
- It ships no HTTP client, no analytics SDK, no ad SDK, no crash reporter, no update checker.
- There is no account, no sync, no subscription, no cloud component anywhere in the product.

**Enforcement:** the CI `privacy-guard` job runs on every push and pull request and fails the build
if any manifest or project file declares a banned permission (`INTERNET`,
`ACCESS_NETWORK_STATE`, `QUERY_ALL_PACKAGES`, `READ_EXTERNAL_STORAGE`), if any banned SDK family
(analytics/ads/crash reporting) appears in a project file, or if any C# source contains a network
client (`System.Net`, `HttpClient`, `WebClient`, raw sockets). You can read the whole check in
[`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — it is intentionally crude and greppable.

**Consequence:** because the app physically cannot open a socket, it cannot leak what it reads.

## 2. What the app can and cannot do with notifications

| | |
|---|---|
| ✅ Can | Read notifications of apps the **user explicitly allows** during onboarding (Android's Notification Listener access, revocable any time in system settings) |
| ❌ Cannot | Dismiss, reply to, or act on any notification (read-only by design) |
| ❌ Cannot | Read SMS directly (different permission class; SMS apps' *notifications* are captured like any other) |
| ❌ Cannot | Send anything anywhere (no network capability — §1) |

## 3. What is stored, and where

- **Encrypted at rest.** All captured content lives in an SQLCipher-encrypted SQLite database
  (`SQLitePCLRaw.bundle.sqlcipher`), with FTS5 full-text search on top. Verified by integration
  tests (PF-10 in [`verification.md`](verification.md)).
- **Retention rules** prune old rows automatically; storage size is bounded.
- **Per-app capture depth** (spec §6.5): you choose, per app, how much is kept. Chat apps default
  to **sender + time only** until you opt in to full content.
- **OTP / 2FA content is never persisted.** This is a zero-tolerance policy: corpus tests assert
  that one-time-password content does not survive into storage, and the rule engine deliberately
  classifies ride PINs as *ride* data, not security OTPs.
- **Backup/restore** (planned, spec §11.5) is a local, encrypted file you create explicitly —
  never an automatic cloud copy, because there is no cloud.

## 4. Explainability

Every automatic decision — category, extracted entity, tag — carries a **human-readable reason**
the UI renders (spec R4 / RUL-020). You can always see *why* something landed in Money, or why a
promo code was saved. Explanations are app-owned templates; language packs reference them by key
and never inject arbitrary UI text.

## 5. AI features are opt-in, local, and yours

- Alak ships **no model weights** and no downloader. You obtain a model file yourself (browser, any
  means) and import it via the Android file picker (spec D-03). The Models screen only offers
  curated links that open in *your* browser.
- Imported models are hash-verified before use.
- The model runs **only when you tap the button** — never scheduled, never in the background
  (spec D-08).
- Inference is bound to the device: prompts and outputs never leave it, because they cannot (§1).
- The runtime sits behind `IInferenceEngine`; the runtime choice (provisional: llama.cpp) is
  recorded in an ADR with measured on-device numbers (spec §9.4, M8 decision gate).

## 6. Extensibility is data-only

Language packs, corpora and model catalogs are data. Nothing loaded from a pack may execute code
(spec R3): pack regexes are compiled with `RegexOptions.NonBacktracking` and capped in size; pack
files cannot reference paths outside their folder; the `alak-pack` CLI validates schema and safety
(`PCK-001`, `PCK-004`) before a pack can ship.

## 7. Android permissions, complete list

The app requests **no runtime permissions** beyond **Notification Listener access**, which the user
grants explicitly in Android settings during onboarding and can revoke at any time. The manifest
contains no `INTERNET`, no network-state, no storage, no contacts, no location. (Verified
continuously by the CI privacy-guard; on-device audit rows are tracked in
[`verification.md`](verification.md) as PF-xx items.)

## 8. Reporting a security issue

Please do not open a public issue for a vulnerability. Use GitHub's *Report a vulnerability*
(private security advisory) on this repository, or contact the maintainer directly
([@SudoScraTchY](https://github.com/SudoScraTchY)). Include a minimal reproduction; device/OS
version; and, if relevant, the notification text shape that triggers the issue (redact real codes).

## 9. Honest status of verification

Items that require a physical device (real-notification latency, on-device SQLCipher behavior,
battery impact, SLM speed) are tracked as `TODO(verify)` rows in
[`verification.md`](verification.md) until measured on real hardware — the spec forbids counting
emulator results as evidence (spec R7). Claims in this document are either enforced in CI, proven
by a test, or explicitly marked as pending on-device verification.
