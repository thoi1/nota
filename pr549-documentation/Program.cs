// Real UI capture, following nota-docs/shots/app: headless Skia, layout scaled 2x.
// The base capture uses the current native DLL only for unchanged track metadata calls.
// Notes are populated in the managed piano roll; none cross the changed note C ABI.
using System.Linq.Expressions;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Nota.App;
using Nota.Application;
using Nota.Application.Editing;
#if AFTER
using Nota.Application.Articulations;
#endif

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NOTA_DATA_DIR"))) throw new Exception("Set isolated NOTA_DATA_DIR");
AppBuilder.Configure<App>().UseSkia().UseHeadless(new() { UseHeadlessDrawing = false }).WithNotaFonts().SetupWithoutStarting();
var theme = typeof(App).Assembly.GetType("Nota.App.NotaThemeService")!.GetMethod("Set")!;
theme.Invoke(null, new[] { Activator.CreateInstance(theme.GetParameters()[0].ParameterType, false, "studio-dark", "studio-dark", "studio-light")! });
var engine = (IAudioEngine)App.Services.GetService(typeof(IAudioEngine))!;
engine.Reset();
int track = engine.AddInstrumentTrack();
engine.SetTrackName(track, "Demo strings");
var timeline = new ArrangementView { Engine = engine };
const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
var type = typeof(App).Assembly.GetType("Nota.App.TrackInspectorView")!;
var ctor = type.GetConstructors(flags).Single();
var delegateType = ctor.GetParameters()[2].ParameterType;
var entryType = typeof(App).Assembly.GetType("Nota.App.FreezeEntry")!;
var empty = Expression.Lambda(delegateType, Expression.Convert(Expression.Constant(Array.CreateInstance(entryType, 0)), delegateType.GetMethod("Invoke")!.ReturnType), Expression.Parameter(typeof(int))).Compile();
#if AFTER
var map = new ArticulationMap { Id = "pr549-demo", Name = "Demo strings", InstrumentName = "Illustrative MIDI mapping", DefaultArticulationId = 1,
    Articulations = new() {
        new() { Id=1, Name="Long", ShortName="Lng", SourceColorIndex=0, Actions=new() { new() { Kind=MidiActionKind.Keyswitch, Data1=24, Data2=100 } } },
        new() { Id=2, Name="Legato", ShortName="Leg", SourceColorIndex=2, NoteOffsetMs=-70, TriggerLeadMs=30, Actions=new() { new() { Kind=MidiActionKind.Keyswitch, Data1=25, Data2=100 } } },
        new() { Id=3, Name="Staccato", ShortName="Stac", SourceColorIndex=4, Trigger=TriggerMode.Momentary, Actions=new() { new() { Kind=MidiActionKind.Keyswitch, Data1=26, Data2=100 } } }
    } };
ArticulationMapAssignment.Assign(engine, track, map);
engine.SetTrackMidiOffsetMs(track, -80);
#endif
var inspector = (Control)ctor.Invoke(new object[] { engine, timeline, empty, (Func<bool>)(() => false) });
type.GetMethod("Show")!.Invoke(inspector, new object[] { track });
Shoot(new Window { Width=340, Height=850, Content=inspector }, "inspector");
var roll = new PianoRollView();
var notes = new List<NotaNote>();
for (int i=0; i<8; i++) {
    var note = new NotaNote(60 + new[] {0,4,7,5,9,7,4,0}[i], i, i<4 ? .85 : .35, .75f);
#if AFTER
    note.ArticulationId = i<3 ? 1 : i<5 ? 2 : 3;
#endif
    notes.Add(note);
}
#if AFTER
roll.ArticulationMapOf = _ => map;
PianoRollView.SetNoteColorByArticulation(true);
#endif
roll.SetClips(ClipSet.Single(track, 0, 0, 8), new[] { new ClipNotes(track, 0, notes) }, _ => ArrangementView.TrackBrush(0));
var editor = new ClipEditorView(roll, "Demo phrase", 0, lengthBeats:8);
Shoot(new Window { Width=1440, Height=540, Content=editor }, "piano-roll", () => roll.FitToNotes());
#if AFTER
var managerType = typeof(ArticulationMapManager);
var manager = (Window)managerType.GetConstructors(flags).Single().Invoke(new object[] { engine, (Func<int>)(() => track) });
Shoot(manager, "manager");
var result = new ArticulationImportResult { Map=map };
result.Info("Demonstration map: MIDI keys and offsets are illustrative; use your library's own values.");
Shoot(new ArticulationImportPreview("demo-strings.notamap", result), "import-preview");
#endif
engine.Dispose();
return 0;

void Pump() { for (int i=0; i<15; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(15); } }
void Shoot(Window win, string name, Action? layout = null) {
    if (win.GetType() == typeof(Window) && Avalonia.Application.Current!.TryGetResource("Brush.SurfaceCard", Avalonia.Application.Current.ActualThemeVariant, out var background) && background is IBrush brush) win.Background = brush;
    var content = (Control)win.Content!;
    win.Content = null;
    win.Content = new LayoutTransformControl { LayoutTransform=new ScaleTransform(2,2), Child=content };
    win.Width*=2; win.Height*=2;
    win.Show(); Pump(); layout?.Invoke(); Pump();
    Directory.CreateDirectory(args[0]);
    using var frame=win.CaptureRenderedFrame() ?? throw new Exception("No rendered frame");
    frame.Save(Path.Combine(args[0], name + ".png"));
    Console.WriteLine("Captured " + name); win.Close();
}
