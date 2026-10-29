# Sprint 4 Epsilon: Developer Showcase Video Generation Status

**Status:** Ready for Execution (Awaiting Host `GEMINI_API_KEY`)
**Date:** 2026-10-28

## Context & Zero-Network Compliance
Video generation runs strictly on the developer host machine (Windows 10/11) using the `gemini-omni-flash-api` skill and `google-genai` SDK. The Android mobile application (`com.alak.app`) maintains its zero-network invariant (no INTERNET permission, no network stack).

## Prerequisites
1. Install SDK:
   ```powershell
   & "C:\Users\SaintScraTchY\AppData\Local\Programs\Python\Python314\python.exe" -m pip install -U google-genai
   ```
2. Configure developer API key:
   ```powershell
   $env:GEMINI_API_KEY = "<YOUR_GEMINI_API_KEY>"
   ```

## Video Generation Commands
### 1. Onboarding-to-Inbox Walkthrough (1080p, 9:16)
```powershell
py -3 "C:\Users\SaintScraTchY\.gemini\config\plugins\gemini-api\skills\gemini-omni-flash-api\scripts\video\generate_video.py" `
  "A smooth cinematic mobile app UI walkthrough of Alak notification organizer. Starting from the display language setup screen, the user taps Next and transitions into the populated dark-mode inbox showing categorized Persian notification cards with category mark spines and promo code chips. Smooth animation, crisp typography, no scene cuts." `
  --first-frame "m:\Hermes\Alak\.hermes\screenshots\screen_ob10.png" `
  --last-frame "m:\Hermes\Alak\.hermes\screenshots\inbox_promo.png" `
  --aspect-ratio "9:16" `
  --resolution 1080p `
  --duration 6 `
  --output "m:\Hermes\Alak\assets\media\onboarding_to_inbox_1080p.mp4"
```

### 2. Codes Wallet Walkthrough (1080p, 9:16)
```powershell
py -3 "C:\Users\SaintScraTchY\.gemini\config\plugins\gemini-api\skills\gemini-omni-flash-api\scripts\video\generate_video.py" `
  "A modern mobile UI showcase of Alak Codes Wallet. Focus on the YALDA50 promo code stub with dashed border, tapping Copy to trigger a subtle feedback ripple, then smoothly transitioning into the settings options combobox overlay. Clean typography, no extra sound effects." `
  --first-frame "m:\Hermes\Alak\.hermes\screenshots\wallet_screen.png" `
  --last-frame "m:\Hermes\Alak\.hermes\screenshots\combobox_opened_final.png" `
  --aspect-ratio "9:16" `
  --resolution 1080p `
  --duration 5 `
  --output "m:\Hermes\Alak\assets\media\wallet_promo_showcase_1080p.mp4"
```
