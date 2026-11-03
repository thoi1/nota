# Data model

- ArrangementTool enum: Select, Pencil, Eraser, WaveformDraw, NotaWarp. Exactly one value, default Select. Per-view transient state.
- AutomationWaveform enum: Sine, Triangle, Sawtooth, Square, Freehand.
- Waveform settings: amplitude [0,1], positive period in beats, phase in cycles [0,1). Defaults 1, one bar, 0.
- Stroke preview: owner control, point-list identity, immutable source points, snapped anchor/current beat, captured grid/modifiers/settings, value range and y geometry, commit callback. Press starts; motion previews; release commits once; Escape/deactivate/tool switch restores source.
- Clip preview: instrument track identity, starting gap bounded by neighbors, grid step, anchor/current endpoints. No engine mutation before release.
