// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nota.App;
using Nota.Application;
using Nota.Infrastructure;
using Nota.Presentation;
using System.Reflection;

string scratch = Path.Combine(Path.GetTempPath(), $"nota-grid-warp-{Guid.NewGuid():N}");
Directory.CreateDirectory(scratch);
Environment.SetEnvironmentVariable("NOTA_DATA_DIR", scratch);
try
{
    AppBuilder.Configure<App>().UseSkia().UseHeadless(new() { UseHeadlessDrawing = false })
        .WithNotaFonts().SetupWithoutStarting();

    {
        int failures = 0;
        foreach (string test in new[] { "add", "ruler", "snap", "source" })
        {
            try { RunAnchorInteractionRegression(scratch, test); Console.WriteLine($"PASS Grid anchor regression: {test}"); }
            catch (Exception ex) { failures++; Console.WriteLine($"FAIL Grid anchor regression: {test}: {ex.Message}"); }
        }
        if (failures > 0) throw new Exception($"{failures} Grid anchor interaction regressions failed");
        if (args.Contains("--anchor-regressions")) return;
    }

    string wave = Path.Combine(scratch, "clip.wav");
    WriteTransientWav(wave, 44100);
    using var engine = new NotaEngine();
    engine.RenderOffline(new float[128], 64, 44100);
    int trackId = engine.AddAudioTrack();
    int clipIndex = engine.AddAudioClip(trackId, wave, 0);
    engine.TryGetAudioClipInfo(trackId, clipIndex, out var clipBefore);
    var transientSnapshot = engine.TransientAnalysis.AnalyzeAudioAsync(clipBefore.SampleId).GetAwaiter().GetResult();
    var targetTransient = transientSnapshot.Transients.OrderBy(t => Math.Abs(t.Seconds - 0.75)).First();
    Require(Math.Abs(targetTransient.Seconds - 0.75) < 0.02, "Grid Warp snap fixture detects the 0.75-second attack");
    float[] sourceBefore = engine.ReadSample(clipBefore.SampleId);
    var mapBefore = engine.GetTempoMap();
    engine.ClearUndoHistory();

    var editor = new AudioClipEditorView(engine, trackId, clipIndex, Brushes.Orange, () => { });
    var window = new Window { Content = editor, Width = 1200, Height = 750 };
    window.Show();
    Pump(100);

    ToggleButton Mode(string name) => window.GetVisualDescendants().OfType<ToggleButton>()
        .SingleOrDefault(b => AutomationProperties.GetName(b) == name)
        ?? throw new Exception($"Grid Warp mode toggle '{name}' not found");
    var noneMode = Mode("None");
    var audioMode = Mode("Audio follows grid");
    var gridMode = Mode("Grid follows audio");
    Require(noneMode.IsChecked == true && audioMode.IsChecked == false && gridMode.IsChecked == false,
        "Grid Warp initializes in None mode");

    Point Center(ToggleButton button) => button.TranslatePoint(
        new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
    void Click(ToggleButton button)
    {
        Point p = Center(button);
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Pump(30);
    }
    void RequireUnchanged()
    {
        var currentMap = engine.GetTempoMap();
        engine.TryGetAudioClipInfo(trackId, clipIndex, out var currentClip);
        Require(currentMap.Points.SequenceEqual(mapBefore.Points)
            && currentMap.Anchors.SequenceEqual(mapBefore.Anchors)
            && currentClip.SampleId == clipBefore.SampleId
            && currentClip.WarpEnabled == clipBefore.WarpEnabled
            && currentClip.WarpBeats == clipBefore.WarpBeats
            && !engine.ClipGridFollowsAudio(trackId, clipIndex)
            && engine.ReadSample(clipBefore.SampleId).SequenceEqual(sourceBefore)
            && !engine.CanUndo,
            "mode-only selection leaves clip audio, timing and undo state unchanged");
    }

    Click(audioMode);
    Require(audioMode.IsChecked == true && !noneMode.IsChecked.GetValueOrDefault()
        && !gridMode.IsChecked.GetValueOrDefault(), "Audio follows grid is the only active mode");
    RequireUnchanged();
    Click(gridMode);
    Require(gridMode.IsChecked == true && !noneMode.IsChecked.GetValueOrDefault()
        && !audioMode.IsChecked.GetValueOrDefault(), "switching modes deactivates the previous mode");
    RequireUnchanged();
    Click(gridMode);
    Require(noneMode.IsChecked == true && !audioMode.IsChecked.GetValueOrDefault()
        && !gridMode.IsChecked.GetValueOrDefault(), "clicking the active warp mode returns to None");
    RequireUnchanged();
    Click(noneMode);
    Require(noneMode.IsChecked == true, "clicking None preserves the None selection");
    RequireUnchanged();

    Click(gridMode);
    Require(engine.GetTempoMap().Points.SequenceEqual(mapBefore.Points)
        && !engine.ClipGridFollowsAudio(trackId, clipIndex),
        "selecting Grid follows audio remains a UI-only operation before an edit");

    var waveform = (Control)typeof(AudioClipEditorView)
        .GetField("_wave", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .GetValue(editor)!;
    double zoom = 1, scroll = 0;
    Point WavePoint(double sourceFraction)
    {
        double x = (sourceFraction - scroll) * zoom * waveform.Bounds.Width;
        return waveform.TranslatePoint(new Point(x, 55), window)!.Value;
    }
    Point hoverLocal = new(0.5 * waveform.Bounds.Width, 55);
    window.MouseMove(waveform.TranslatePoint(hoverLocal, window)!.Value);
    Pump(30);
    long hoverTick = (long)waveform.GetType()
        .GetField("_gridHoverTick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .GetValue(waveform)!;
    long hitTick = (long)waveform.GetType()
        .GetMethod("HitGridLine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .Invoke(waveform, new object[] { hoverLocal.X })!;
    bool handleHit = (bool)waveform.GetType()
        .GetMethod("OverGridWarpHandle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .Invoke(waveform, new object[] { hoverLocal })!;
    var handleCenterMethod = waveform.GetType()
        .GetMethod("GridWarpHoverHandleCenter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    Point? handleCenter = handleCenterMethod?.Invoke(waveform, new object[] { hitTick, hoverLocal.X }) is Point center
        ? center : null;
    Require(hoverTick == hitTick && hitTick == 960 && handleHit && handleCenter is Point centerPoint
        && Math.Abs(centerPoint.X - hoverLocal.X) < 1,
        "hovering a beat line reveals its draggable handle at the grid hit location");
    void DragGridLine(double fromSource, double toSource, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Point start = WavePoint(fromSource), end = WavePoint(toSource);
        window.MouseDown(start, MouseButton.Left, modifiers);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton | modifiers);
        window.MouseUp(end, MouseButton.Left, modifiers);
        Pump(60);
    }
    DragGridLine(0.5, 0.75);
    var firstWarpMap = engine.GetTempoMap();
    Require(engine.ClipGridFollowsAudio(trackId, clipIndex)
        && Math.Abs(engine.TempoAtBeat(0.5) - 60 / targetTransient.Seconds) < 1e-6
        && firstWarpMap.Anchors.Any(a => a.Tick == 960 && a.SourceFrame == targetTransient.SamplePosition),
        "Grid Warp dragging snaps a beat line to a detected attack and commits its tempo anchor");
    Require(engine.ReadSample(clipBefore.SampleId).SequenceEqual(sourceBefore),
        "Grid Warp gesture leaves the source sample untouched");

    DragGridLine(0.75, 0.79);
    var movedWarpMap = engine.GetTempoMap();
    Require(Math.Abs(engine.TempoAtBeat(0.5) - 60 / 0.79) < 1e-8
        && movedWarpMap.Anchors.Any(a => a.Tick == 960 && Math.Abs(a.Seconds - 0.79) < 1e-8),
        "dragging a committed anchor updates its source-time alignment");
    var beforeInvalidDrag = engine.GetTempoMap();
    DragGridLine(0.79, 0.999);
    Require(engine.GetTempoMap().Points.SequenceEqual(beforeInvalidDrag.Points)
        && engine.GetTempoMap().Anchors.SequenceEqual(beforeInvalidDrag.Anchors),
        "an invalid tempo interval is rejected without a partial commit");

    Point anchorPoint = WavePoint(0.79);
    window.MouseDown(anchorPoint, MouseButton.Left, RawInputModifiers.Alt);
    window.MouseUp(anchorPoint, MouseButton.Left, RawInputModifiers.Alt);
    Pump(60);
    Require(engine.GetTempoMap().Anchors.All(a => a.Tick != 960)
        && Math.Abs(engine.TempoAtBeat(1) - 120) < 1e-8,
        "Alt-click removes a committed Grid Warp anchor and restores the interval");

    var zoomField = waveform.GetType().GetField("_zoom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    var scrollField = waveform.GetType().GetField("_scrollFrac", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    zoom = 2; scroll = 0.1;
    zoomField.SetValue(waveform, zoom);
    scrollField.SetValue(waveform, scroll);
    waveform.InvalidateVisual();
    Pump(40);
    double beatOneSource = 0.5;
    Point linePoint = WavePoint(beatOneSource);
    window.MouseDown(linePoint, MouseButton.Left);
    window.MouseUp(linePoint, MouseButton.Left);
    Pump(30);
    window.MouseDown(linePoint, MouseButton.Left);
    window.MouseUp(linePoint, MouseButton.Left);
    Pump(80);
    Require(engine.GetTempoMap().Anchors.Any(a => a.Tick == 960),
        "double-clicking a beat line creates an anchor at the zoomed and scrolled location");

    var middleTransient = transientSnapshot.Transients.OrderBy(t => Math.Abs(t.Seconds - 0.5)).First();
    double middleFraction = (double)middleTransient.SamplePosition / 44100;
    double nearTransientFraction = middleFraction + 0.004;
    var beforePreview = engine.GetTempoMap();
    Point previewStart = WavePoint(middleFraction), previewEnd = WavePoint(middleFraction + 0.03);
    window.MouseDown(previewStart, MouseButton.Left);
    window.MouseMove(previewEnd, RawInputModifiers.LeftMouseButton | RawInputModifiers.Shift);
    Pump(40);
    var previewDragField = waveform.GetType().GetField("_gridDrag",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    var previewValidField = waveform.GetType().GetField("_gridPreviewValid",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    Require(previewDragField.GetValue(waveform) is not null && Equals(previewValidField.GetValue(waveform), true)
        && engine.GetTempoMap().Points.SequenceEqual(beforePreview.Points)
        && engine.GetTempoMap().Anchors.SequenceEqual(beforePreview.Anchors),
        "Grid Warp previews the target and BPM locally without rebuilding the tempo map");
    window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
    Pump(30);
    window.MouseUp(previewEnd, MouseButton.Left, RawInputModifiers.Shift);
    Pump(30);
    Require(previewDragField.GetValue(waveform) is null
        && engine.GetTempoMap().Points.SequenceEqual(beforePreview.Points)
        && engine.GetTempoMap().Anchors.SequenceEqual(beforePreview.Anchors),
        "Escape cancels an uncommitted Grid Warp preview");

    DragGridLine(middleFraction, nearTransientFraction);
    Require(engine.GetTempoMap().Anchors.Any(a => a.Tick == 960 && a.SourceFrame == middleTransient.SamplePosition),
        "Grid Warp snapping lands on the nearby transient under zoom and scroll");
    DragGridLine(middleFraction, nearTransientFraction, RawInputModifiers.Shift);
    Require(engine.GetTempoMap().Anchors.Any(a => a.Tick == 960 && Math.Abs(a.SourceFrame / 44100 - nearTransientFraction) < 1e-8),
        "Shift-drag bypasses transient snapping under zoom and scroll");

    double EditorValue(string field) => (double)typeof(AudioClipEditorView)
        .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .GetValue(editor)!;
    Grid SliderRow(string label) => window.GetVisualDescendants().OfType<Grid>()
        .Single(g => g.Children.OfType<TextBlock>().Any(t => t.Text == label));
    Control SliderTrack(Grid row) => row.Children.OfType<Control>()
        .Single(c => c.GetType().Name == "SliderTrack");
    var thresholdRow = SliderRow("Detection threshold");
    var spacingRow = SliderRow("Minimum spacing");
    Control thresholdSlider = SliderTrack(thresholdRow);
    Control spacingSlider = SliderTrack(spacingRow);
    Require(EditorValue("_thresholdPercent") == 50 && EditorValue("_minimumSpacingMs") == 30
        && thresholdRow.Children.OfType<TextBlock>().Last().Text == "50\u2009%"
        && spacingRow.Children.OfType<TextBlock>().Last().Text == "30\u2009ms",
        "transient sliders show the 50% and 30 ms defaults");
    void DragSlider(Control slider, double widthFraction)
    {
        Point start = slider.TranslatePoint(new Point(slider.Bounds.Width / 2, slider.Bounds.Height / 2), window)!.Value;
        Point end = new(start.X + slider.Bounds.Width * widthFraction, start.Y);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left);
        Pump(20);
    }
    DragSlider(thresholdSlider, -1);
    Require(EditorValue("_thresholdPercent") == 0, "detection threshold slider reaches 0%");
    DragSlider(thresholdSlider, 1);
    Require(EditorValue("_thresholdPercent") == 100, "detection threshold slider reaches 100%");
    DragSlider(thresholdSlider, -1);
    DragSlider(spacingSlider, -1);
    Require(EditorValue("_minimumSpacingMs") == 5, "minimum spacing slider reaches 5 ms");
    DragSlider(spacingSlider, 1);
    Require(EditorValue("_minimumSpacingMs") == 250
        && spacingRow.Children.OfType<TextBlock>().Last().Text == "250\u2009ms",
        "minimum spacing slider reaches 250 ms and displays its value");
    var anchorsBeforeAnalysis = engine.GetTempoMap().Anchors;
    Pump(600);
    var latestSettings = new TransientAnalysisSettings(1, 0.250);
    const System.Reflection.BindingFlags InstanceMembers =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    var workspace = typeof(AudioClipEditorView).GetProperty("Transients",
        InstanceMembers)!.GetValue(editor)!;
    var latestSnapshot = workspace.GetType().GetMethod("Snapshot", InstanceMembers)!.Invoke(workspace, [clipBefore.SampleId]);
    var actualSettings = (TransientAnalysisSettings?)latestSnapshot?.GetType()
        .GetProperty("Settings", InstanceMembers)!.GetValue(latestSnapshot);
    Require(latestSnapshot is not null && actualSettings == latestSettings
        && engine.GetTempoMap().Anchors.SequenceEqual(anchorsBeforeAnalysis),
        "debounced slider changes re-analyse with only the latest settings and preserve committed anchors");

    window.Close();
    string pulseFile = Path.Combine(scratch, "original-pulse.wav");
    WriteTransientWav(pulseFile, 44100, 9, Enumerable.Range(0, 17).Select(i => 0.5 + i * 0.5).ToArray());
    var appSettings = (ISettingsService)App.Services!.GetService(typeof(ISettingsService))!;
    appSettings.Current.CheckForUpdates = false;
    appSettings.Current.SamplesFolder = Path.Combine(scratch, "samples");
    appSettings.Current.ProjectsFolder = Path.Combine(scratch, "projects");
    var mainVm = (MainWindowViewModel)App.Services.GetService(typeof(MainWindowViewModel))!;
    var projectEngine = mainVm.Engine;
    projectEngine.SetBpm(155); mainVm.Transport.RestoreBpm(155);
    int pulseTrack = projectEngine.AddAudioTrack(), pulseClip = projectEngine.AddAudioClip(pulseTrack, pulseFile, 4);
    projectEngine.TryGetAudioClipInfo(pulseTrack, pulseClip, out var pulseInfo);
    projectEngine.TransientAnalysis.RememberOriginalBpm(pulseInfo.SampleId, 120);
    projectEngine.AutoWarpClipAtBpm(pulseTrack, pulseClip, 120);
    var appWindow = new MainWindow { DataContext = mainVm, Width = 1500, Height = 1000 };
    appWindow.Show(); Pump(150);
    var arrangement = appWindow.FindControl<ArrangementView>("Timeline")!;
    arrangement.Refresh(); arrangement.RevealTrack(pulseTrack); Pump(50);
    var lanes = (Control)typeof(ArrangementView).GetField("_lanes", InstanceMembers)!.GetValue(arrangement)!;
    var tempoLane = (Control)typeof(ArrangementView).GetField("_tempoLane", InstanceMembers)!.GetValue(arrangement)!;
    var rows = ((System.Collections.IEnumerable)typeof(ArrangementView).GetField("_tracks", InstanceMembers)!.GetValue(arrangement)!).Cast<object>().ToArray();
    int pulseRow = Array.FindIndex(rows, row => (int)row.GetType().GetField("Id")!.GetValue(row)! == pulseTrack);
    double rowTop = (double)typeof(ArrangementView).GetMethod("RowTop", InstanceMembers)!.Invoke(arrangement, [pulseRow])!;
    Point grab = lanes.TranslatePoint(new Point((4.5 - arrangement.ScrollBeats) * arrangement.PixelsPerBeat, rowTop + 32), appWindow)!.Value;
    Point tempoTarget = tempoLane.TranslatePoint(new Point(220, 15), appWindow)!.Value;
    var beforeDropMap = projectEngine.GetTempoMap(); projectEngine.ClearUndoHistory();
    appWindow.MouseDown(grab, MouseButton.Left); appWindow.MouseMove(grab + new Vector(7, 0));
    appWindow.MouseMove(tempoTarget); appWindow.MouseUp(tempoTarget, MouseButton.Left); Pump(50);
    var extractionEditor = (AudioClipEditorView?)typeof(MainWindow).GetField("_audioEditor", InstanceMembers)!.GetValue(appWindow);
    Require(extractionEditor is not null, "actual captured clip drop onto Tempo row opens the existing editor");
    var previewField = typeof(AudioClipEditorView).GetField("_beatPreview", InstanceMembers)!;
    var taskWatch = System.Diagnostics.Stopwatch.StartNew();
    while (previewField.GetValue(extractionEditor) is null && taskWatch.ElapsedMilliseconds < 15000) Pump(20);
    var proposals = (TempoAnchor[]?)previewField.GetValue(extractionEditor);
    Require(proposals is { Length: >= 15 }, "drop analyses real original audio into beat proposals");
    projectEngine.TryGetAudioClipInfo(pulseTrack, pulseClip, out var afterDropInfo);
    Require(afterDropInfo.StartBeat == 4 && projectEngine.GetTempoMap().Points.SequenceEqual(beforeDropMap.Points)
        && !projectEngine.CanUndo, "Tempo row drop and proposal preview do not move the source or edit the map");
    Button EditorButton(string label) => appWindow.GetVisualDescendants().OfType<Button>()
        .Single(b => AutomationProperties.GetName(b) == label);
    void InvokeButton(string label) => EditorButton(label).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
    var originalMarkers = ClipWarpMarkers.Read(projectEngine, pulseTrack, pulseClip);
    var extractionWave = (Control)typeof(AudioClipEditorView).GetField("_wave", InstanceMembers)!.GetValue(extractionEditor)!;
    var editMethod = extractionWave.GetType().GetMethod("EditSelectedGridAnchor", InstanceMembers)!;
    var selectMethod = extractionWave.GetType().GetMethod("SelectGridAnchor", InstanceMembers)!;
    var chosen = proposals![3];
    Pump(30);
    double proposalX = (double)extractionWave.GetType().GetMethod("SrcToX", InstanceMembers)!.Invoke(extractionWave, [chosen.SourceFrame, extractionWave.Bounds.Width])!;
    const BindingFlags AllFields = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    double rulerHeight = Convert.ToDouble(extractionWave.GetType().GetField("RulerH", AllFields)!.GetRawConstantValue());
    double stripHeight = Convert.ToDouble(extractionWave.GetType().GetField("StripH", AllFields)!.GetRawConstantValue());
    var proposalAt = extractionWave.TranslatePoint(new Point(proposalX, rulerHeight + stripHeight / 2), appWindow)!.Value;
    appWindow.MouseDown(proposalAt, MouseButton.Left);
    appWindow.MouseMove(proposalAt + new Vector(6, 0), RawInputModifiers.Shift);
    appWindow.MouseUp(proposalAt + new Vector(6, 0), MouseButton.Left, RawInputModifiers.Shift); Pump(30);
    Require(((TempoAnchor[])previewField.GetValue(extractionEditor)!).Single(a => a.Tick == chosen.Tick).SourceFrame != chosen.SourceFrame
        && projectEngine.GetTempoMap().Points.SequenceEqual(beforeDropMap.Points) && !projectEngine.CanUndo,
        "actual proposal pointer drag updates only the preview, with Shift snap bypass");
    selectMethod.Invoke(extractionWave, [chosen]);
    bool corrected = (bool)editMethod.Invoke(extractionWave, [(double)chosen.Tick / 960, chosen.Seconds + 0.01])!;
    Require(corrected && !projectEngine.CanUndo, "proposal numeric correction remains local before Apply");
    string shots = Path.Combine("specs", "079-grid-warp", "screenshots"); Directory.CreateDirectory(shots);
    appWindow.MouseMove(new Point(1400, 20)); ToolTip.SetIsOpen(tempoLane, false);
    Pump(80); using (var picture = appWindow.CaptureRenderedFrame()) picture!.Save(Path.Combine(shots, "proposal-dark.png"));
    InvokeButton("Cancel"); Pump(20);
    Require(previewField.GetValue(extractionEditor) is null && !projectEngine.CanUndo
        && projectEngine.GetTempoMap().Points.SequenceEqual(beforeDropMap.Points), "Cancel discards proposals without an undo entry");
    var analysis = extractionEditor!.ExtractOriginalTempoAsync();
    while (!analysis.IsCompleted) Pump(10); analysis.GetAwaiter().GetResult();
    Require(previewField.GetValue(extractionEditor) is TempoAnchor[], "Snap All Beats can regenerate the original-source preview");
    InvokeButton("Apply"); Pump(60);
    var applied = projectEngine.GetTempoMap();
    var markersAfterApply = ClipWarpMarkers.Read(projectEngine, pulseTrack, pulseClip);
    Require(projectEngine.ClipGridFollowsAudio(pulseTrack, pulseClip)
        && applied.Points.Length >= 15 && applied.Points.All(p => p.Curve == TempoCurve.Step)
        && originalMarkers.Source.SequenceEqual(markersAfterApply.Source) && originalMarkers.Beats.SequenceEqual(markersAfterApply.Beats),
        "Apply writes real Step tempo points and preserves original audio warp markers");
    Require(projectEngine.Undo() && !projectEngine.CanUndo && projectEngine.GetTempoMap().Points.SequenceEqual(beforeDropMap.Points)
        && projectEngine.Redo(), "automatic Apply is one complete tempo/reference undo/redo action");
    extractionEditor.Reload();
    var numericAnchor = projectEngine.GetTempoMap().Anchors.First(a => a.Tick == chosen.Tick);
    selectMethod.Invoke(extractionWave, [numericAnchor]); projectEngine.ClearUndoHistory();
    long numericRevision = projectEngine.TempoRevision;
    extractionWave.GetType().GetMethod("BeginGridNumericPreview", InstanceMembers)!.Invoke(extractionWave, null);
    Require((bool)editMethod.Invoke(extractionWave, [(double)numericAnchor.Tick / 960, numericAnchor.Seconds + 0.01])!
        && projectEngine.TempoRevision == numericRevision && !projectEngine.CanUndo,
        "numeric anchor drag previews do not rebuild or publish tempo maps");
    extractionWave.GetType().GetMethod("EndGridNumericPreview", InstanceMembers)!.Invoke(extractionWave, null);
    Require(projectEngine.TempoRevision > numericRevision && projectEngine.CanUndo, "numeric anchor gesture commits only on release");
    int beforeAdd = projectEngine.GetTempoMap().Anchors.Length;
    InvokeButton("Add anchor"); Pump(30);
    Require(projectEngine.GetTempoMap().Anchors.Length == beforeAdd + 1, "discoverable Add anchor creates a new fixed anchor");
    InvokeButton("Reset grid"); Pump(40);
    Require(!projectEngine.ClipGridFollowsAudio(pulseTrack, pulseClip) && projectEngine.Undo(), "actual Reset control restores the baseline and is undoable");
    extractionEditor.Reload();
    var gridSection = (Control)typeof(AudioClipEditorView).GetField("_gridExtractionControls", InstanceMembers)!.GetValue(extractionEditor)!;
    gridSection.BringIntoView(); Pump(80);
    using (var picture = appWindow.CaptureRenderedFrame()) picture!.Save(Path.Combine(shots, "fixed-dark.png"));
    var theme = typeof(App).Assembly.GetType("Nota.App.NotaThemeService")!.GetMethod("Set")!;
    theme.Invoke(null, new object[] { new AppearanceChoice(false, "studio-light", "studio-dark", "studio-light") }); Pump(80);
    using (var picture = appWindow.CaptureRenderedFrame()) picture!.Save(Path.Combine(shots, "fixed-light.png"));
    typeof(MainWindow).GetField("_closeConfirmed", InstanceMembers)!.SetValue(appWindow, true); appWindow.Close(); Pump(100);
    Console.WriteLine("PASS actual captured clip-to-Tempo drop, original-source preview, correction, Cancel and Apply");
    Console.WriteLine("PASS actual Step tempo/reference undo, Reset and dark/light editor screenshots");
    Console.WriteLine("PASS Grid Warp headless UI harness setup");
    Console.WriteLine("PASS Grid Warp mode exclusivity and side-effect-free selection");
    Console.WriteLine("PASS Grid Warp grid gestures, transient snapping, anchors and audio preservation");
    Console.WriteLine("PASS transient slider bounds, defaults, debounce and anchor preservation");
}
finally
{
    Directory.Delete(scratch, recursive: true);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static void WriteTransientWav(string path, int sampleRate, double seconds = 1, double[]? times = null)
{
    const short channels = 1, bitsPerSample = 16;
    int frames = (int)(sampleRate * seconds);
    int dataBytes = frames * channels * bitsPerSample / 8;
    var audio = new float[frames];
    foreach (double time in times ?? new[] { 0.25, 0.5, 0.75 })
    {
        int start = (int)Math.Round(time * sampleRate);
        for (int j = 0; j < sampleRate * 0.12 && start + j < audio.Length; j++)
            audio[start + j] += (float)(0.35 * Math.Exp(-(double)j / sampleRate * 160) * (j % 2 == 0 ? 1 : -0.7));
    }
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write("RIFF"u8);
    writer.Write(36 + dataBytes);
    writer.Write("WAVEfmt "u8);
    writer.Write(16);
    writer.Write((short)1);
    writer.Write(channels);
    writer.Write(sampleRate);
    writer.Write(sampleRate * channels * bitsPerSample / 8);
    writer.Write((short)(channels * bitsPerSample / 8));
    writer.Write(bitsPerSample);
    writer.Write("data"u8);
    writer.Write(dataBytes);
    foreach (float sample in audio)
        writer.Write((short)Math.Round(Math.Clamp(sample, -1, 1) * short.MaxValue));
}

static void Pump(int milliseconds)
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    do { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } while (watch.ElapsedMilliseconds < milliseconds);
}

static void RunAnchorInteractionRegression(string scratch, string test)
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    string path = Path.Combine(scratch, $"anchor-{test}.wav"); WriteTransientWav(path, 44100);
    using var engine = new NotaEngine(); engine.RenderOffline(new float[128], 64, 44100);
    int track = engine.AddAudioTrack(), clip = engine.AddAudioClip(track, path, 4);
    engine.TryGetAudioClipInfo(track, clip, out var original);
    engine.TransientAnalysis.AnalyzeAudioAsync(original.SampleId).GetAwaiter().GetResult();
    if (test == "source")
    {
        engine.AutoWarpClipAtBpm(track, clip, 120);
        engine.SetClipWarpMarkers(track, clip, [0, 22050, 44100], [0, 0.7, 2]);
    }
    var source = engine.ReadSample(original.SampleId);
    var markers = ClipWarpMarkers.Read(engine, track, clip);
    engine.ClearUndoHistory();
    int seeks = 0;
    var editor = new AudioClipEditorView(engine, track, clip, Brushes.Orange, () => { }) { Seek = _ => seeks++ };
    var window = new Window { Content = editor, Width = 1200, Height = 750 }; window.Show(); Pump(70);
    try
    {
        var mode = window.GetVisualDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == "Grid follows audio");
        Point modeAt = mode.TranslatePoint(new Point(mode.Bounds.Width / 2, mode.Bounds.Height / 2), window)!.Value;
        window.MouseDown(modeAt, MouseButton.Left); window.MouseUp(modeAt, MouseButton.Left); Pump(50);
        var wave = (Control)typeof(AudioClipEditorView).GetField("_wave", Members)!.GetValue(editor)!;
        Point At(double fraction, double y) => wave.TranslatePoint(new Point(fraction * wave.Bounds.Width, y), window)!.Value;
        Button Button(string name) => window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name || b.Content as string == name);
        void Click(Button button)
        {
            button.BringIntoView(); Pump(40);
            var at = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, MouseButton.Left); window.MouseUp(at, MouseButton.Left); Pump(60);
        }
        void Fields(double beat, double time)
        {
            foreach (var value in new[] { ("_gridAnchorBeat", beat), ("_gridAnchorTime", time) })
            {
                var field = typeof(AudioClipEditorView).GetField(value.Item1, Members)!.GetValue(editor)!;
                field.GetType().GetProperty("Value")!.SetValue(field, value.Item2);
            }
        }
        if (test == "add")
        {
            Click(Button("Add anchor"));
            Require(engine.GetTempoMap().Anchors.Any(a => a.Tick > 4 * 960 && a.Tick < 6 * 960),
                "Add anchor must work immediately for a clip at beat 4 without prior selection");
            Require(engine.Undo() && !engine.CanUndo, "Add is one complete undo action");
        }
        else if (test == "ruler")
        {
            Point start = At(0.5, 12), target = At(0.75, 12);
            window.MouseDown(start, MouseButton.Left); window.MouseUp(start, MouseButton.Left); Pump(30);
            window.MouseDown(start, MouseButton.Left); window.MouseUp(start, MouseButton.Left); Pump(60);
            Require(engine.GetTempoMap().Anchors.Any(a => a.Tick == 5 * 960), "double-click beat ruler must add a musical anchor");
            Require(seeks == 0, "musical ruler handle must take priority over transport seeking");
            window.MouseDown(start, MouseButton.Left); window.MouseMove(target, RawInputModifiers.LeftMouseButton);
            window.MouseUp(target, MouseButton.Left); Pump(60);
            Require(engine.GetTempoMap().Anchors.Any(a => a.Tick == 5 * 960 && Math.Abs(a.SourceFrame / 44100 - 0.75) < 0.02),
                "dragging an anchor from the ruler snaps onto the detected source attack");
            Require(seeks == 0, "ruler anchor drag must not seek");
            Point between = At(0.35, 12);
            window.MouseDown(between, MouseButton.Left); window.MouseUp(between, MouseButton.Left); Pump(20);
            Require(seeks > 0, "ruler between musical handles still seeks");
        }
        else if (test == "snap")
        {
            Fields(5, 2.70); Click(Button("Add anchor"));
            Require(engine.GetTempoMap().Anchors.Any(a => a.Tick == 5 * 960), "snap fixture created an interior anchor");
            var snap = Button("Snap selected anchor");
            Require(snap.IsEnabled, "Snap selected anchor must enable for selected Grid anchor");
            engine.ClearUndoHistory(); Click(snap);
            Require(engine.GetTempoMap().Anchors.Any(a => a.Tick == 5 * 960 && Math.Abs(a.SourceFrame / 44100 - 0.75) < 0.02),
                "Snap selected anchor must move the musical anchor to the nearest valid transient");
            Require(engine.Undo() && !engine.CanUndo, "snap is one undo action");
        }
        else if (test == "source")
        {
            var drawn = (System.Collections.IList)wave.GetType().GetField("_mk", Members)!.GetValue(wave)!;
            Require(drawn.Count == 0, "Grid mode must draw original source, not the auto-warped overview");
            double content = (double)wave.GetType().GetMethod("SourceToContent", Members)!.Invoke(wave, [22050.0])!;
            Require(Math.Abs(content - 0.5) < 1e-9, "transient guides use original source coordinates before first Grid edit");
            Require(!engine.CanUndo, "selecting Grid tool does not change authoring state");
            Point from = At(0.5, 55), to = At(0.75, 55);
            window.MouseDown(from, MouseButton.Left); window.MouseMove(to, RawInputModifiers.LeftMouseButton);
            window.MouseUp(to, MouseButton.Left); Pump(50);
            Require(engine.GetTempoMap().Anchors.Any(a => a.Tick == 5 * 960 && Math.Abs(a.SourceFrame / 44100 - 0.75) < 0.02),
                "first Grid edit on an auto-warped clip aligns the natural source attack");
        }
        Require(engine.ReadSample(original.SampleId).SequenceEqual(source), "Grid interactions preserve original PCM");
        var afterMarkers = ClipWarpMarkers.Read(engine, track, clip);
        Require(markers.Source.SequenceEqual(afterMarkers.Source) && markers.Beats.SequenceEqual(afterMarkers.Beats), "Grid interactions preserve audio warp data");
    }
    finally { window.Close(); Pump(25); }
}
