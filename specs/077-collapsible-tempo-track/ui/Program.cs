// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nota.App;
using Nota.Application;
using Nota.Presentation;

string scratch = Path.Combine(Path.GetTempPath(), $"nota-tempo-track-{Guid.NewGuid():N}");
Directory.CreateDirectory(scratch);
Environment.SetEnvironmentVariable("NOTA_DATA_DIR", scratch);
AppBuilder.Configure<App>().UseSkia().UseHeadless(new() { UseHeadlessDrawing = false })
    .WithNotaFonts().SetupWithoutStarting();
var settings = (ISettingsService)App.Services!.GetService(typeof(ISettingsService))!;
settings.Current.CheckForUpdates = false;
settings.Current.SamplesFolder = Path.Combine(scratch, "samples");
settings.Current.ProjectsFolder = Path.Combine(scratch, "projects");
var vm = (MainWindowViewModel)App.Services.GetService(typeof(MainWindowViewModel))!;
var window = new MainWindow { DataContext = vm, Width = 1500, Height = 900 };
window.Show(); Pump(150);
var timeline = window.FindControl<ArrangementView>("Timeline")!;
var lane = (TempoTrackControl)typeof(ArrangementView).GetField("_tempoLane", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(timeline)!;
if (!timeline.ShowTempo || timeline.TempoExpanded || lane.Bounds.Height != 30) throw new Exception("Default compact lane missing");
Console.WriteLine("PASS visible compact Tempo Track in actual MainWindow");
timeline.TempoExpanded = true; timeline.TempoLaneHeight = 180; Pump(100);
if (lane.Bounds.Height != 180) throw new Exception("Expanded lane height did not apply");
Point Root(double beat, double bpm) => lane.TranslatePoint(new Point((beat - timeline.ScrollBeats) * timeline.PixelsPerBeat,
    9 + (lane.Bounds.Height - 18) * (1 - (bpm - 20) / 280)), window)!.Value;
var mouse = new Avalonia.Input.Pointer(1, PointerType.Mouse, true);
void Add(double beat, double bpm)
{
    lane.RaiseEvent(new PointerPressedEventArgs(lane, mouse, window, Root(beat, bpm), 0,
        new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 2));
    Pump(30);
}
Add(4, 140);
if (vm.Engine.GetTempoMap().Points is not [_, { Tick: 3840, Bpm: 140 }]) throw new Exception("Double-click point insertion failed");
Console.WriteLine("PASS actual double-click inserts snapped point");
vm.Engine.ClearUndoHistory();
Point start = Root(4, 140), end = Root(6, 160);
window.MouseDown(start, MouseButton.Left); window.MouseMove(end); window.MouseUp(end, MouseButton.Left); Pump(30);
var point = vm.Engine.GetTempoMap().Points[1];
if (point.Tick != 5760 || Math.Abs(point.Bpm - 160) > 0.01) throw new Exception($"Drag failed: {point}");
if (!vm.Engine.Undo() || vm.Engine.GetTempoMap().Points[1].Tick != 3840 || vm.Engine.CanUndo) throw new Exception("Point drag was not one undo");
vm.Engine.Redo(); timeline.ReloadTempo();
Console.WriteLine("PASS real point drag updates BPM/beat in one undo transaction");
void PressKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
{
    lane.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers }); Pump(20);
}
RawInputModifiers Raw(KeyModifiers modifiers) => modifiers == KeyModifiers.Shift ? RawInputModifiers.Shift : modifiers == KeyModifiers.Alt ? RawInputModifiers.Alt : RawInputModifiers.None;
void Click(double beat, double bpm, KeyModifiers modifiers = KeyModifiers.None)
{
    Point at = Root(beat, bpm); window.MouseDown(at, MouseButton.Left, Raw(modifiers)); window.MouseUp(at, MouseButton.Left, Raw(modifiers)); Pump(20);
}
start = Root(6, 160); end = Root(8, 180);
window.MouseDown(start, MouseButton.Left, RawInputModifiers.Shift);
window.MouseMove(end, RawInputModifiers.Shift); window.MouseUp(end, MouseButton.Left, RawInputModifiers.Shift); Pump(20);
point = vm.Engine.GetTempoMap().Points[1];
if (point.Tick != 5952 || Math.Abs(point.Bpm - 162) > 0.01) throw new Exception($"Fine Shift-drag failed: {point}");
Console.WriteLine("PASS Shift-drag retains selected point and refines position/BPM");
lane.SetSelected(beat: 6, bpm: 160);
PressKey(Key.Up, KeyModifiers.Shift);
if (vm.Engine.GetTempoMap().Points[1].Bpm != 160.01) throw new Exception("Fine keyboard nudge failed");
lane.SetSelected(bpm: 160);
start = Root(6, 160); end = Root(8, 180);
window.MouseDown(start, MouseButton.Left); window.MouseMove(end); PressKey(Key.Escape); window.MouseUp(end, MouseButton.Left); Pump(20);
if (vm.Engine.GetTempoMap().Points[1].Tick != 5760) throw new Exception("Escape did not cancel drag");
Click(0, 120, KeyModifiers.Alt);
if (vm.Engine.GetTempoMap().Points[0].Tick != 0 || vm.Engine.GetTempoMap().Points.Length != 2) throw new Exception("Initial point deletion was not protected");
Add(10, 180); Click(10, 180, KeyModifiers.Alt);
if (vm.Engine.GetTempoMap().Points.Length != 2) throw new Exception("Alt-click deletion failed");
Click(6, 160); lane.SetSelected(beat: 7, bpm: 155.25);
if (vm.Engine.GetTempoMap().Points[1] is not { Tick: 6720, Bpm: 155.25 }) throw new Exception("Numeric inspector edit failed");
Console.WriteLine("PASS fine keys, Escape cancellation, numeric edits, Alt deletion and initial protection");
Add(10, 180);
Point boxStart = Root(6, 210), boxEnd = Root(11, 130);
window.MouseDown(boxStart, MouseButton.Left); window.MouseMove(boxEnd); window.MouseUp(boxEnd, MouseButton.Left); Pump(20);
PressKey(Key.Up);
if (vm.Engine.GetTempoMap().Points[1].Bpm != 156.25 || vm.Engine.GetTempoMap().Points[2].Bpm != 181) throw new Exception("Box/group selection failed");
void Await(Task task) { while (!task.IsCompleted) Pump(5); task.GetAwaiter().GetResult(); }
Await(lane.CopyAsync());
vm.Engine.Seek(16); vm.Engine.RenderOffline(new float[128], 64, 44100);
Await(lane.PasteAsync());
if (vm.Engine.GetTempoMap().Points.Length != 5 || vm.Engine.GetTempoMap().Points[3].Tick != 15360) throw new Exception("Tempo clipboard paste failed");
Console.WriteLine("PASS real box selection, group editing and system clipboard copy/paste at playhead");
vm.Engine.Seek(0); vm.Engine.RenderOffline(new float[128], 64, 44100);
Click(7, 156.25);
lane.SetSelected(curve: TempoCurve.Linear); Add(12, 100);
lane.SetSelected(curve: TempoCurve.Smooth); Pump(50);
Point contextAt = Root(12, 100);
window.MouseDown(contextAt, MouseButton.Right); window.MouseUp(contextAt, MouseButton.Right); Pump(20);
var smoothItem = lane.ContextMenu!.Items.OfType<MenuItem>().First(item => item.Header?.ToString() == "Smooth");
smoothItem.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); lane.ContextMenu.Close(); Pump(20);
if (vm.Engine.GetTempoMap().Points.Single(p => p.Tick == 11520).Curve != TempoCurve.Smooth) throw new Exception("Context interpolation selection failed");
Console.WriteLine("PASS actual point context menu chooses outgoing interpolation");
timeline.SaveTrackView(scratch);
timeline.ShowTempo = false; timeline.TempoExpanded = false; timeline.TempoLaneHeight = 100;
timeline.LoadTrackView(scratch); Pump(30);
if (!timeline.ShowTempo || !timeline.TempoExpanded || timeline.TempoLaneHeight != 180) throw new Exception("Tempo lane view state did not reload");
Console.WriteLine("PASS compact/expanded/visibility/height persistence");
string screenshots = Path.Combine("specs", "077-collapsible-tempo-track", "screenshots");
Directory.CreateDirectory(screenshots);
Pump(100); using (var image = window.CaptureRenderedFrame()) image!.Save(Path.Combine(screenshots, "expanded.png"));
timeline.TempoExpanded = false; Pump(50);
using (var image = window.CaptureRenderedFrame()) image!.Save(Path.Combine(screenshots, "compact.png"));
Console.WriteLine("PASS actual Windows headless UI rendered compact and expanded screenshots");
timeline.TempoExpanded = true;
timeline.FollowPlayhead = true;
timeline.SetPlayheadFrame(0, 0, false); Pump(50);
byte[] TempoPixels()
{
    using var frame = window.CaptureRenderedFrame() ?? throw new Exception("Tempo lane frame missing");
    Point origin = lane.TranslatePoint(new Point(), window)!.Value;
    var rect = new PixelRect((int)origin.X, (int)origin.Y, (int)lane.Bounds.Width, (int)lane.Bounds.Height);
    int stride = rect.Width * 4;
    var pixels = new byte[stride * rect.Height];
    IntPtr buffer = Marshal.AllocHGlobal(pixels.Length);
    try { frame.CopyPixels(rect, buffer, pixels.Length, stride); Marshal.Copy(buffer, pixels, 0, pixels.Length); }
    finally { Marshal.FreeHGlobal(buffer); }
    return pixels;
}
byte[] beforeFollowScroll = TempoPixels();
timeline.SetPlayheadFrame(60, 60, true); Pump(80);
if (timeline.ScrollBeats <= 0 || beforeFollowScroll.SequenceEqual(TempoPixels()))
    throw new Exception("Tempo lane did not redraw when Follow scrolled the Arrangement");
