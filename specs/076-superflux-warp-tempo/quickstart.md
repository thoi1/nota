# Verification

In matching Visual Studio Developer PowerShell:

```powershell
pwsh scripts/build-win.ps1
ctest --test-dir src/native/nota.engine/build -R '^nota-transient-selftest$' --output-on-failure
dotnet run --project tests/Nota.SmokeTest -c Release -- --transients
```

Focused switch runs only transient tests, exits nonzero on failures. Tests capture imported source through NotaEngine, analyse via existing .NET pool, check synthetic attacks/piano/chords/arpeggios/vibrato/silence/stereo/rates and cache/query/cancel/invalidate/lifetime semantics.

The CTest target compares each spectral ODF frame with committed NumPy/SciPy reference
values and checks duration-independent scratch memory. `tests/fixtures/superflux/generate.py`
regenerates the golden header when NumPy/SciPy are installed; the app/tests need neither.

US2 UI/native edit verification is recorded in [warp-verification.md](warp-verification.md),
including the real-app headless scene and EN/RU docs checks. Start Nota, focus Arrangement,
press 5 and click an audio clip to analyse; enable Audio follows grid and double-click near
a guide, then drag its anchor. US3 variable-clock verification remains pending.
