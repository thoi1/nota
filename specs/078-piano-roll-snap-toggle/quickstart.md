# Quickstart: Piano Roll Snap Toggle

## Prerequisites

- Windows, macOS, or Linux development environment supported by Nota.
- .NET 10 SDK and the repository's managed dependencies restored.

## Validate in the app

1. Open a MIDI clip in the Piano Roll.
2. Locate the dedicated magnet toggle next to the grid chip.
3. Click it off, then place or drag a note between grid lines. The note should retain its
   off-grid beat position.
4. Click it on and repeat. The note should snap to the current Piano Roll grid.
5. Toggle snap using the existing grid menu or Toggle Snap command. Confirm the button
   state updates while the Piano Roll stays open.
6. Confirm that changing snap does not change the grid step, triplet setting, or Quantize
   value.

## Automated validation

- Run the focused UI regression harness:
  `dotnet run --project specs/078-piano-roll-snap-toggle/ui/PianoRollSnapToggleUi.csproj -c Release`
- Run the existing grid-state tests:
  `dotnet run --project tests/Nota.SmokeTest -c Release -- --grid`

Expected: both commands pass; the UI harness confirms on/off behavior and external state
synchronization, and the grid tests confirm that toggling preserves other grid fields.
