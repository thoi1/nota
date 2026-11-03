# Data model

Extra controller lane: editor-local Control plus independent controller ID and row controls.
Controller curves remain the active clip's engine-owned AutomationPoint arrays; no copying
or deletion on lane removal. Layout survives reload within an editor, not application restart.
