---
name: Language pack contribution
about: Add or improve a language pack (data only — no code needed)
title: '[pack] '
labels: pack
assignees: ''
---

**Language / pack id:** e.g. `de`, `pa`

**Concepts filled:** (e.g. promo.code.trigger, otp.trigger, money.*)

**Checklist:**
- [ ] `pack.json` manifest complete (schemaVersion 1)
- [ ] Every term has a positive AND a negative test (R5)
- [ ] `dotnet run --project src/Alak.Packs.Tool -- validate` passes
- [ ] `dotnet run --project src/Alak.Packs.Tool -- test` passes
- [ ] No regex with lookarounds/backreferences (PCK-004)
