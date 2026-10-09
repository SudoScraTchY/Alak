# Alak — Handoff Prompt & Status for Continuing Agent

**Audience:** AI agent taking over the Alak project. Read `docs/HANDOFF-STATUS.md` and `docs/spec/SPEC.md` FIRST — they are the authoritative state dump and specification. This document covers the delta since HANDOFF-STATUS.md was last refreshed (M4 close → M8 in progress), the MAUI-app debugging history, and the delegation prompt to use for the next child agent.

**Repo:** `M:/Hermes/Alak` (Windows host, git-bash shell). Remote: `https://github.com/SudoScraTchY/Alak.git` (gh CLI authenticated as SudoScraTchY — pushes work directly).
**HEAD at time of writing:** `b86ddb6` on `main`, tree clean after the listed commits.

---

## 1. All SDD / project documents (authoritative set)

| Doc | Role |
|---|---|
| `docs/spec/SPEC.md` | THE specification, v1.0, M1–M11 (§16). Authoritative for all behavior. §10 adaptive, §11 user rules/settings/backup, §12 design system, §13 app shell/screens, §6.6 storage, §8 rules engine, §8.9.3 fa-latn, §8.10 FA examples |
| `docs/HANDOFF-STATUS.md` | Machine-oriented state dump (solution layout, test layout, milestones, exit criteria). Read first each session |
| `docs/progress.md` | Human progress log |
| `docs/verification.md` | Verification evidence per milestone |
| `docs/GOAL-PROMPTS.md` | Pre-written delegation goal prompts (M4-era) |
| `docs/adr/` | Architecture decision records |
| `docs/design/` | Design docs (pre-implementation) |
| `docs/contributing/` | Contribution conventions |
| `Directory.Build.props` | Root build props — `TreatWarningsAsErrors=false` (needed: NU1900 NuGet audit in proxied env) |

---

## 2. Verified state (as of HEAD `b86ddb6`)

- **M1–M3** done (M3 = `19eb24f`). **M4** all parts done & merged: GOAL A (FA pack+corpus, `253de83`), GOAL B (Jalali DAT-002/PF-09 golden table 1400–1420 + RTL tests, `53faa04`), GOAL C (fa-latn §8.9.3, `00a4a62`), GOAL E (adversarial EN corpus 60 cases, 6 tests, `781987e`).
- **M5** done: MAUI app shell per §12–§13 — design tokens (DesignTokens.cs + 4 XAML resource dictionaries), 6-tab bottom nav, 8 screens, DI wired.
- **M6** done: Onboarding §13.1 (5-step), Sources §13.6 (PER-010), Rules §13.7, Settings §13.9/§11.4, UserRulesEngine §11.1–11.3 (user rules evaluated BEFORE Tier 1, first match by priority asc/id asc), GlobalSettings key-value store.
- **M7** done: Codes Wallet §13.8 (PromoCodeRepository + WalletViewModel/Page), Tier 2 Adaptive Engine §10/ADP-004 (IAdaptiveEngine: RecordOutcomeAsync/GetWeightsAsync/AdjustWeightAsync/ResetAsync/Export/Import; FNV-1a hashing trick D=65536 C=9, AdaGrad, 30-day half-life decay, JSON persistence, thread-safe). Backup/Restore §11.5 NOT yet started.
- **M8** in progress: MAUI app now BUILDS and DEPLOYS to Android. Commit `b86ddb6`.
- **Tests:** 893 passing (Alak.Text 137 + Rules 704 + Core 20 wait — actual breakdown by project: Core 20, Text 137, Rules 704, Adaptive 1, Inference.Abstractions 21, Storage 10). Run: `dotnet test` from repo root.

### Milestone M8 Android-app debugging history (all real errors, all fixed)

