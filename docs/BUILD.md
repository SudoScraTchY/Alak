# Building & Developing Alak

Everything you need to go from clone to green tests to an installed APK. For *why* the code is
shaped the way it is, see [`ARCHITECTURE.md`](ARCHITECTURE.md); for rules of contribution, see
[`contributing/CONTRIBUTING.md`](contributing/CONTRIBUTING.md).

## 1. Prerequisites

| Tool | Pin | Notes |
|---|---|---|
| .NET SDK | **10.0.x** — pinned by [`global.json`](../global.json) (`10.0.301`, `rollForward: latestFeature`) | `dotnet --version` should resolve to 10.0.3xx |
| MAUI workloads | `android` (36.x) | `dotnet workload install android` — needed only to build/run `Alak.App`; all libraries and tests run on plain desktop .NET |
| JDK | 17+ (21 used) | Required by the Android workload |
| Android SDK | platforms 35/36, build-tools 36, platform-tools (`adb`) | The NDK is only needed for the future native llama.cpp shim (`native/llama-shim`) |
| Python 3 | any recent | Only to regenerate corpora deterministically (`tests/corpus/gen_*.py`) |

Libraries and test projects (`Alak.Core`, `Alak.Text`, `Alak.Rules`, `Alak.Storage`,
`Alak.Adaptive`, `Alak.Inference.Abstractions` + their tests) build and run on any desktop OS.
`Alak.App` needs the Android workload.

## 2. Clone, restore, build

```bash
git clone https://github.com/SudoScraTchY/Alak.git
cd Alak
dotnet restore Alak.slnx
dotnet build Alak.slnx -c Release
```

Conventions baked into the build:

- `Directory.Build.props` sets `Nullable=enable`, determinism, and analyzers. **Do not** add package
  versions there — each `.csproj` pins its own exact versions (ADR-0001, to avoid multi-agent merge
  conflicts).
- Project convention ARC-004 asks for a **zero-warnings** build.
- Package versions in `.csproj` files must come from a source you actually opened (NuGet, official
  docs) — never guessed (spec R1).

## 3. Tests

The solution has six test suites, one per library:

```bash
dotnet test Alak.slnx -c Release
```

If `dotnet test` misbehaves on a given .NET 10 install, every test project also runs standalone:

```bash
dotnet run --project tests/Alak.Rules.Tests      -c Release
dotnet run --project tests/Alak.Storage.Tests    -c Release
dotnet run --project tests/Alak.Text.Tests       -c Release
dotnet run --project tests/Alak.Core.Tests       -c Release
dotnet run --project tests/Alak.Adaptive.Tests   -c Release
dotnet run --project tests/Alak.Inference.Abstractions.Tests -c Release
```

What the suites prove beyond unit coverage:

- **Corpus tests** (`Alak.Rules.Tests`) load the *real* packs from `packs/` and run every case in
  `tests/corpus/{en,fa,fa-latn}` — worked examples, generated cases and adversarial hard-negatives.
- **Performance tests** (`Alak.Rules.Tests`, Release) enforce the Tier-1 latency budget (RUL-002).
- **Storage exit-criteria tests** prove 10k-row insert/paginate/FTS/prune plus SQLCipher + FTS5
  with Persian queries (PF-10).
- **`FaLatnActivationTests`** prove the Finglish extension pack can never degrade English
  classification (identical results with and without it).

## 4. Corpus evaluation gate (QLT-003)

The metric harness computes per-category P/R/F1, macro-F1, promo-code precision/recall and OTP
recall over a corpus:

```bash
dotnet run --project tools/Alak.CorpusEval -c Release -- tests/corpus/en/cases.json packs/en/lexicon.json en
dotnet run --project tools/Alak.CorpusEval -c Release -- tests/corpus/fa/cases.json packs/fa/lexicon.json fa
```

(Arg order: `<corpus.json> <lexicon.json> <lang>`; running with no args uses the English defaults.)
Rule or pack changes that regress the gates do not land — the gates are the definition of "the rule
engine still feels like AI".

Corpora are regenerated deterministically (fixed seeds) by `tests/corpus/gen_*.py` — commit the
generator and the data together.

## 5. Language-pack tooling (`alak-pack`)

```bash
dotnet run --project src/Alak.Packs.Tool -- new <id>           # scaffold from packs/_template
dotnet run --project src/Alak.Packs.Tool -- validate packs/<id> # schema + safety (PCK-001, PCK-004)
dotnet run --project src/Alak.Packs.Tool -- test packs/<id>     # run the pack's own tests/
dotnet run --project src/Alak.Packs.Tool -- coverage packs/<id> # per-concept term counts (PCK-006)
dotnet run --project src/Alak.Packs.Tool -- lint packs/<id>     # duplicates, style, conflicts
dotnet run --project src/Alak.Packs.Tool -- bundle packs/<id> [out]  # → .alakpack + SHA-256
```

Coverage gates (v1.0, QLT-004): ≥ 40 terms for the high-value concepts
(`promo.discount.word`, `promo.limited`, `ad.cta`, `otp.trigger`, `money.*`, `orders.status`);
≥ 15 for every other filled concept. Every term needs a test case that fails if the term is
removed (R5).

## 6. Building & running the app

```bash
dotnet build src/Alak.App -c Debug
dotnet run --project src/Alak.App -c Debug        # picks a target; or use -t:Run with a device selected
```

Notes encoded in [`src/Alak.App/Alak.App.csproj`](../src/Alak.App/Alak.App.csproj) — they exist
because of real emulator failures (full history in [`AGENT-HANDOFF.md`](AGENT-HANDOFF.md)):

- `EmbedAssembliesIntoApk=true` and `AndroidUseSharedRuntime=false` — fast-deployment staging is
  flaky on x86_64 emulators; the Debug APK is self-contained.
- `AndroidEnableAssemblyCompression=false` + `AndroidExtractNativeLibs=true` — compressed/stored
  `lib/` entries trip monodroid aborts on API 30 emulators.
- RIDs: `android-arm64` (physical devices) and `android-x64` (emulators).
- Material3 default theme is disabled to avoid missing color resources.
- App identity: `com.alak.app`; display version 1.0.

Install on a device/emulator with `adb install -r <apk>`; the app appears once you grant
**Notification access** during onboarding.

## 7. CI

`.github/workflows/ci.yml` runs two jobs on every push to `main` and every PR:

1. **unit-tests** — restore, `dotnet build -c Release`, `dotnet test` on `ubuntu-latest`
   (ARC-001: tests must run without an emulator).
2. **privacy-guard** — the no-network enforcement described in
   [`PRIVACY.md`](PRIVACY.md) §1. If your change trips it, the change is wrong, not the job.

## 8. Working conventions (the short list)

- Reference requirement IDs (`CAP-xxx`, `RUL-xxx`, `PF-xx`, `QLT-xxx`) in commits, test names and
  PR titles (spec §0.2). Commit style example lives in CONTRIBUTING.
- Time in logic comes from `IClock` only — never `DateTime.Now` (ARC-003).
- New rules and pack terms are **test-first**: positive *and* negative cases before the fix lands
  (R5, QLT-005).
- Unverifiable platform facts get `TODO(verify)` and, once checked, a row in
  [`verification.md`](verification.md) (R1, R8). Real-device evidence only for perf/battery/memory
  claims (R7).
- Meaningful decisions get an ADR in [`adr/`](adr/); package bumps record the reason there too.
- Milestone order matters (spec §16): don't start M(n+1) before M(n) exits green.
