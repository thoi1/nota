# Data Model: Grid Warp

Grid Warp reuses the project tempo map and existing source-reference metadata. This document describes the logical data; it does not prescribe a parallel timing model.

## Warp Mode

The Sample Editor's current interaction selection.

| Field | Type | Constraints |
|---|---|---|
| Mode | None / Audio follows grid / Grid follows audio | At most one active; selecting the active mode returns to None |

The selected mode is editor interaction state. It is not a tempo-map mutation and is not required to persist in the project. Committed audio warp and Grid follows audio reference state continue to use existing clip/project persistence.

## Transient Analysis Settings and Snapshot

| Field | Type | Constraints |
|---|---|---|
| Sensitivity | normalized number | 0–1 internally; UI displays 0–100%; default 0.5 |
| Minimum spacing | seconds | 0.005–0.250; default 0.030 |
| Source identity | engine sample identity | Immutable source version, owned by existing analysis service |
| Results | ordered transient list | Source sample position and seconds increase monotonically; results match the settings key |

A transient result remains a candidate only. It is not itself a committed Grid Warp anchor.

## Grid Warp Timing Anchor

Represent using the existing tempo-map timing anchor fields where sufficient.

| Logical field | Existing value | Validation |
|---|---|---|
| Musical position | Tempo anchor tick | Nonnegative, unique/strictly ordered among committed map points |
| Absolute audio time | Tempo anchor seconds | Finite, nonnegative, strictly increases with musical position |
| Source position | Source frame | Finite, in source bounds, and monotonic for the referenced playback direction |
| Clip/reference identity | Track, clip, reference id | References the source clip whose attacks define the mapping |
| Fixed status | Locked flag | Fixed anchors constrain neighboring edits; re-analysis does not change them |

The project tempo map may contain anchors from more than one clip. Edits must preserve unrelated references and tempo points outside the affected interval.

## Tempo Event and Map

Use the existing project-owned `TempoMapState`, `TempoPoint`, `TempoCurve`, and `TempoAnchor` structures.

| Field | Type | Constraints |
|---|---|---|
| Tick | musical tick | Ordered, nonnegative; tick zero point retained |
| BPM | finite number | Within map minimum/maximum |
| Outgoing curve | Step for generated intervals | Generated piecewise-constant BPM remains positive and monotonic in elapsed time |
| Map bounds | minimum/maximum BPM | Existing engine limits and validation |
| Anchors | timing-anchor list | All locked anchors must remain consistent with the map |

For adjacent committed anchors `(beatA, secondsA)` and `(beatB, secondsB)`, require `beatB > beatA` and `secondsB > secondsA`; the generated constant interval is `60 * (beatB - beatA) / (secondsB - secondsA)` BPM.

## Edit Lifecycle

1. Mode selection changes only editor interaction state.
2. Pointer movement creates an in-memory candidate and visual preview only.
3. Commit validates all affected intervals, anchors, BPM bounds, and references as one transaction.
4. An accepted edit publishes one project map/reference update and one undoable action.
5. A rejected or cancelled edit discards the candidate and leaves committed map, references, audio, and undo history unchanged.
6. Save/load restores the committed tempo map and reference identities through existing project persistence.

## Completion entities

- SourceTempoSettings: starting/min/max BPM, beat sensitivity, inference version.
- SourceBeat: source frame, beat ordinal, evidence confidence, observed/interpolated status.
- OriginalSourceTiming: immutable source sample rate/region, original detected BPM seed,
  extracted settings/version and source beats. Independent of mutable project tempo/warp.
- BeatPreview: original source/clip identity, source window/absolute origin, proposals and
  fixed constraints; corrections remain local until Apply. Cancel discards it.
- Grid reference: durable reference ID plus source start/end and absolute origin, independent
  of original audio warp markers. GridAnchor ID uses positive reserved int32 POD slot;
  TempoPoint explicit linked-anchor ID uses its reserved int32 slot; zero remains compatible.
- Grid baseline: first mapping's previous region points/anchors, region bounds and original
  clip relationship/geometry; persisted and restored by one validated region Reset.
