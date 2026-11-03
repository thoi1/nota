<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# UI contract

Arrangement MIDI double-click enters the main editing workspace and focuses notes.
The existing maximize control gains a visible return label while maximized. Audio and
Session activation remain unchanged. Hidden containers consume zero layout height.

Docked close remains; detached Devices gains the same native close glyph. Existing
View controls reopen Devices. Temporary workspace hiding does not change manual visibility.

A divider above each lane captures its own drag; upward grows that lane. Release saves
its preference. A minimum height preserves readable curves. Overflow lanes scroll while
notes retain usable space. Hit testing and stem drawing use exact BeatToX onset.
