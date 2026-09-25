<!-- Linked requirement ID(s) (required, spec §0.2): e.g. CAP-006, RUL-003 -->
**Requirements:** 

<!-- For any rule/extractor change: a corpus case that fails before this PR (required, QLT-005) -->
**Corpus case added:** 

- [ ] `dotnet build Alak.slnx` — zero warnings (ARC-004)
- [ ] `dotnet test Alak.slnx` — green (ARC-001)
- [ ] No new network capability anywhere (R2 — CI privacy-guard enforces)
- [ ] Time via IClock, no DateTime.Now in logic (ARC-003)
