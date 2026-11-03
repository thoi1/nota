# Bug Fix: Tempo track does not follow playhead scroll

- **Slug**: tempo-track-playhead-scroll
- **Fixed**: 2026-11-03
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

Invalidated the Tempo Track when playback Follow changes the Arrangement's horizontal
scroll, so its retained drawing follows the shared viewport. Added a headless UI regression
check that confirms the visible tempo lane pixels update after Follow scrolls.

## Changes

| File | Change | Notes |
|------|--------|-------|
| `src/managed/Nota.App/ArrangementView.cs` | modified | Invalidate `_tempoLane` alongside `_lanes` only when Follow scrolls. |
| `specs/077-collapsible-tempo-track/ui/Program.cs` | modified | Capture the lane before/after a Follow scroll and assert its rendered pixels change. |

## Tests Added or Updated

- `specs/077-collapsible-tempo-track/ui/Program.cs` — Follow-scroll regression check; it fails when the new invalidation is removed and passes with the fix.

## Local Verification

- `dotnet run --project specs\077-collapsible-tempo-track\ui\TempoTrackUi.csproj -c Release -p:BaseOutputPath="$env:TEMP\nota-tempo-track-build\bin\"` → passed; all 13 UI harness checks passed, including the Follow-scroll regression.
- `git diff --check -- src/managed/Nota.App/ArrangementView.cs specs/077-collapsible-tempo-track/ui/Program.cs` → passed.
- The default UI harness build could not replace the Release app output because the existing Nota app process (PID 30856) held it open; the isolated output path allowed the harness to build and run without interrupting that process.
- Manual checks: none beyond the headless UI harness.

## Deviations from Assessment

None.

## Follow-ups

- Run `/speckit-bug-test slug=tempo-track-playhead-scroll` for the bug verification report.
