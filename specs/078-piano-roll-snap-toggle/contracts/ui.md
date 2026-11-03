# UI Contract: Piano Roll Snap Toggle

## Location and purpose

- A dedicated magnet toggle appears in the MIDI clip editor header beside the existing
  Piano Roll grid chip.
- The grid chip continues to display the active step and open the grid-options menu.
- The toggle changes snap state directly and does not open the menu.

## State and behavior

- Checked/active means snapping is on; unchecked/inactive means snapping is off.
- Clicking the toggle changes only the existing Piano Roll `SnapOn` setting.
- The displayed toggle state follows changes from the grid menu, Toggle Snap command, or
  other existing setting updates while the control is visible.
- Turning snap off allows existing note-editing gestures to retain free beat positions;
  turning it on restores snapping to the currently selected grid step.
- Grid step, adaptive/fixed mode, density, triplets, and Quantize are not modified.

## Accessibility and localization

- The control has a localized accessible name and tooltip that identify its snap-to-grid
  action and current state.
- The active state uses the existing Studio selected-control treatment and remains
  distinguishable when inactive.
