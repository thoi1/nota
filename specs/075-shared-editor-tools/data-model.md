# Data model

EditorToolState owns one ArrangementTool value (default Select), emits tool changes, and refers to the session-wide SharedWaveformState. SharedWaveformState owns Sine/Triangle/Sawtooth/Square/Freehand plus bounded amplitude/cycle/phase and emits changes. Tool activation and waveform cycling are separate operations; only repeated command 4 in the active context cycles. Shape changes do not cancel another editor's captured gesture.

Focused input context refers to a live editor state, never a duplicate waveform value. Tool-key latches are released on key-up/window loss. Typed/protected contexts never dispatch a tool command.

New note previews snapshot notes/owners/selection and change the local view only; release commits once; cancel restores. Controller previews reuse lane _work/_orig; captured shape/settings/grid stay stable for the gesture. Events map editor beats to the active clip's local timeline, stay in its bounds/range, and retain other data.

No persistence changes. Existing note/controller data is saved by existing project ports. Tool and shape choices are session UI state; editor reparenting retains its instance/state.
