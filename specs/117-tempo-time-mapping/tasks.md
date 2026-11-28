# Tasks: Interactive Tempo Time Mapping

## Setup

- [x] T001 Trace the existing tempo and warp test fixtures in `tests/Nota.SmokeTest/Program.cs` and `src/native/nota.engine/src/engine/Engine_Automation.cpp` against [quickstart.md](quickstart.md).

## Foundations

- [x] T002 Implement the local anchor solver in `src/native/nota.engine/src/engine/TempoMap.h` with two constant spans, fixed splice sides, and validation for finite targets, 20–300 BPM, ordered beats and the existing point limit.
- [x] T003 Add a preview transaction in `src/native/nota.engine/src/engine/Engine_Automation.cpp` and `Engine.h` that derives each candidate from the starting snapshot and rejects stale or conflicting edits.
- [x] T004 Expose begin, preview and finish in `src/native/nota.engine/include/nota/nota_engine.h`, `src/managed/Nota.Application/Engine/IAudioEngine.cs` and Nota.Infrastructure interop.

## User Story 1 — Align a beat with a performance

Independent check: drag an interior beat to a transient, observe live grid
feedback, then verify audio timing and MIDI beat positions.

- [x] T005 [US1] Add optional retained audio timing with absolute clip edges and effective clip-local time in `src/native/nota.engine/src/engine/Track.h` and populate it from the drag-start graph in `Engine.cpp`.
- [x] T006 [US1] Integrate retained timing into playback, seek, clip envelopes, reverse and warp cache reuse in `src/native/nota.engine/src/engine/Engine_Render.cpp`, `RenderGraphExecutor.cpp` and `Engine_Tracks.cpp`. Keep preparation off the render thread.
- [x] T007 [P] [US1] Use the retained audio edges for timeline indexing in `src/native/nota.engine/src/engine/TimelineIndex.cpp` and display through the clip info read by `src/managed/Nota.App/ArrangementView.cs`.
- [x] T008 [US1] Round-trip retained audio timing through `src/managed/Nota.Infrastructure/Persistence/ProjectModel.cs` and its loader/writer. Legacy clips default to the existing timing behavior.
- [x] T009 [P] [US1] Add contextual Ctrl-drag capture, guide and readouts in `src/managed/Nota.App/ArrangementView.AutoEditor.cs` and `ArrangementView.TempoMapping.cs`. Use a frozen pointer conversion and draw from the current preview map.

## User Story 2 — Edit one region and recover the original map

Independent check: map one beat between fixed anchors, repeat with snap off,
cancel one edit and undo and redo another.

- [x] T010 [US2] Constrain snapped and free destinations using the frozen axis in `src/managed/Nota.App/ArrangementView.TempoMapping.cs`. Keep the last valid preview when no legal snap point exists.
- [x] T011 [US2] Add one-step commit from the baseline, no-op and cancellation in `src/native/nota.engine/src/engine/Engine_Automation.cpp`. Include retained audio timing in the undo graph.
- [x] T012 [US2] Finish or cancel the pointer lifecycle on release, Escape, capture loss and context changes in `src/managed/Nota.App/ArrangementView.AutoEditor.cs`.
- [x] T013 [US2] Cover audio alignment, fixed anchor times, persistence, unchanged MIDI beats and zero-history cancellation through the C ABI in `tests/Nota.SmokeTest/TempoMappingTests.cs`.

## Integration

- [x] T014 [P] Add localized gesture help in `src/managed/Nota.Application/Input/ShortcutLayout.cs` and `src/managed/Nota.Application/Localization/en.json`.
- [x] T015 [P] Document the gesture in `FEATURES.md`, `CHANGELOG.md` and the English and Russian Automation and Shortcuts manual pages in `../nota-docs`.
- [x] T016 Run the Windows build and exercise the quickstart through the native smoke suite and the headless Arrangement. Reconcile the finished work with the spec.
- [x] T017 Before enabling remap commits in the UI, project retained warped waveform peaks through the original timing basis in `src/managed/Nota.App/ArrangementView.LaneControl.cs` and `src/native/nota.engine/src/engine/Engine_Tracks.cpp`.

## Dependencies and rollout

T002 → T003 → T004 establishes the contract. After T005, native playback
and timeline display can proceed separately. After T004, UI feedback can be
built in parallel with the audio work. T010–T012 complete the transactional
workflow before T013 and T016. T014 and T015 can proceed independently once
the gesture is stable. The first usable slice is live alignment with verified
audio timing, followed by constrained snapping and the complete undo lifecycle.

## Verification

- The Windows build, scanner checks, and full smoke suite passed.
- The focused tempo suite covered render parity, fixed anchors, MIDI beat
  positions, history, conflicting edits and saved timing.
- A headless Arrangement run exercised Ctrl-drag preview, Alt bypass, fixed
  snapping, release, cancel and undo. Physical pointer input on macOS and Linux
  was not tested in this run.
