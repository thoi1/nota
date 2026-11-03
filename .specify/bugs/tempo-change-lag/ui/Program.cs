// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Nota.App;
using Nota.Application;
using Nota.Presentation;

string scratch = Path.Combine(Path.GetTempPath(), $"nota-tempo-ui-{Guid.NewGuid():N}");
Directory.CreateDirectory(scratch);
Environment.SetEnvironmentVariable("NOTA_DATA_DIR", scratch);
AppBuilder.Configure<App>().UseSkia().UseHeadless(new() { UseHeadlessDrawing = false })
    .WithNotaFonts().SetupWithoutStarting();
var engine = (IAudioEngine)App.Services!.GetService(typeof(IAudioEngine))!;
var settings = (ISettingsService)App.Services.GetService(typeof(ISettingsService))!;
settings.Current.CheckForUpdates = false;
settings.Current.SamplesFolder = Path.Combine(scratch, "samples");
settings.Current.ProjectsFolder = Path.Combine(scratch, "projects");
var vm = (MainWindowViewModel)App.Services.GetService(typeof(MainWindowViewModel))!;
var window = new MainWindow { DataContext = vm, Width = 1440, Height = 900 };
window.Show();
Pump(100);
engine.Reset();
engine.RenderOffline(new float[128], 64, 44100);
string file = Path.Combine(scratch, "long.wav");
Nota.SmokeTest.WavWriter.WriteSine(file, 20, 440, 44100);
for (int i = 0; i < 2; i++)
{
    int track = engine.AddAudioTrack();
    int clip = engine.AddAudioClip(track, file, 0);
    engine.SetClipWarp(track, clip, true, 3);
}
window.FindControl<ArrangementView>("Timeline")!.Refresh();
Pump(100);
var host = window.FindControl<Panel>("BpmHost")!;
var field = host.Children.OfType<DragNumber>().Single();
Point point = field.TranslatePoint(new Point(field.Bounds.Width / 2, field.Bounds.Height / 2), window)!.Value;
window.MouseDown(point, MouseButton.Left);
double slowest = 0;
for (int i = 1; i <= 25; i++)
{
    var watch = Stopwatch.StartNew();
    window.MouseMove(point - new Vector(0, i * 2));
    watch.Stop();
    slowest = Math.Max(slowest, watch.Elapsed.TotalMilliseconds);
    Dispatcher.UIThread.RunJobs();
}
window.MouseUp(point - new Vector(0, 50), MouseButton.Left);
if (vm.Transport.Bpm != 145) throw new Exception($"Gesture did not reach 145 BPM: {vm.Transport.Bpm}");
if (engine.WarpBuildRemaining <= 0) throw new Exception("Transport gesture synchronously rebuilt caches");
Console.WriteLine($"PASS actual transport drag: 25 BPM changes; slowest input {slowest:F3} ms");
if (slowest > 100) throw new Exception("Transport gesture blocked on warp DSP");

// No device in this harness: drain clock commands without letting offline export
// preparation stand in for the actual MainWindow background polling loop.
engine.SetDeferWarpBuild(true);
engine.RenderOffline(new float[128], 64);
engine.SetDeferWarpBuild(false);
var deadline = Stopwatch.StartNew();
while (engine.WarpBuildRemaining > 0 && deadline.ElapsedMilliseconds < 10000) Pump(15);
Pump(50); // let the final scheduled poll publish a worker that just finished
if (engine.WarpBuildRemaining != 0) throw new Exception("MainWindow did not poll tempo caches to completion");
if (vm.Transport.Bpm != 145 || Math.Abs(engine.Bpm - 145) > 1e-8) throw new Exception("Latest BPM was lost");
Console.WriteLine($"PASS MainWindow debounce/poller completed latest tempo ({deadline.ElapsedMilliseconds} ms)");

// A pending tempo continuation must stop before touching a disposed engine.
vm.Transport.Bpm = 132;
typeof(MainWindow).GetField("_closeConfirmed", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
window.Close();
Pump(200);
Console.WriteLine("PASS closed-window continuation does not access the disposed engine");
return 0;

static void Pump(int milliseconds)
{
    var watch = Stopwatch.StartNew();
    do { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } while (watch.ElapsedMilliseconds < milliseconds);
}
