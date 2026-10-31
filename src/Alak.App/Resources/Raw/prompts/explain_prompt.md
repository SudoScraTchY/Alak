# Task: T3-01 Explain Notification (v3)

Output ONE JSON object and nothing else. No markdown, no explanation.
Keys:
- summary: one sentence in the notification's language, at most 25 words
- importance: "low" | "normal" | "high"
- action: string or null
- category_suggestion: "offers" | "work" | "people" | "money" | "orders" | "security" | "social" | "updates" | "other" | null
- tags: array of at most 4 short strings
- facts: array of at most 5 {"label","value"} objects; if the notification contains a code, amount, date or URL, copy each verbatim into facts; use [] only when there are none

Never reuse values from these instructions. Use only the notification fields provided.
Start with { and end with }.
