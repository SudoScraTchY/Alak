# Alak — Handoff Prompt & Status for Continuing Agent

**Audience:** AI agent taking over the Alak project. Read `docs/HANDOFF-STATUS.md` and `docs/spec/SPEC.md` FIRST — they are the authoritative state dump and specification. This document covers the delta since HANDOFF-STATUS.md was last refreshed (M4 close → M8 working app), the MAUI-app debugging history, and the delegation prompt to use for the next child agent.

**Repo:** `M:/Hermes/Alak` (Windows host, git-bash shell). Remote: `https://github.com/SudoScraTchY/Alak.git` (gh CLI authenticated as SudoScraTchY — pushes work directly).
**HEAD at time of writing:** `d3c6ba4` on `main`, tree clean, pushed. **The app builds, installs, launches, and renders its full UI on the API 30 x86_64 emulator.**

---

## 1. All SDD / project documents (authoritative set)

| Doc | Role |
|---|---|
| `docs/spec/SPEC.md` | THE specification, v1.0, M1–M11 (§16). Authoritative for all behavior. §10 adaptive, §11 user rules/settings/backup, §12 design system, §13 app shell/screens, §6.6 storage, §8 rules engine, §8.9.3 fa-latn, §8.10 FA examples |
| `docs/HANDOFF-STATUS.md` | Machine-oriented state dump (solution layout, test layout, milestones, exit criteria). Read first each session |
| `docs/AGENT-HANDOFF.md` | THIS document — M8 delta, debugging history, delegation prompt |
| `docs/progress.md` | Human progress log |
| `docs/verification.md` | Verification evidence per milestone |
| `docs/GOAL-PROMPTS.md` | Pre-written delegation goal prompts (M4-era) |
| `docs/adr/` | Architecture decision records |
| `docs/design/` | Design docs (pre-implementation) |
| `docs/contributing/` | Contribution conventions |
| `Directory.Build.props` | Root build props — `TreatWarningsAsErrors=false` (needed: NU1900 NuGet audit in proxied env) |

## 2. Related skills (installed in Hermes, load before MAUI/UI work)

