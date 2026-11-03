<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Quickstart validation

Build using `pwsh -File scripts/build-win.ps1` from an x64 Visual Studio developer shell.
Run the feature's actual Avalonia probe after Release build. Verify:

1. MIDI activation while playing enters main workspace, focuses notes, hides Mixer/Devices;
   return restores docked visibility/content/height and detached hosts without recreation.
2. Close/reopen both Devices layouts; repeat with Devices manually hidden before workspace.
3. Resize Velocity and two CC lanes; compare MIDI callbacks and undo history before/after;
   reopen editor and restart preference reader to verify persistence and normalized heights.
4. Verify note/stem timeline alignment at zooms 6, 400, 1600 and 3200, fractional scroll,
   several grid steps and logical display scales 100/125/150/200 percent.
5. Verify anchored extreme zoom, accurate scroll limits and short/narrow layouts.
6. Compare theme hash, inspect Logic Grey screenshot and run existing build/smoke guards.

Record available high-refresh and monitor hardware honestly; do not claim untested setups.
