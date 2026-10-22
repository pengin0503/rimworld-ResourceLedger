# Resource Ledger 0.2.1 review fixes

> **For agentic workers:** Use superpowers:executing-plans. Execute in this session without subagents, as required by docs/DESIGN.md.

**Goal:** Review the released 0.2.0 and fix reproducible defects, then publish a verified patch release.

**Architecture:** Preserve schema 1 and the existing core/game/UI boundaries. Add behavioral regression tests before each fix; keep private game references outside the repository.

**Tech Stack:** C#/.NET 10, RimWorld 1.7, Harmony, Python, GitHub Actions.

**Spec:** docs/DESIGN.md plus the user's approved 0.2.0 changes and autonomous review/fix request.

## Global constraints

- No subagents; no approval checkpoint: the user explicitly authorized completion using our judgment.
- Keep both old plain JSON and RLZ1 saves readable; invalid saves must be preserved read-only.
- Do not publish game/Unity/Harmony reference binaries.
- Actual engine GUI/playtesting is unavailable and must not be claimed.

## Review focus

- Null nested data, duplicate sample ticks and invalid series keys must not crash load/merge.
- Daily retention must preserve gaps between recorded periods.
- Failed storage reads must suppress forecasts and mark the next recovered interval as a gap.
- A pawn standing on a stockpile must not move its carried/equipped inventory into the stored scope.
- Exact gross ingredient consumption must remain visible in stored/custom scopes; CSV windows with no samples must not invent zero stock.

## Tasks

- [x] 1. Add malformed-save regression cases; run RED; validate all persisted collections and chronological series; run GREEN.
  Files: src/Core/Ledger.cs, tests/ReviewTests.cs. Interface: LedgerSerializer.TryLoad(string,out Ledger?). Verify: portable test executable, then the complete suite.
- [x] 2. Add daily-gap and failed-read recovery regressions; run RED; preserve adjacency in Trim and add Ledger.MarkGap(string map), used for unreadable scans and failed captures; suppress forecasts while PendingGap; run GREEN.
  Files: src/Core/{Ledger,Queries}.cs, src/Game/{LedgerGameComponent,OperationCapture}.cs, tests/ReviewTests.cs.
- [x] 3. Add actual-API snapshot tests for carried items on storage cells and same-resource recipe turnover in stored/custom scopes; run RED; centralize scope membership and project consumed ingredients using the pre-operation locations; run GREEN.
  Files: src/Game/{InventoryReader,OperationCapture,CapturePatches}.cs, src/UI/LedgerWindow.cs, tests/ContractTests.cs.
- [x] 4. Add CSV no-history regression; run RED; skip series without a sample in the requested export window; run GREEN.
  Files: src/Core/CsvExport.cs, tests/ReviewTests.cs.
- [x] 5. Review the final diff separately, run the full release build, all core/API tests and Python/artifact/package checks. Record each finding, evidence and limitations in REVIEW.md / VALIDATION.md. Bump to 0.2.1, publish via existing verified CI and confirm release assets.

## Progress

Baseline: 69/69 C# tests passed on 2026-10-23. Remote main = 13b3592642e62e5c8dd4b4f8297b2f7f7124dac9. Working on fix/review-0.2.1 in the existing task-only checkout.

Ruling: Follow the existing no-subagent spec and user's autonomous execution request. No additional worktree or planning approval is needed; the existing cloud checkout has no user changes.

Tasks 1–4 complete: original failures reproduced before fixes; 70 portable tests green. Additional final-pass finding: JSON numeric overflow admitted infinity; regression RED→GREEN. Scope projections also exclude consumption of items absent from the pre-operation inventory.

Final review: self-review, following the no-subagent spec. Inspected all core/game/UI files and packaging/CI; checked scan-failure propagation, scope consistency, old-save compatibility and source/binary fingerprints. No newly identified defect remains unaddressed. Engine/UI/play/save-upgrade/TPS validation remains unavailable, documented as a release limitation.

Task 5 complete: Release 0.2.1 published; CI 39805381933 passed both verify and release. Release build 0 warnings/errors; 84/84 C# tests and 4/4 Python tests passed.

Final distribution follow-up (#7): the install ZIP lacked REVIEW.md referenced by its README. A real-package Python test failed before adding the document; all 5 Python tests passed afterward. Preserve published 0.2.1 and publish 0.2.2 with the package correction. The game logic is unchanged from reviewed 0.2.1; the assembly version advances with the release.
