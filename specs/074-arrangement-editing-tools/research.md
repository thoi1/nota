# Research

- Decision: reuse the main transport switches and tp-icon style, retaining a visible tool group at narrow widths. Rationale: this is the actual top editing-control location; no duplicate tool controls exist. Alternative: floating palette rejected by request.
- Decision: share an automation stroke implementation while keeping existing Select automation behavior. Rationale: AutoEditor already serves track and return envelopes; footer master uses a separate list and commit API. Alternative: inventing MIDI CC lanes rejected; those exist only in the piano roll.
- Decision: preview points locally, commit once inside an undo group. Rationale: AddAutomationLane plus SetAutomationPoints can otherwise cost two undo steps; engine has no hard lane point cap, so bound generated samples. Preview flags must prevent live automation refresh replacing lists mid-gesture.
- Decision: Nota Warp is state-only with explicit unavailable status. Rationale: existing clip warp APIs render stretched audio; no shared tempo-map/Tempo Track editing engine was found. Alternative: using clip warp would violate the fixed-audio requirement.
- Decision: use the current SnapGrid and preserve the existing clip Select path. Rationale: grid accounts for bars/triplets and Alt bypass; draw cannot overwrite neighbors. No new keyboard bindings.
