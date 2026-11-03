# Research

Decision: reuse ControllerLane with an instance override for its controller ID, keeping
the primary fifth-tab fallback. Rationale: all curve/switch gestures and undo routing
already exist. Alternative: copy editor logic per lane; rejected as duplicated behavior.

Decision: bounded scrollable extra lanes beneath the primary lane. Rationale: preserve
existing velocity/MPE interactions and shared time axis. No new engine/project format.
Existing picker excludes reserved CCs; the shared cache is invalidated after each write.

Decision: - only removes a view. Clearing remains an explicit existing context action.