Installed from the Hermes skills hub (`skills-sh/dotnet/skills/...`) at `C:\Users\SaintScraTchY\AppData\Local\hermes\skills\`:

- `maui-shell-navigation` — AppShell/TabBar/ShellContent hierarchy, GoToAsync, route registration
- `maui-app-lifecycle` — window lifecycle, Created/Resumed/Stopped, ConfigureLifecycleEvents
- `maui-data-binding` — BindingContext, x:DataType compiled bindings, converters
- `maui-dependency-injection` — MauiProgram service registration, constructor-injected VMs
- `maui-collectionview` — CollectionView patterns (Inbox list)
- `maui-theming` — AppTheme/resource dictionaries
- Also relevant: `github` (gh CLI auth already done), `subagent-delegation` (probe gateway before dispatch), `spec-driven-implementation`, `hermes-agent-skill-authoring`

---

## 3. Verified state (as of HEAD `d3c6ba4`)

- **M1–M3** done (M3 = `19eb24f`). **M4** all parts done & merged: GOAL A (FA pack+corpus, `253de83`), GOAL B (Jalali DAT-002/PF-09 golden table 1400–1420 + RTL tests), GOAL C (fa-latn §8.9.3, `00a4a62`), GOAL E (adversarial EN corpus 60 cases).
- **M5** done: MAUI app shell per §12–§13 — design tokens (4 XAML resource dictionaries), 5-tab bottom nav, screens, DI wired.
- **M6** done: Onboarding §13.1, Sources §13.6, Rules §13.7, Settings §13.9/§11.4, UserRulesEngine §11.1–11.3, GlobalSettings store.
- **M7** mostly done: Codes Wallet §13.8, Tier 2 Adaptive Engine §10/ADP-004. Backup/Restore §11.5 NOT started.
- **M8** in progress: **app builds, installs, launches, and renders on the emulator** (verified 2026-10-10). UI dump confirms: Inbox tab with category chips (All/Promo/Money/Security/Social/Work/Orders/People/Updates/Other), search bar, empty state, and 5 tabs (Inbox/Scan/Codes/Rules/Settings).
- **Tests:** Storage 10/10 green (`dotnet run --project tests/Alak.Storage.Tests` — VSTest `dotnet test` no longer works on .NET 10; use `dotnet run` per test project). Full suite was 893 green before M8 app work.

## 4. M8 Android debugging history — every real error and its fix (all committed)

1. `ClassNotFoundException ...MainActivity` — manifest had invented Java class names → real `Platforms/Android/MainActivity.cs` (`[Activity(MainLauncher...)] partial class MainActivity : MauiAppCompatActivity`); manifest stripped to permissions only.
2. `XA5207 could not find android.jar API 36` — `-p:AndroidSdkDirectory=M:/Android/Sdks` on every build.
3. **Missing `MainApplication.cs`** — every MAUI Android app needs `[Application] class MainApplication : MauiApplication` with `CreateMauiApp()` → added (`Platforms/Android/MainApplication.cs`).
4. `monodroid: No assemblies found in .__override__` (SIGABRT) — Debug default `EmbedAssembliesIntoApk=false` produced a thin fast-deploy APK with **zero managed assemblies**. Fix: `<EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>` + `<AndroidUseSharedRuntime>false</AndroidUseSharedRuntime>` + `<AndroidFastDeploymentType>None</AndroidFastDeploymentType>` in csproj. Assemblies now appear as `lib/arm64-v8a/lib_*.dll.so` (verify with `unzip -l ...apk | grep lib_`).
5. `ALL entries in APK named lib/x86_64/ MUST be STORED` — misleading monodroid message; real cause was #4. Also set `android:extractNativeLibs="true"` in AndroidManifest `<application>` (libs extract at install; compression then fine).
6. `MAUIR0004 MissingMethodException HarfBuzzSharp.Font.SetVariations` — Resizetizer fails on SVGs containing `<text>` elements. Fix: replaced appicon/appiconfg/splash SVGs with pure-`<path>` graphics (no text, no fonts). Tab icons (inbox/scan/settings) were already clean; added `wallet.svg`, `rules.svg`.
7. `XamlParseException: StaticResource not found for key Radius.MD` — App.xaml merged Typography.xaml (which uses Radius.MD) BEFORE Shapes.xaml (which defines it). Fix: merge order Colors → **Shapes** → Typography → Controls.
8. `XamlParseException: No parameterless constructor for InboxViewModel` — pages instantiated DI-VMs directly in XAML (`<vm:InboxViewModel />`). Fix: removed all 7 XAML `<ContentPage.BindingContext>` blocks; pages now take VMs via constructor injection (`BindingContext = viewModel`), VMs registered in MauiProgram (ScanViewModel + SourcesViewModel registrations added).
9. `SqliteException: no such column: is_read` — NotificationRepository queries `is_read/is_persian/extracted_code`, which migration 1 never created. Fix: **migration 4** in `src/Alak.Storage/StorageConnection.cs` (three `ALTER TABLE notifications ADD COLUMN` statements). Storage tests 10/10 green after.
10. `INSTALL_FAILED_USER_RESTRICTED` on Xiaomi physical device — MIUI blocks adb installs; enable "Install via USB" + "USB debugging (Security settings)" in Developer options.

## 5. Build / deploy commands (verified working)

```bash
# Build (x86_64 for the emulator; use -r android-arm64 for the phone)
dotnet build src/Alak.App/Alak.App.csproj -c Debug -f net10.0-android36.0 -r android-x64 -p:AndroidSdkDirectory=M:/Android/Sdks

# Emulator (headless, background): M:/Android/Sdks/emulator/emulator.exe -avd pixel_7_-_api_30_0 -no-window -gpu swiftshader_indirect
adb -s emulator-5554 uninstall com.alak.app   # always after full rebuild
adb -s emulator-5554 install src/Alak.App/bin/Debug/net10.0-android36.0/android-x64/com.alak.app-Signed.apk
adb -s emulator-5554 shell am start -W -n com.alak.app/crc64a44735e8a764ea7f.MainActivity
adb -s emulator-5554 shell uiautomator dump /sdcard/ui.xml   # verify rendered UI text
adb -s emulator-5554 logcat -d | grep -E "E AndroidRuntime|monodroid"   # crash check
```

Notes: NuGet restore needs the proxy (`export HTTPS_PROXY=http://127.0.0.1:9080`) when new packages are fetched. The emulator AVD `pixel_7_-_api_30_0` has arm translation and may pick arm64-v8a from a multi-ABI APK — building single-RID (`-r android-x64`) avoids this. Physical device: Xiaomi `5a8c869e`.

## 6. Remaining M8 work (NOT done — do not assume)

