// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nota.App;
using Nota.Application;
using Nota.Application.Editing;

string scratch = Path.Combine(Path.GetTempPath(), $"nota-snap-toggle-{Guid.NewGuid():N}");
Directory.CreateDirectory(scratch);
Environment.SetEnvironmentVariable("NOTA_DATA_DIR", scratch);
AppBuilder.Configure<App>().UseSkia().UseHeadless(new() { UseHeadlessDrawing = false })
    .WithNotaFonts().SetupWithoutStarting();

var grids = EditorGrids.Shared;
var original = grids.Get(EditorKind.PianoRoll);
var fixedGrid = original with { Adaptive = false, FixedIndex = 2, Triplets = true, Density = GridDensity.Wide, SnapOn = true };
grids.Set(EditorKind.PianoRoll, fixedGrid);
var roll = new PianoRollView();
roll.SetNotes([new NotaNote(60, 0, 1, .8f)], 16);
var editor = new ClipEditorView(roll, "Snap toggle test", 0);
var window = new Window { Content = editor, Width = 1200, Height = 750 };
window.Show();
Pump(100);

var button = editor.GetVisualDescendants().OfType<ToggleButton>()
    .SingleOrDefault(b => AutomationProperties.GetName(b) == "Snap to grid")
    ?? throw new Exception("Dedicated Piano Roll snap toggle not found");
Require(button.IsChecked == true, "toggle initializes from the enabled Piano Roll grid setting");
Require(AutomationProperties.GetHelpText(button) == "Snap on", "enabled state has an accessible status");

Point Center() => button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
void Click()
{
    Point p = Center();
    window.MouseDown(p, MouseButton.Left);
    window.MouseUp(p, MouseButton.Left);
    Pump(30);
}
double SnapBeat(double beat) => (double)typeof(PianoRollView)
    .GetMethod("SnapBeat", BindingFlags.Instance | BindingFlags.NonPublic)!
    .Invoke(roll, [beat])!;

var snapOff = fixedGrid with { SnapOn = false };
Click();
Require(grids.Get(EditorKind.PianoRoll) == snapOff && button.IsChecked == false,
    "one click disables snap and preserves every other grid setting");
Require(AutomationProperties.GetHelpText(button) == "Snap off — items move freely (⌘4)",
    "disabled state has an accessible status");
Require(Math.Abs(SnapBeat(1.3) - 1.3) < 1e-9, "note editing keeps an off-grid beat while snap is off");

grids.Set(EditorKind.PianoRoll, fixedGrid with { SnapOn = false });
Click();
Require(grids.Get(EditorKind.PianoRoll) == fixedGrid && button.IsChecked == true,
    "one click restores snapping without changing the selected grid");
double snappedBeat = SnapBeat(1.3);
Require(Math.Abs(snappedBeat - 4.0 / 3.0) < 1e-9, $"note editing snaps to the selected triplet step while snap is on (got {snappedBeat})");

grids.Set(EditorKind.PianoRoll, snapOff);
Pump(20);
Require(button.IsChecked == false, "toggle follows snap changes from the grid menu or command");

Console.WriteLine("PASS Piano Roll snap toggle initialization, clicks, note snapping and external synchronization");
window.Close();
grids.Set(EditorKind.PianoRoll, original);

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static void Pump(int milliseconds)
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    do { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } while (watch.ElapsedMilliseconds < milliseconds);
}
