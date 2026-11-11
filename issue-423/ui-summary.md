# Playback-cursor comparison for issue #423

Actual Nota App, 1280 x 900, Studio Dark, 100% interface scale, identical generated
MIDI notes and scratch settings. Windows/WASAPI, 48 kHz, 480-frame hardware period.
No user project or saved settings are used. Track volume is zero during sampling.

| Measurement (five-second sample) | Official develop | Playhead fix |
|---|---:|---:|
| Piano-roll cursor changes/s | 22.21 | 234.19 |
| Median cursor gap (ms) | 46.60 | 4.16 |
| P95 cursor gap (ms) | 49.59 | 4.41 |
| Observed animation callbacks | 415 | 1171 |
| Piano-roll cursor changes | 111 | 1171 |
| Arrangement cursor changes | 111 | 1171 |
| Notes/grid draw-list replacements | 110 | 0 |
| Ruler draw-list replacements | 110 | 0 |
| Reported audio dropouts | 0 | 0 |

Counts are cursor-property changes observed on animation callbacks, not physical screen
presentations or a guarantee for every monitor/session. Replacing static drawing is
counted through the actual composition draw-list identity. Input is temporarily disabled
during the cursor-only interval to exclude interactive zoom/hover/edit redraws.

The same-position stopped screenshots are byte-identical: this fixes cursor presentation
cadence without restyling the app. Both images are kept for the before/after comparison.

The probe's after assertions require cursor changes on at least 90% of observed animation
callbacks and zero static grid/ruler replacements. The official base reproduces the defect;
the fix passes those assertions. Windows timing thresholds stay out of CI.
