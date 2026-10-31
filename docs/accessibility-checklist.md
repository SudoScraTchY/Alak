# Accessibility pass (SPEC §12.9)

Status: **started 2026-10-31**; items below are the manual pass checklist. Each item must be checked on the physical device (`5a8c869e`) in light and dark, EN and FA (RTL).

| # | Requirement | Status | Evidence / next action |
|---|---|---|---|
| A11Y-1 | Dynamic type up to 130 % without truncation in the Stub | TODO | Styles use fixed `FontSize`; no font-scaling hook. Add scaled resources and test the Stub at 130 %. |
| A11Y-2 | Every interactive control ≥ 44×44 dp | PARTIAL | Tab bar/system controls pass; **fails** for Inbox “Mark read” (text-only label), code “Copy” chip, and category filter pills (height 42). Wrap text actions in ≥44 dp hit areas and re-shoot. |
| A11Y-3 | Visible 2 px focus ring (`ink`/`txt`) for keyboard/switch access | TODO | MAUI default has no focus visuals; add `VisualStateManager` focus states to Buttons, chips and rows. |
| A11Y-4 | Reduced-motion equivalent disables spine draw / expiry pulse | TODO | No `Settings.Global.ANIMATOR_DURATION_SCALE` check; add a helper and gate animations. |
| A11Y-5 | All color pairings meet AA | PARTIAL | Palette contrast table exists (§12.2); verify chip text-on-tint and disabled states with the device in both themes. |
| A11Y-6 | Category never conveyed by color alone | PASS | Every row/chip renders the category glyph/label next to the spine; verify in screenshots. |
| A11Y-7 | TalkBack labels for icon-only controls | TODO | Add `SemanticProperties.Description` to icon buttons and the app icon in rows. |

Definition of done: all rows PASS with a dated note and screenshot/UI-dump evidence, appended to `docs/verification.md`.