timeline.FollowPlayhead = false;
timeline.SetPlayheadFrame(0, 0, false); Pump(40);
Console.WriteLine("PASS tempo lane redraws when Follow scrolls the Arrangement");
typeof(ArrangementView).GetMethod("ZoomAtBeat", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(timeline, new object[] { 10000d, 7d }); Pump(40);
typeof(ArrangementView).GetMethod("ZoomAtBeat", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(timeline, new object[] { 0.000001d, 7d }); Pump(40);
typeof(ArrangementView).GetMethod("ZoomAtBeat", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(timeline, new object[] { 8d, 0d }); Pump(40);
Console.WriteLine("PASS actual lane renders at minimum/maximum zoom without losing map points");
var themeSet = typeof(App).Assembly.GetType("Nota.App.NotaThemeService")!.GetMethod("Set")!;
themeSet.Invoke(null, new object[] { new AppearanceChoice(false, "studio-light", "studio-dark", "studio-light") }); Pump(100);
using (var image = window.CaptureRenderedFrame()) image!.Save(Path.Combine(screenshots, "expanded-light.png"));
var prefs = new PreferencesWindow(new SettingsViewModel(settings), vm);
prefs.Show(window); prefs.ShowPage("Shortcuts"); Pump(100);
var content = prefs.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "Tempo track");
content?.BringIntoView(); Pump(60);
if (content?.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() is { } scroll)
{
    double y = content.TranslatePoint(new Point(), scroll)!.Value.Y;
    scroll.Offset = new Vector(scroll.Offset.X, scroll.Offset.Y + y - 12); Pump(60);
}
using (var image = prefs.CaptureRenderedFrame()) image!.Save(Path.Combine(screenshots, "shortcuts-light.png"));
prefs.Close(); Pump(30);
Console.WriteLine("PASS Studio Light lane and actual Preferences shortcut pane screenshots");
var originalMap = vm.Engine.GetTempoMap();
vm.Engine.TrySetTempoMap(originalMap with { Points = Enumerable.Range(0, 10000).Select(i => new TempoPoint(i * 960L, 100 + i % 80)).ToArray() }, out _);
var denseWatch = Stopwatch.StartNew(); timeline.ReloadTempo(); Pump(30);
using (var frame = window.CaptureRenderedFrame()) { if (frame is null) throw new Exception("Dense map did not render"); }
denseWatch.Stop();
if (vm.Engine.GetTempoMap().Points.Length != 10000) throw new Exception("Dense UI map lost points");
Console.WriteLine($"PASS 10,000-point actual lane reload/render ({denseWatch.Elapsed.TotalMilliseconds:F1} ms including 30 ms dispatcher pump)");
vm.Engine.TrySetTempoMap(originalMap, out _); timeline.ReloadTempo();
typeof(MainWindow).GetField("_closeConfirmed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
window.Close(); Pump(200);
return 0;

static void Pump(int milliseconds)
{
    var watch = Stopwatch.StartNew();
    do { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } while (watch.ElapsedMilliseconds < milliseconds);
}
