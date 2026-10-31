# ADR-0002 — Inference runtime decision (M8 spike)

- **Status:** Accepted for llama.cpp via LLamaSharp (with documented deviations)
- **Date:** 2026-10-31
- **Device:** Xiaomi Redmi Note 11 (`2201117TG`, Snapdragon 680, 5.93 GB RAM, Android 13/API 33, arm64-v8a) — the project's R1-class low-end reference.
- **Related:** SPEC §9.3–§9.5, §14, M8/M9 exit criteria; ADR-0003 (unrelated source display)

## Decision

**llama.cpp (GGUF), wrapped by LLamaSharp 0.30, is the primary Tier-3 engine.** It meets the §9.4 pass gate on R1-class hardware with a 0.5 B Q4 model, keeps the widest model choice (GGUF), and needs no extra runtime beyond the bundled native libraries.

LiteRT-LM was **not evaluated**: no `.litertlm` model or .NET binding path was available within this closure window, and llama.cpp already passes the gate. It remains a possible second engine plugin (§9.5) but is not on the v1 critical path.

## Measured spike results (§9.4 step 2–3)

Model used for the gate: **Qwen2.5-0.5B-Instruct Q4_K_M**, 397,808,192 bytes (379.4 MiB), bundled in app-private storage (not in the APK).

| Metric | Measured | Gate |
|---|---|---|
| Cold load (first run) | 1,046 ms (first-ever load 1,833 ms) | ≤ 15 s |
| Warm load | 1,007–1,011 ms | — |
| Time to first token | 599 ms | — |
| Decode speed | **12.3 tok/s** (32-token run); 11.0–12.0 tok/s across 5 thermal runs | ≥ 3 tok/s |
| Peak PSS | 240,848 KB (~235 MiB); 248,846 KB under the 5-run loop | (no OOM) |
| Thermal after 5 consecutive runs | `THERMAL_STATUS_NONE` (0) throughout | no throttle required |
| Crash count | 0 in the benchmark/golden/cancel/thermal runs | 0 |
| Battery drain / 100 tokens | **Not measured** — device was on USB power | — |
| APK size | 92,968,571 bytes (88.7 MiB) | ≤ 40 MB (hard ceiling 60 MB) — **VIOLATED** |

SmolLM2-135M-Instruct Q4_K_M (105 MB) was measured for comparison: load 923 ms, TTFT 239 ms, **37.4 tok/s**, PSS 246 MiB, but only **70–80 % JSON validity** on the golden suite with the production prompt path, below the ≥ 85 % gate. It is useful as a speed-optimized option but not the golden model.

Golden suite (10 explain: 5 EN + 5 FA; 2 digest), run on device through the production `IModelService` path:

- Qwen2.5-0.5B: **explain 10/10 valid, digest 2/2 valid → 12/12 (100 %)** JSON validity. Target without grammar ≥ 85 %.
- Grounding: 2 of 2 checked claims grounded (most outputs contained no code/amount/date facts, so the sample is small; GroundingGuard unit tests cover dropping hallucinated fields).
- Cancellation (SLM-008): generation stopped **6.5 ms** after `Cancel()` — target ≤ 500 ms.
- Oversized model (SLM-020): a 2 GiB sparse file was refused with “Model file is 2048 MB; the limit is 1536 MB. Choose a smaller model.”; the free-memory formula (`availMem < size × 1.15 + 350 MB`) is active and allowed the 379 MB model with 2.67 GB free.

## Deviations from the letter of §9.4/§9.5 (maintainer decision required)

1. **ENG-004 (C shim contract):** the implementation calls llama.cpp through the LLamaSharp managed binding, not through the specified `alak_load/generate/cancel/free/system_info` C shim. The abstraction (`IInferenceEngine`) is satisfied; the shim is not. Building the tiny shim and pinning the llama.cpp commit (ENG-005/ENG-006) remains open if strict conformance is required.
2. **ENG-005 (build flags/CI shim):** native libraries are LLamaSharp's prebuilt arm64-v8a binaries; this repo does not build them in CI. PF-07 (16 KB page size) is **not** satisfied by the bundled `libllama.so`/`libggml*.so` (build warning XA0141), which blocks Android 16 targets until the libraries are rebuilt.
3. **GBNF grammar:** enabling `DefaultSamplingPipeline.Grammar` in LLamaSharp 0.30 causes an immediate native SIGSEGV in `libllama.so` on this device (sampler init), reproduced once and then removed. The fallback path from SLM-001 is used instead: prompt-only + JSON prefill (`{` seeded as the assistant turn) + tolerant extraction (`JsonValidator`), with one strict retry (SLM-002). The ≥ 95 % “with grammar” row therefore cannot be claimed; the ≥ 85 % no-grammar row is met at 100 % on this suite.
4. **Second device:** the spec asks for R1 **and** one mid-range phone; only the Redmi Note 11 was available. Mid-range numbers are missing.
5. **LiteRT-LM:** not attempted (see Decision).

## Consequences

- Tier 3 stays behind the model-import flow (no bundled weights, no network), default Qwen2.5-0.5B Q4 in the catalog guidance.
- APK size is **over the §14 budget** (88.7 MiB vs 60 MB ceiling); M11 must address this (trimming, asset packaging, possibly `RuntimeIdentifier`-only assets). Recorded as a release blocker, not an M8 failure of the engine gate.
- Grammar support must be revisited with a newer LLamaSharp/native build or the C shim before claiming the ≥ 95 % target; the prompt-only path is validated on-device.
- Idle unload (SLM-023, 60 s), performance profile mapping (SLM-024), free-memory/thermal gate (SLM-020/021), SHA-256 import (MOD-003) and benchmark persistence (`models.benchmark_json`, MOD-006) are implemented with this change.
