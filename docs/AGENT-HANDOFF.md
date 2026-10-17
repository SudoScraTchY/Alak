# Alak — Handoff Prompt & Status for Continuing Agent

**Audience:** AI agent taking over the Alak project. Read `docs/spec/SPEC.md` FIRST (authoritative spec), then THIS document (current state + delegation prompt). `docs/HANDOFF-STATUS.md` holds the M1–M3 machine dump and binding conventions; its milestone section was refreshed 2026-10-17.

**Repo:** `M:/Hermes/Alak` (Windows host, PowerShell 5.1 sandbox). Remote: `https://github.com/SudoScraTchY/Alak.git` (authenticated as `SudoScraTchY` — pushes work directly).
**HEAD at time of writing:** `6b5619e` on `main`, pushed (`f6bf485..6b5619e`). Tree: clean except `ob.jpg` (user's stray image, never commit) and `.hermes/` (agent scratch/screenshots, never commit).
**Working directory:** `M:\Hermes\Alak` — this is the zwork project root; file tools are sandboxed to it.

---

## 1. Verified state (as of HEAD `6b5619e`, 2026-10-16)

- **M1–M4** done (engine + EN/FA/fa-latn packs + corpus; see `HANDOFF-STATUS.md` §3).
- **M5–M6** done: MAUI app shell, design tokens, onboarding, sources, rules, settings.
- **M7** mostly done: Codes Wallet, Tier-2 adaptive. **Backup/Restore §11.5 NOT started.**
- **M8** in progress:
  - App boots/renders on emulator (verified 2026-10-10 at `d3c6ba4`).
  - **UI redesigned on the SPEC §12 design system and verified on the physical device** (Redmi Note 11 `2201117TG`, adb id `5a8c869e`, arm64; verified 2026-10-16): Inbox, Codes, Rules, Settings, Onboarding — light AND dark themes correct. Screenshots: `.hermes/ui-audit/` (scratch, not committed).
  - **PENDING (do this first):** the Scan-tab crash fix (`BarcodesDetected`) is committed but was never re-verified on device — the phone dropped off USB. Launch app, tap Scan, confirm live camera preview and no entry in `adb logcat -b crash`.
- **Tests:** 893/893 green via `dotnet run` per project (Core 20, Rules 704, Text 137, Storage 10, Adaptive 1, Inference.Abstractions 21). VSTest `dotnet test` does NOT work on .NET 10 — always `dotnet run --project tests/<X>.Tests`.
- Docs commit `f6bf485` (2026-10-13): README EN+FA, ARCHITECTURE, PRIVACY, BUILD — keep them accurate when behavior changes.

**Human direction (2026-10-15):** current focus is UI redesign/enhancement; classification-logic integration has NOT started ("we have not reached the concept yet") — do not wire inference into the UI without an explicit request. `InferenceEngineStub` is intentional until M9. Related skill repos the user follows: `davidortinau/maui-skills` (maui-* skills installed locally under `C:\Users\SaintScraTchY\AppData\Local\hermes\skills\`) and `amit-nayar/android-adb-skill` (not installed).

---

## 2. Sandbox environment cheatsheet (hard-won; every item bit us once)

**Git identity (MANDATORY for every commit).** The sandbox injects `GIT_AUTHOR_NAME=zwork`, `GIT_AUTHOR_EMAIL=zwork@local`, `GIT_COMMITTER_NAME=zwork`, `GIT_COMMITTER_EMAIL=zwork@local` into EVERY exec shell. Override in the same shell as the commit:
```powershell
$env:GIT_AUTHOR_NAME='SudoScraTchY';  $env:GIT_AUTHOR_EMAIL='SainScraTchY@gmail.com'
$env:GIT_COMMITTER_NAME='SudoScraTchY'; $env:GIT_COMMITTER_EMAIL='SainScraTchY@gmail.com'
git commit ...
```
If a commit lands with the wrong author: `git commit --amend --reset-author --no-edit` (a plain `--amend` KEEPS the original author). Machine account = `SudoScraTchY <SainScraTchY@gmail.com>` (from `C:/Users/SaintScraTchY/.gitconfig`, matches repo owner).

**dotnet.** `dotnet` on PATH resolves to the AutoClaw vendor runtime (NO SDKs). Always use the full path `C:\Program Files\dotnet\dotnet.exe`. `global.json` pins 10.0.301; machine has 10.0.401 (rollForward covers it).

**NuGet.** The exec shell has EMPTY `APPDATA`/`LOCALAPPDATA` → restore dies with `Value cannot be null. (Parameter 'path1')`. Set before any build:
```powershell
$env:APPDATA='C:\Users\SaintScraTchY\AppData\Roaming'; $env:LOCALAPPDATA='C:\Users\SaintScraTchY\AppData\Local'
```
NuGet restore needs the proxy (`$env:HTTPS_PROXY='http://127.0.0.1:9080'`) only when NEW packages are fetched.

**Android.** SDK root `M:/Android/Sdks` (always pass `-p:AndroidSdkDirectory=M:/Android/Sdks`). adb: `M:\Android\Sdks\platform-tools\adb.exe`. Device: Xiaomi/MIUI `5a8c869e` (Redmi Note 11). Emulator AVD: `pixel_7_-_api_30_0`. MIUI quirks: `INSTALL_FAILED_USER_RESTRICTED` → retry the install once (on-phone prompt); MIUI overlay ads can steal `input tap` coordinates — verify the foreground app after taps.

**Screenshots.** `adb shell screencap -p /sdcard/a.png` then `adb pull` — NEVER `adb exec-out screencap -p > file` under PowerShell 5.1 (text re-encoding corrupts the PNG). Device screen is 1080x2400; screenshots come back 900x2000 (×1.2 scale for tap coordinates). Tab-bar centers at real y≈2160: x = 113 (Inbox), 324 (Scan), 540 (Codes), 756 (Rules), 968 (Settings).

**Long commands.** A combined build+install+launch+screenshot can outlive the exec window. Split into steps; after ANY timeout, re-verify actual state (build may have landed, install may not have run) — never blind-replay.

**File edits.** Repo has mixed CRLF/LF line endings; the edit tools require EXACT matches (mismatch → `workspace_edit_match_not_found` — retry with `\r\n` or rewrite the whole file). Keep `docs/spec/SPEC.md` untouched (ADR required to change spec).

---

## 3. All SDD / project documents (authoritative set)

| Doc | Role |
|---|---|
| `docs/spec/SPEC.md` | THE specification, v1.0, M1–M11 (§16). §12 design system, §13 screens, §11 rules/settings/backup, §10 adaptive, §6.6 storage, §8 rules engine |
| `docs/HANDOFF-STATUS.md` | M1–M3 machine dump + binding conventions (milestones refreshed 2026-10-17) |
| `docs/AGENT-HANDOFF.md` | THIS document — current state, debugging history, delegation prompt |
| `docs/README.md` story | `README.md` (EN, authoritative) + `README.fa.md` (FA) + `docs/ARCHITECTURE.md` + `docs/PRIVACY.md` + `docs/BUILD.md` |
| `docs/progress.md` / `docs/verification.md` | progress log / PF-0x + QLT verification evidence |
| `docs/adr/`, `docs/design/`, `docs/contributing/`, `docs/GOAL-PROMPTS.md` | ADRs, pre-implementation design docs, conventions, M4-era prompts |
| `Directory.Build.props` | `TreatWarningsAsErrors=false` (NU1900 audit noise in proxied env — keep) |

Related installed skills: `maui-shell-navigation`, `maui-theming`, `maui-data-binding`, `maui-dependency-injection`, `maui-collectionview`, `maui-app-lifecycle` (under `C:\Users\SaintScraTchY\AppData\Local\hermes\skills\`; NOT readable via the sandboxed file tools — session skills only; standard MAUI knowledge suffices for what they cover).

---

## 4. Debugging history — every real error and its fix (all committed)

### M8 boot era (through `d3c6ba4`, emulator)
1. `ClassNotFoundException ...MainActivity` — manifest had invented Java class names → real `Platforms/Android/MainActivity.cs`; manifest stripped to permissions only.
2. `XA5207 could not find android.jar API 36` — pass `-p:AndroidSdkDirectory=M:/Android/Sdks` on every build.
3. Missing `MainApplication.cs` — every MAUI Android app needs `[Application] class MainApplication : MauiApplication` with `CreateMauiApp()`.
4. `monodroid: No assemblies found in .__override__` (SIGABRT) — Debug default thin APK has zero managed assemblies. Fix: `EmbedAssembliesIntoApk=true` + `AndroidUseSharedRuntime=false` + `AndroidFastDeploymentType=None` in csproj; `android:extractNativeLibs="true"` in manifest.
5. `ALL entries in APK named lib/x86_64/ MUST be STORED` — misleading; real cause was #4.
6. `MAUIR0004 HarfBuzzSharp.Font.SetVariations` — SVGs with `<text>` break Resizetizer; all icons are pure-`<path>`. NEVER reintroduce text in SVGs.
7. `XamlParseException: StaticResource not found for key Radius.MD` — App.xaml merge order must be Colors → **Shapes** → Typography → Controls.
8. `XamlParseException: No parameterless constructor for InboxViewModel` — never `<vm:X />` in XAML; pages take VMs via constructor injection (`BindingContext = viewModel` in code-behind).
9. `SqliteException: no such column: is_read` — migrations 4 (and 5, wallet schema §13.8) in `src/Alak.Storage/StorageConnection.cs`.
10. `INSTALL_FAILED_USER_RESTRICTED` — MIUI needs "Install via USB" + "USB debugging (Security settings)"; also retry once to clear the on-phone prompt.

### UI-redesign era (`6b5619e`, 2026-10-15/16, physical device)
11. **`Label.LineHeight` is a multiplier, not CSS pixels.** Old styles carried 18–36 → 18–36× spacing → clipped/floating text, giant dead zones on EVERY screen (this was "most of the elements are not rendering correctly"). Now 1.0–1.3, tuned for Vazirmatn's tall Persian metrics (1.5 still looks airy). Also: register per-weight font families (`VazirmatnMedium`/`VazirmatnSemiBold`) — Android fake-bold on the Regular family is ugly; TTFs live in `Resources/Fonts/`.
12. **Stray top-level ShellContents collapse the TabBar.** `AppShell.xaml` had 7 `ShellContent`s after the `TabBar` (duplicating `Routing.RegisterRoute` entries) → tab bar rendered with ONE visible tab. Rule: TabBar owns tab routes (inbox/scan/wallet/rules/settings); ONLY modal pages (detail, onboarding, stub-detail) go through `Routing.RegisterRoute`.
13. **Theming done right.** `Color.AppThemeBinding` nested inside `<Color x:Key>` is INVALID (renders black — the earlier black-screen fix). The working pattern: plain color resources + `AppThemeBinding` inside style SETTERS + `Application.UserAppTheme`. `ThemeManager` now ONLY sets `UserAppTheme` and persists the choice; the old runtime resource-rewrite hack could never work (StaticResource resolves once at XAML load).
14. **ZXing.Net.Maui 0.10.4 API.** `CameraBarcodeReaderView` has NO `IsDetectingState` property and the event is `BarcodesDetected` (plural). Either mistake = `XamlParseException` → app exits at tab switch (looks like "app vanished"). ZXing handlers registered manually in MauiProgram (no Use-extension).
15. **Color source-of-truth.** Four divergent palettes existed (VM Tailwind hexes, DesignTokens Material hexes, converter hexes, Colors.xaml). Now single source: `Colors.xaml` primitives (Ink/Salt/Night/… + per-theme category marks/tints per SPEC §12.2, gold wallet accent) — VM/converters only supply category KEYS where feasible; `NotificationViewModel.GetMarkColor/GetTintColor` return spec marks + alpha washes (`#24xxxxxx`) that read on both themes.

---

## 5. Build / deploy / verify commands (all verified working)

```powershell
# Environment (EVERY shell): identity for commits + NuGet env + full-path dotnet
$env:APPDATA='C:\Users\SaintScraTchY\AppData\Roaming'; $env:LOCALAPPDATA='C:\Users\SaintScraTchY\AppData\Local'

# Build for the phone (arm64):
& 'C:\Program Files\dotnet\dotnet.exe' build src/Alak.App/Alak.App.csproj -c Debug -f net10.0-android36.0 -r android-arm64 -p:AndroidSdkDirectory=M:/Android/Sdks
# APK: src/Alak.App/bin/Debug/net10.0-android36.0/android-arm64/com.alak.app-Signed.apk
# (emulator builds: -r android-x64)

$adb='M:\Android\Sdks\platform-tools\adb.exe'
& $adb devices                       # expect 5a8c869e  device
& $adb -s 5a8c869e install -r <apk>  # retry once if USER_RESTRICTED
& $adb -s 5a8c869e shell am start -W -n com.alak.app/crc64a44735e8a764ea7f.MainActivity

# Verify: screenshots (NEVER `>` redirect), UI dump, crash check
& $adb -s 5a8c869e shell screencap -p /sdcard/a.png; & $adb -s 5a8c869e pull /sdcard/a.png out.png
& $adb -s 5a8c869e shell uiautomator dump /sdcard/ui.xml
& $adb -s 5a8c869e logcat -d -b crash          # Java + native crash buffer
& $adb -s 5a8c869e logcat -d | Select-String 'FATAL|monodroid'

# Theme switching on device (app theme pref = "system"):
& $adb -s 5a8c869e shell cmd uimode night yes|no

# Tests (VSTest broken on .NET 10 — ALWAYS dotnet run per project):
foreach ($t in 'Alak.Core.Tests','Alak.Rules.Tests','Alak.Text.Tests','Alak.Storage.Tests','Alak.Adaptive.Tests','Alak.Inference.Abstractions.Tests') {
  & 'C:\Program Files\dotnet\dotnet.exe' run --project "tests/$t" }
```

App id `com.alak.app`; MainActivity hash `crc64a44735e8a764ea7f` (stable for the class FQN).

---

## 6. Remaining work (do NOT assume anything here is done)

**Immediate (next session):**
- [ ] **Verify Scan tab on device** — `BarcodesDetected` fix is committed but unverified (phone unplugged). Expect: camera preview renders, no crash; camera-denied state shows the new card.
- [ ] Walk detail routes: `detail?id=`, `stub-detail?id=`, Sources — restyled but never opened on device yet.

**M8 remainder:**
- [ ] NotificationListenerService platform implementation (capture side) — not written; no manifest service entry (correct until the class exists).
- [ ] Onboarding end-to-end: steps 3–5 (notification-access grant incl. MIUI "Restricted setting" path §13.1, OEM battery guidance, capture depth).
- [ ] Inbox day-group headers (§13.2) — needs a grouped collection on `InboxViewModel` (VM change: agree scope with the user first — logic integration is deliberately deferred).
- [ ] Stub expiry-drain fraction (needs a fraction property on `PromoCodeItem`; drain animation §12.7 states).

**Later milestones:** M7 leftover Backup/Restore §11.5; SkiaSharp **3.x only** when barcode rendering §13.8 happens (4.x breaks Resizetizer); RTL/UX-001 mirroring pass + snapshot tests; a11y §12.9 (touch targets ≥44, focus ring, reduced motion); M9 SLM; M10 corpus hardening; M11 release.

**Polish backlog (small):** `StepToColorConverter` inactive color is static light hairline (fine on light, slightly bright on dark); Shell tab switching has first-creation lag (~1.5 s) — consider eager tab warming; `Text.Code` CharacterSpacing is 1.5 (spec says +0.5px — verify rendering before changing).

---

## 7. Delegation prompt (copy-paste for the next child agent)

> **Goal:** Continue Alak M8 on the physical device. FIRST: verify the committed Scan-tab fix (`BarcodesDetected`, commit `6b5619e`) — install, tap Scan, confirm live camera preview with no crash. THEN walk the pushed routes (`detail?id=`, `stub-detail?id=`, Sources) and fix any rendering/crash defects found. The UI was redesigned on the SPEC §12 design system (verified on Inbox/Codes/Rules/Settings/Onboarding, light+dark) — do not re-litigate it; preserve its patterns.
>
> **Context:** Repo root `M:/Hermes/Alak` (Windows sandbox, PowerShell 5.1). Read, in order: `docs/spec/SPEC.md` (§12 design system, §13 screens), `docs/AGENT-HANDOFF.md` (environment cheatsheet §2, debugging history §4, commands §5, remaining work §6), `docs/HANDOFF-STATUS.md` (conventions §4). HEAD `6b5619e` on `main`, pushed. Tests 893/893 green. OUT OF SCOPE: classification-logic/inference integration (stub is intentional until M9), NotificationListenerService, Backup/Restore §11.5, SkiaSharp/barcode rendering — report them as open items, do not implement.
>
> **Hard environment facts (details in AGENT-HANDOFF §2):** use `C:\Program Files\dotnet\dotnet.exe` (PATH dotnet has no SDK); set `$env:APPDATA`/`$env:LOCALAPPDATA` before any build or NuGet dies with `path1` null; every commit MUST override the injected zwork identity to `SudoScraTchY <SainScraTchY@gmail.com>` in the same shell; adb at `M:\Android\Sdks\platform-tools\adb.exe`, device `5a8c869e`, MIUI retries installs and steals taps; never redirect `screencap` through PowerShell `>`; tests via `dotnet run` per project (VSTest broken); edits need exact CRLF/LF matches.
>
> **Conventions:** ViewModels via constructor injection (NEVER `<vm:X />` in XAML); App.xaml merge order Colors→Shapes→Typography→Controls; `LineHeight` values are multipliers (1.0–1.3, never pixel numbers); modal routes only via `Routing.RegisterRoute` (tabs own their routes); themed colors via `AppThemeBinding` in setters; SVGs contain no `<text>`; sentence-case labels; `الک` watermark for empty states; category colors ONLY from `Colors.xaml` tokens.
>
> **Tasks:** (1) device-verify Scan + pushed routes; (2) fix what breaks, rebuild (`-r android-arm64`), redeploy, re-verify; (3) keep all 6 test suites green; (4) commit per logical fix with conventional messages and `git push origin main` after each verified-green state; NEVER force-push.
>
> **Anti-fabrication contract:** "NOT RUN" is an acceptable answer; invented numbers are not. Report only metrics, test counts, commit hashes, and log excerpts you actually produced. End the report with `git status` and `git log --oneline -5` output.

## 8. Commit cadence rule (user instruction, 2026-10-09)

Push to GitHub periodically — commit per logical fix and `git push origin main` after each verified-green state. Do not accumulate large uncommitted changesets. On any mystery breakage, run `git status`/`git diff` FIRST before editing (INCIDENT 2026-09-26: RulesEngine.cs wiped mid-session). Pre-existing untracked files (`ob.jpg`) and agent scratch (`.hermes/`) are never staged.
