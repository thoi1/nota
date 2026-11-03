# Bug Verification: Tempo track does not follow playhead scroll

- **Slug**: tempo-track-playhead-scroll
- **Tested**: 2026-11-03
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

The headless MainWindow reproduction moved the Arrangement with Follow and confirmed the
Tempo Track's rendered pixels updated to the new shared scroll position. The regression
assertion also failed when the invalidation fix was temporarily removed, confirming it
detects the original bug.

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Reproduction (post-fix) | Set Follow, move the playhead through `SetPlayheadFrame(60, 60, true)`, then compare rendered Tempo Track pixels. | pass | Automated UI equivalent of playback-driven Follow; shared scroll moved and the tempo lane redrew. |
| New / updated tests | `dotnet run --project specs\077-collapsible-tempo-track\ui\TempoTrackUi.csproj -c Release -p:BaseOutputPath="$env:TEMP\nota-tempo-track-build\bin\"` | pass | All 13 UI harness checks passed, including the new Follow-scroll check. |
| Regression suite | Full solution smoke/build suite | skipped | The focused headless UI harness covers the changed behavior; a broader suite was not run. |
| Lint / type-check | The UI harness build within the command above | pass | C# compilation succeeded. `git diff --check -- src/managed/Nota.App/ArrangementView.cs` also passed. |

## Output Excerpts

```text
PASS tempo lane redraws when Follow scrolls the Arrangement
PASS 10,000-point actual lane reload/render (39.5 ms including 30 ms dispatcher pump)
```

With the invalidation removed, the same regression assertion failed with:

```text
Tempo lane did not redraw when Follow scrolled the Arrangement
```

## Residual Risks

- The test drives the production Follow display-update path in a headless MainWindow; manual playback in a running audio session was not performed.
- The default output path was locked by the already-running Nota app, so the passing harness used an isolated build output directory.

## Recommendation

Close the bug as verified by the focused UI reproduction and regression check. If convenient,
confirm once in a live playback session; the shared Follow path is otherwise covered.