- [ ] Walk every screen on-device: Scan, Codes (Wallet), Rules, Settings, plus push routes (detail, onboarding, sources, stub-detail) — fix runtime binding/XAML errors as they surface (the pattern so far: each crash revealed the next real bug; expect a few more).
- [ ] NotificationListenerService platform implementation (capture side) — not written; manifest has no service entry (correct until the class exists).
- [ ] M7 leftover: Backup/Restore §11.5.
- [ ] Fonts: `Resources/Fonts/` is EMPTY but MauiFont includes `Vazirmatn-*.ttf`/`JetBrainsMono-*.ttf` globs — MAUI `ConfigureFonts` references may fail at runtime; obtain real TTFs or remove.
- [ ] Re-add SkiaSharp (3.x line only) when barcode rendering (§13.8) is implemented — 4.x breaks the Resizetizer.
- [ ] Restore full test suite green (`dotnet run` per project) after app changes.

## 7. Delegation prompt (copy-paste for the next child agent)

> **Goal:** Continue Alak milestone M8: walk every MAUI screen on the emulator and fix all runtime defects found. The app already BUILDS, INSTALLS, LAUNCHES, and RENDERS the Inbox tab at HEAD `d3c6ba4` — do not re-litigate boot issues; the debugging history in `docs/AGENT-HANDOFF.md` §4 lists every fixed error. The AI/ML inference model is OUT OF SCOPE — `InferenceEngineStub` is intentional until M9.
>
> **Context:** Repo root `M:/Hermes/Alak`, branch `main`. Authoritative docs (read in this order): `docs/HANDOFF-STATUS.md`, `docs/AGENT-HANDOFF.md` (M8 delta + build commands §5), `docs/spec/SPEC.md` (§12 design system, §13 screens, §11 rules/settings, §10 adaptive, §13.8 wallet). Verified starting state: HEAD `d3c6ba4`, tree clean, pushed; Storage tests 10/10 green via `dotnet run --project tests/Alak.Storage.Tests` (VSTest dotnet-test is broken on .NET 10 — always use `dotnet run` per test project). Build: `dotnet build src/Alak.App/Alak.App.csproj -c Debug -f net10.0-android36.0 -r android-x64 -p:AndroidSdkDirectory=M:/Android/Sdks`. Deploy: `adb -s emulator-5554 uninstall com.alak.app && adb -s emulator-5554 install src/Alak.App/bin/Debug/net10.0-android36.0/android-x64/com.alak.app-Signed.apk && adb -s emulator-5554 shell am start -W -n com.alak.app/crc64a44735e8a764ea7f.MainActivity`. Verify rendering with `adb shell uiautomator dump` and crashes with `adb logcat -d | grep "E AndroidRuntime"`. csproj is already correct: EmbedAssembliesIntoApk=true, AndroidUseSharedRuntime=false, extractNativeLibs=true in manifest, single-RID builds. Conventions: ViewModels via constructor injection into pages (NEVER `<vm:X />` in XAML), standard Shell TabBar/ShellContent hierarchy, Routes via Routing.RegisterRoute in AppShell.xaml.cs, resource-dictionary merge order Colors→Shapes→Typography→Controls, SVGs must contain NO `<text>` elements (HarfBuzz breaks Resizetizer). Follow the installed skills: maui-shell-navigation, maui-data-binding, maui-dependency-injection, maui-app-lifecycle, maui-collectionview, maui-theming.
>
> **Tasks:** (1) Launch app; tap through Scan, Codes, Rules, Settings tabs and push routes (detail, onboarding, sources, stub-detail via `GoToAsync`); for each crash capture `adb logcat`, diagnose, fix, rebuild, redeploy. (2) Keep all test projects green (`dotnet run` per project). (3) Commit per logical fix (conventional messages) and `git push origin main` after each verified-green state; NEVER force-push. (4) Do NOT implement: NotificationListenerService, Backup/Restore §11.5, the inference model, or SkiaSharp/barcode rendering — report them as open items instead.
>
> **Anti-fabrication contract:** 'NOT RUN' is an acceptable answer, invented numbers are not. Report only metrics, test results, commit hashes, and logcat excerpts you produced from real command output. End your report with the output of `git status` and `git log --oneline -5`.

## 8. Commit cadence rule (user instruction, 2026-10-09)

Push to GitHub periodically — commit per logical fix and `git push origin main` immediately after each verified-green state. Do not accumulate large uncommitted changesets. On any mystery breakage, run `git status`/`git diff` FIRST before editing (see INCIDENT 2026-09-26: RulesEngine.cs wiped mid-session).
