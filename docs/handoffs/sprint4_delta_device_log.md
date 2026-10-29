# Sprint 4 Handoff: Device Verification & Showcase Log

**Status:** Completed & Successfully Verified on Physical Hardware
**Date:** 2026-10-28
**Physical Target Device:** Xiaomi Redmi Note 11 (`2201117TG`, serial `5a8c869e`, ARM64-v8a, Snapdragon 680, Android 13 API 33 / MIUI 14)

## Execution & Deployment Record
1. **ADB Transport Configuration:**
   - Host Windows Hyper-V port exclusion (`4968 - 5067`) conflicted with default ADB port 5037 (WSAEACCES 10013).
   - Resolved by configuring `$env:ANDROID_ADB_SERVER_PORT = 5555`, establishing clean USB ADB transport to physical device `5a8c869e`.

2. **Package Deployment & Cold Boot:**
   - Deployed signed APK: `src/Alak.App/bin/Debug/net10.0-android36.0/android-arm64/com.alak.app-Signed.apk`.
   - Cold boot activity launch: `com.alak.app/crc64a44735e8a764ea7f.MainActivity` (Status: ok, LaunchState: WARM/COLD, TotalTime: 3156ms).
   - Crash inspection (`logcat -d -b crash`): **0 crashes, 0 errors**.

3. **Tab Navigation & UI Verification:**
   - **Inbox Tab (`live_screen.png`):** Populated dark-mode inbox with live notifications, Persian category mark spines, Vazirmatn typography, and promo code chips ("MGFVX Copy").
   - **Codes Wallet Tab (`live_wallet.png`):** Ticket stubs with perforated dashed borders, barcode renderers, copy buttons, and action bars.
   - **Daily Digest Tab (`live_wallet.png` initial state):** SLM/Rules instant digest with financial and orders summary.
   - **Settings Tab (`live_settings.png`):** Global settings, language, calendar, theme, and SLM engine switches.

4. **Showcase Frames Captured & Available in `.hermes/screenshots/`:**
   - `screen_ob10.png` (Onboarding language screen)
   - `inbox_promo.png` (Populated dark-mode inbox)
   - `wallet_screen.png` (Codes wallet card)
   - `combobox_opened_final.png` (Settings combobox overlay)
   - `live_screen.png` (Live verified Inbox on device `5a8c869e`)
   - `live_screen_v2.png` (Live verified MainActivity warm boot post-rebuild)
   - `live_wallet.png` (Live verified Codes wallet on device `5a8c869e`)
   - `live_settings.png` (Live verified Settings on device `5a8c869e`)

5. **Host Developer Showcase Video Generation (`gemini-omni-flash-api`):**
   - Verified that `GEMINI_API_KEY` is currently not configured in the host environment. Once the developer configures their API key, the host generation script `generate_video.py` can produce the 1080p 9:16 walkthrough videos.
