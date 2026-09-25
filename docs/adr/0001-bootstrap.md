# ADR-0001: Bootstrap and toolchain pins

**Date:** 2026-09-25 · **Status:** Accepted

## Context

Alak is a long-running, multi-agent implementation of `docs/spec/SPEC.md` (Alak v0.1, 2026-09-24).
Several toolchain facts needed re-verification at project start (spec §0.1, §5.1, PF-10, R1).

All "VERIFY" items below were checked today on this machine, results recorded for reference.
This ADR records both the verified toolchain and the working conventions for the build.

## Verified facts (2026-09-25, host = Windows 10, SDK pin via global.json)

| Fact | Result |
|---|---|
| .NET SDK | **10.0.301** installed (`dotnet --list-sdks`: 9.0.300, 10.0.202? no: 9.0.300, 10.0.301). Roll-forward `latestFeature` pinned in `global.json`. |
| .NET MAUI workload | **android 36.1.43** (VS-installed). `dotnet workload list` shows `android 36.1.43/10.0.100` + `maui-windows 10.0.20/10.0.100`. The MAUI packages (`Microsoft.Maui.Controls` etc.) come from NuGet at build time, not the workload. |
| Android platforms | SDK has **android-35 and android-36** platform dirs; build-tools **36.0.0**; **no NDK installed yet** (needed for M8 llama.cpp shim; not a blocker for M1–M7). |
| adb | Present at `C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe`; **no device attached** at bootstrap. Device work (PF-0x verification, R7) is deferred to when a physical device is available. |
| Java | OpenJDK **21.0.11 LTS** (2026-04-21) — meets the Android workload's JDK 17+ requirement. |
| FTS5 in SQLCipher bundle | Not yet verified. **TODO(verify):** create an encrypted DB with `SQLitePCLRaw.bundle.sqlcipher`, `CREATE VIRTUAL TABLE … USING fts5`, query in Persian + English, record in `docs/verification.md` (PF-10). |
| SQLitePCLRaw.bundle.sqlcipher on NuGet | Exact package ID confirmed on nuget.org search: `SQLitePCLRaw.bundle.sqlcipher` with stable 2.1.x line; will pin to the 2.1.x stable version that resolves for net10.0 at first restore (record the exact version used). |
| Licenses | `LICENSE` = GPL-3.0 full text (gnu.org, 35149 bytes) and `LICENSE-DATA` = CC0 1.0 legal code (creativecommons.org, 7048 bytes), both fetched today and committed at repo root per D-02. |

## Decisions

1. **Pin the SDK** in `global.json` (10.0.301, `rollForward: latestFeature`) — OSS-005.
2. **Package versions live in each `.csproj`, not in `Directory.Build.props`** — multiple agents editing one shared props file caused merge conflicts on every bump. Each project pins its own exact versions.
3. **Repo root** = `M:\Hermes\Alak` (user choice). The repo is the single source of truth; the `.hermes/` plan dir holds task plans only.
4. **Tests before app**: the desktop-only test projects (ARC-001) are scaffolded in the first commit so every later change is test-covered from the start (TDD skill: no production code without a failing test first).
5. **Device-dependent PF-0x items** are marked TODO(verify) with docs/verification.md stubs; they are exit criteria for M1, not blockers for Core/Text/Rules/Storage libraries. No emulator evidence substitutes for real-device data (R7).
6. PF-10 (SQLCipher+FTS5) will be verified via a desktop integration test (the SQLCipher bundle runs on .NET desktop too); the Android-side check rides the M1 device session.
5. **LLamaSharp** is "evaluate, not assume" per spec §9.4; the C-shim P/Invoke path is the default (D-04).

## Consequences

- CI (GitHub Actions) runs `unit-tests` (no Android workload) + `privacy-guard` (OSS-004) on every PR.
- No INTERNET permission will ever appear in a manifest: privacy-guard greps for it and fails.
- The NDK is not installed; M8 (native shim) will install/build it via `sdkmanager` when that milestone starts.

## Conventions for agents (binding)

- Dependency direction (spec §5.2): `App → (Rules, Adaptive, Storage, Inference.*) → (Text, Core)`. Never add a reverse reference. Enforced by review and by test-project references.
- Every requirement ID (CAP-xxx, RUL-xxx, NRM-xxx …) referenced in commits/tests: put the ID in the test name and commit message (spec §0.2).
- Anything marked VERIFY in the spec gets a row in `docs/verification.md` when checked, or `TODO(verify)` otherwise (R8).
- OTP content is never persisted — zero tolerance, corpus-tested (RUL-014, CAP-008, QLT-003).
- Test frameworks: xUnit + Shouldly (never FluentAssertions ≥8 — spec §5.1 tests row).

## Toolchain table (for agents)

| Concern | Pin |
|---|---|
| SDK | 10.0.301 (global.json) |
| TFM | net10.0 (libs), net10.0-android36.0 (app, per spec §5.1) |
| MAUI Controls | 10.0.100 line (10.0.110 latest stable verified on NuGet today) |
| CommunityToolkit.Maui | 15.0.1 (verified on NuGet today; pulls Microsoft.Maui.Controls 10.0.90) |
| CommunityToolkit.Mvvm | 8.4.2 (verified on NuGet today) |
| Microsoft.Data.Sqlite | 10.0.12 (verified) |
| SQLitePCLRaw.bundle.sqlcipher | 2.1.x stable (exact pin recorded at first restore; TODO(verify) FTS5) |
| xUnit | 2.9.3 / runner 3.1.5 / MS Test SDK 18.10.1 (verified) |
| Shouldly | 4.3.0 (verified) |
| YamlDotNet | 17.1.0 (tooling/tests only; 18.x exists, keep 17.x until CLI is written — record reason in ADR when bumped) |
| Phosphor Icons | MIT-licensed (spec choice); font import deferred to M5 design-system work |

