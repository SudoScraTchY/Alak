# Sprint 1 Handoff: Core Engine Hardening, Security & SIMD Vectorization

**Status:** Completed & Fully Verified
**Date:** 2026-10-28

## Changes Implemented
1. **Security & Zero-Cloud Invariant (STO-006 / OSS-004):**
   - In `src/Alak.App/Platforms/Android/AndroidManifest.xml`, updated line 17 to set `android:allowBackup="false"`. This guarantees that private encrypted databases and preferences are never automatically backed up to Google Drive or extracted via `adb backup`.

2. **SQLite Error 26 & Silent Database Deletion Fix:**
   - In `src/Alak.App/Services/SecureKeyProvider.cs`: Removed silent fallback to default password on Android. Failures throw `CryptographicException` / `InvalidOperationException` to prevent masking KeyStore state issues. Added `ReinitializeKey()` to allow safe recovery on key invalidation.
   - In `src/Alak.Storage/ISecureKeyProvider.cs`: Added `bool ReinitializeKey() => false;` contract.
   - In `src/Alak.App/MauiProgram.cs`: Storage factory never deletes or renames `alak.db` without first attempting KeyStore re-initialization and retrying decryption.

3. **SIMD Vectorization & SearchValues Fast Paths:**
   - In `src/Alak.Rules/ConceptMatcher.cs`: Vectorized ASCII alphanumeric boundary checking using `SearchValues<char>` (`s_asciiAlphaNumeric`) and `Vector128<ushort>` (`ContainsAlphaNumericSimd`).
   - In `src/Alak.Text/Normalization.cs`: Added `FoldSimd(Span<char>)` utilizing 128-bit NEON/SSE2 vectors for bulk ASCII lowercase and Persian/Arabic-Indic digit folding. Optimized `FoldChar` with `SearchValues<char>` (`s_whitespaceFold`) and inline ASCII/digit branch shortcuts.

## Verification Records
- `tests/Alak.Rules.Tests`: 1,300 / 1,300 passed (0 errors, 0 failures, ~3.56s)
- `tests/Alak.Storage.Tests`: 32 / 32 passed (0 errors, 0 failures, ~20.92s)
- `tests/Alak.Text.Tests`: 148 / 148 passed (0 errors, 0 failures, ~0.19s)
