<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Data model

Workspace snapshot is transient: old detail content, visibility, tab choice and detached
window reference. Existing desired row heights and Mixer state remain owned by MainWindow.
State: split/floating → main workspace → previous split/floating. Repeated MIDI activation
must not overwrite the snapshot. Shutdown closes both visible and temporarily hidden windows.

LaneHeight preferences map stable keys (velocity, expression dimension, CC number) to
logical heights. Defaults remain 72/112. Normalize finite values to 48–480 logical pixels.
Save only completed divider gestures; resizing never calls note/controller edit callbacks.
