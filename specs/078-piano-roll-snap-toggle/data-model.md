# Data Model: Piano Roll Snap Toggle

No new data entities or persisted fields are introduced.

The control reads and updates the existing `PianoRollGrid` preference:

- `SnapOn` is the only field changed by the dedicated button.
- Adaptive/fixed mode, fixed step, density, and triplet settings are preserved.
- The Piano Roll's Quantize step remains a separate preference and is unchanged.
- The existing setting is app-wide, persisted in app settings, and shared with the audio
  clip editor.

State transitions are the existing on/off toggle: enabled → disabled → enabled.
