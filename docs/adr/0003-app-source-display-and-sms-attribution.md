# ADR-0003 — App identity display and SMS source attribution

- **Status:** Accepted (2026-10-30)
- **Context owners:** Alak maintainer
- **Related:** SPEC §6.6 (`apps`), §7/CAP-013 (label/icon resolution), §8.6/E-07 (URL extraction), R2 (no network), PF-05 (package visibility), ADR-0002 (reserved for the M8 inference-engine spike)

## Context

Two user-facing problems:

1. Notification rows and the detail screen showed raw package ids (`com.digikala`, sometimes with the channel id). SPEC §13.2 calls for an app icon and a readable source, and CAP-013 already defines the resolution order (PackageManager → package context → humanized package) with a ≤ 96 px cached icon and **no `QUERY_ALL_PACKAGES`**.
2. Ads that arrive as SMS are captured with the SMS app as their package (`com.google.android.apps.messaging`), so the inbox attributes them to "Messages" even when the content is from a shop. E-07 only extracts URLs for highlighting and forbids fetching them; R2 forbids network access entirely.

## Decision

### App identity (CAP-013)

- Resolution happens on the capture binder thread first: `CreatePackageContext(package, IgnoreSecurity)` gives the posting app's `ApplicationInfo` (label + icon) and, as a second choice, the notification's own large/small icon. This works where plain `PackageManager` lookups are blocked by Android 11+ visibility (PF-05) and never leaves the binder thread as an Android object (CAP-003).
- Ingestion then resolves any remaining package once (in-memory cache) via `PackageManager`; the label and a PNG icon (≤ 96 px, longest side) are stored in the existing `apps.label` / `apps.icon_png` columns.
- When neither path resolves the package, the label falls back to `PackageNameHumanizer` and the icon stays null. No new manifest visibility or permission is added.
- A one-time startup backfill fills labels/icons for packages already in the local database.
- Inbox rows and Notification Detail render the resolved label plus icon; the raw package stays visible on Detail for transparency. Wallet source text is unchanged in this ADR.

### SMS source attribution

- The pure `SafeLinkParser` (in `Alak.Core`, no I/O) extracts **registrable domains** from title/text/bigText/lines.
- Notifications posted by the six known SMS packages are *attributed*, never learned:
  - If the domains resolve to exactly one non-communication package with at least **2 sightings** (`app_domains.hits >= 2`), that package is stored in `notifications.attributed_package` and displayed (with a localized “via SMS” chip).
  - Ambiguous (multiple packages), unknown, or under-threshold domains yield no attribution.
- Notifications from every non-communication app teach the index (`host → package`, `hits++`). Messaging apps never teach it, so Telegram/WhatsApp links cannot poison attribution.
- The original `notifications.package` is never rewritten: rules, filters, retention, and provenance remain correct. Attribution is display-only.
- No seeds or bundled brand lists; the index is learned from what the user's own apps post, plus the local database backfill.

## Security invariants (why this cannot execute or fetch anything)

- No network permission exists in the app; this feature performs **no** DNS, HTTP, WebView, intent, or `Process` calls.
- Only `http`/`https` absolute URIs are considered. `javascript:`, `data:`, `file:`, `ftp:`, credentials in the authority, IP literals, localhost, and invalid DNS labels are rejected.
- Hosts are ASCII/punycode-normalized (`Uri.IdnHost`), lowercased, trailing dot removed, `www.` stripped; matching uses the registrable-domain heuristic (last two labels, with a small second-level-suffix guard for `co.uk`-style names).
- Input is bounded: ≤ 10 domains per notification, ≤ 2048 chars per candidate, regex with a 100 ms timeout; no evaluation of link contents.
- Attribution is conservative by construction (unambiguous package, ≥ 2 sightings, SMS senders only) and is never used for security decisions.

## Consequences

- SMS ads with shorteners (e.g. `bit.ly`) stay attributed to Messages until the destination domain is observed from the brand's own app; this is intentional.
- The registrable-domain heuristic is approximate for exotic public suffixes; mismatches only lose an attribution, they never create a false positive across packages.
- Icons add up to ~96 px PNGs per package; backup already excludes cached icons.
- `attributed_package` is display-only; future features that need the brand as a real package (rules/promo wallet) must opt in explicitly and be reviewed separately.