1. `ClassNotFoundException crc64f8e5b5e3e6c8e7d1.MainActivity` — manifest had **invented Java class names**. Fix: real `Platforms/Android/MainActivity.cs` (`[Activity(MainLauncher=true...)] partial class MainActivity : MauiAppCompatActivity`), manifest stripped to permissions only (MAUI generates activity entries from attributes).
2. `TypeLoadException: Could not load type 'Alak.App.MainActivity'` — stale incremental build: Java wrapper regenerated but embedded managed assembly didn't. Fix: `dotnet build-server shutdown`, delete `bin`+`obj`, full rebuild, `adb uninstall` before install.
3. `XA5207 Could not find android.jar for API 36` — SDK platform missing. Fix: user ran `-t:InstallAndroidDependencies` (or pass `-p:AndroidSdkDirectory=M:/Android/Sdks`).
4. `monodroid: No assemblies found in .../.__override__` + SIGABRT on x86_64 emulator — shared-runtime/fast-deployment assumption. Fix in csproj: `<AndroidUseSharedRuntime>false</AndroidUseSharedRuntime>`, `<AndroidFastDeploymentType>None</AndroidFastDeploymentType>`, `<RuntimeIdentifiers>android-arm64;android-x64</RuntimeIdentifiers>`.
5. `MAUIR0004 MissingMethodException HarfBuzzSharp.Font.SetVariations` — SkiaSharp 4.153.1's HarfBuzzSharp transitive version conflicts with MAUI Resizetizer's Svg.Skia. Fix: removed `SkiaSharp.Views.Maui.Controls` package (app doesn't reference SkiaSharp in code yet — re-add pinned to v3 when barcode rendering lands).
6. `INSTALL_FAILED_USER_RESTRICTED` on the Xiaomi physical device — MIUI blocks adb installs; user must enable "Install via USB" + "USB debugging (Security settings)" in Developer options.
7. **BLANK SCREEN root cause:** `App.xaml.cs` overrode `CreateWindow()` but never set `MainPage`. A Window with no page renders nothing. Fix: `MainPage = new AppShell()` in App constructor; `CreateWindow` override removed. Also removed a bogus `Shell.ItemTemplate` override (ContentView/Grid wrapper) that broke Shell's default tab hosting.

### MAUI project conventions now in place (verified against installed skills: maui-shell-navigation, maui-app-lifecycle, maui-data-binding, maui-dependency-injection, maui-collectionview, maui-theming)

- Shell navigation: standard `<TabBar><Tab><ShellContent ContentTemplate="{DataTemplate views:XPage}"/>` hierarchy; routes for push pages via `Routing.RegisterRoute` in `AppShell.xaml.cs`. NO custom ItemTemplate.
- DI: ViewModels and pages registered in `MauiProgram.cs`; pages take VMs via constructor injection (`BindingContext = vm`).
- Build for device: `dotnet build src/Alak.App/Alak.App.csproj -c Debug -f net10.0-android36.0 -p:AndroidSdkDirectory=M:/Android/Sdks`
- Install: `adb install -r src/Alak.App/bin/Debug/net10.0-android36.0/com.alak.app-Signed.apk` (always `adb uninstall com.alak.app` first after a full rebuild to avoid stale-assembly crashes)
- Real activity class: `crc64a44735e8a764ea7f.MainActivity` (determined by package hash — verify with `adb shell cmd package resolve-activity --brief com.alak.app` if it changes)

### Known-open items (M8 exit work)

- [ ] On-device verification of the `MainPage` fix (device was offline when fix landed — emulator `emulator-5554` x86_64 AVD "pixel_7_-_api_30_0" and Xiaomi phone `5a8c869e` both available when connected)
- [ ] Walk each screen (Inbox/Scan/Settings/Rules/Onboarding/Wallet) on device; fix runtime binding/XAML errors as they surface
- [ ] NotificationListenerService platform implementation (capture side) — not yet written; manifest currently has no service entry (correct, since the class doesn't exist yet)
- [ ] M7 leftover: Backup/Restore §11.5
- [ ] Fonts: Vazirmatn/JetBrainsMono TTFs are placeholders — confirm real files exist in Resources/Fonts or the `ConfigureFonts` entries will fail at runtime
- [ ] Re-add SkiaSharp (pinned v3.x line) when barcode rendering (§13.8) is implemented

---

## 3. Delegation prompt (copy-paste for the next child agent)

> **Goal:** Continue Alak milestone M8: verify the MAUI Android app launches and every implemented screen works on-device, then fix all defects found. The blank-screen root cause is already fixed at HEAD (`b86ddb6`): `App.xaml.cs` sets `MainPage = new AppShell()` and the nonstandard `Shell.ItemTemplate` was removed. The AI/ML inference model is OUT OF SCOPE — the InferenceEngineStub is intentional (M8 uses stubs).
>
> **Context:** Repo root `M:/Hermes/Alak`, branch `main`. Authoritative docs (read in this order): `docs/HANDOFF-STATUS.md`, `docs/spec/SPEC.md` (§12 design system, §13 screens, §11 rules/settings, §10 adaptive, §13.8 wallet). Verified starting state: HEAD `b86ddb6`, tree clean, 893 tests green (`dotnet test`). The MAUI app builds via `dotnet build src/Alak.App/Alak.App.csproj -c Debug -f net10.0-android36.0 -p:AndroidSdkDirectory=M:/Android/Sdks` and installs via `adb install -r src/Alak.App/bin/Debug/net10.0-android36.0/com.alak.app-Signed.apk` (run `adb uninstall com.alak.app` first after full rebuilds). Emulator AVD `pixel_7_-_api_30_0` (x86_64, API 30); physical device `5a8c869e` (Xiaomi, arm64). Build conventions already fixed in csproj: self-contained runtime (`AndroidUseSharedRuntime=false`), no fast deployment, RIDs `android-arm64;android-x64`, SkiaSharp REMOVED (don't re-add 4.x — it breaks the Resizetizer; use 3.x when needed).
>
> **Tasks:** (1) Build and deploy to the emulator; confirm the app launches past splash — the launcher activity is `crc64a44735e8a764ea7f.MainActivity`. (2) Walk every tab: Inbox, Scan, Settings, Rules, Onboarding, Wallet/Codes. For each crash/blank/binding error, capture `adb logcat` output, diagnose, fix, rebuild, redeploy. Follow standard MAUI patterns (see installed skills maui-shell-navigation / maui-data-binding / maui-dependency-injection): `BindingContext` via constructor-injected ViewModel, standard Shell hierarchy, no code-behind logic beyond wiring. (3) Run `dotnet test` and keep all 893 tests green. (4) Commit per logical fix with conventional messages, push to `origin main` (gh auth is configured). NEVER force-push.
>
> **Anti-fabrication contract:** 'NOT RUN' is an acceptable answer, invented numbers are not. Report only metrics, test results, commit hashes, and logcat excerpts you produced from real command output. End your report with the output of `git status` and `git log --oneline -5`.

---

## 4. Commit cadence rule (user instruction, 2026-10-09)

Push to GitHub periodically — commit per logical fix and `git push origin main` immediately after each verified-green state. Do not accumulate large uncommitted changesets.
