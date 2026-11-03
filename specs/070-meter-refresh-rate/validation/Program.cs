// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nota.App;
using Nota.Application;
using Nota.Infrastructure;
using Nota.Presentation;

// Measures production meter state and retained draw-list updates without changing app code.
// Neither animation callbacks nor draw-list changes prove physical screen presentation.
internal static class Program
{
    const BindingFlags Hidden = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static object? Field(object o, string name) => o.GetType().GetField(name, Hidden)!.GetValue(o);
    static void Call(object o, string name) => o.GetType().GetMethod(name, Hidden)!.Invoke(o, null);
    static object? DrawList(Control c)
    {
        var v = ElementComposition.GetElementVisual(c);
        return v?.GetType().GetProperty("DrawList", Hidden)?.GetValue(v);
    }
    sealed class MeterSample(string name, Func<MeterBar> getControl)
    {
        public readonly string Name = name;
        public MeterBar Control => getControl();
        public readonly List<double> Changes = new(), Draws = new();
        double _lastL = double.NaN, _lastR = double.NaN;
        object? _lastDraw;
        MeterBar? _lastControl;
        public int RebuiltControls;
        public double Min = double.PositiveInfinity, Max = 0;
        public void Sample(double now)
        {
            var control = Control;
            if (_lastControl is not null && !ReferenceEquals(_lastControl, control))
            {
                RebuiltControls++; _lastL = double.NaN; _lastDraw = null;
            }
            _lastControl = control;
            double l = (double)Field(control, "_levelL")!, r = (double)Field(control, "_levelR")!;
            Min = Math.Min(Min, Math.Min(l, r)); Max = Math.Max(Max, Math.Max(l, r));
            if (!double.IsNaN(_lastL) && (l != _lastL || r != _lastR)) Changes.Add(now);
            _lastL = l; _lastR = r;
            var draw = DrawList(control);
            if (draw is null) throw new InvalidOperationException(Name + " has no composition draw list; bounds=" + control.Bounds);
            if (_lastDraw is not null && !ReferenceEquals(draw, _lastDraw)) Draws.Add(now);
            _lastDraw = draw;
        }
        public object Result() => new { name = Name, effectivelyVisible = Control.IsEffectivelyVisible,
            rebuiltControls = RebuiltControls, stateChanges = Stats(Changes), drawListChanges = Stats(Draws), minNormalizedLevel = Min, maxNormalizedLevel = Max };
    }
    static object Stats(List<double> times)
    {
        var gaps = times.Zip(times.Skip(1), (a,b) => b-a).Order().ToArray();
        return new { count = times.Count, updatesPerSecond = gaps.Length == 0 ? (double?)null : 1000/gaps.Average(),
            medianGapMs = gaps.Length == 0 ? (double?)null : gaps[gaps.Length/2],
            p95GapMs = gaps.Length == 0 ? (double?)null : gaps[(int)(gaps.Length*.95)],
            maxGapMs = gaps.Length == 0 ? (double?)null : gaps[^1] };
    }
    [STAThread]
    static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Run(string[] args)
    {
        string output = Path.GetFullPath(args[0]), asio = args.Length > 1 ? args[1] : "";
        int hz = args.Length > 2 ? int.Parse(args[2]) : 30;
        string theme = args.Length > 3 ? args[3] : "studio-dark";
        string scratch = Path.Combine(Path.GetDirectoryName(output)!, "probe-data", Path.GetFileNameWithoutExtension(output));
        Directory.CreateDirectory(scratch);
        Environment.SetEnvironmentVariable("NOTA_DATA_DIR", scratch);
        if(asio=="restart")
        {
            if(new SettingsService().Current.MeterRefreshHz!=hz) throw new Exception("Meter rate did not persist across processes");
            Console.WriteLine("Meter preference persisted across a new process"); return;
        }
        var seed=new SettingsService(); seed.Current.ColourTheme=theme; seed.Save();
        var lifetime = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppBuilder.Configure<Nota.App.App>().UseWin32().UseSkia().UseHarfBuzz().WithNotaFonts().SetupWithLifetime(lifetime);
        var settings = (ISettingsService)App.Services.GetService(typeof(ISettingsService))!;
        settings.Current.ShowWelcomeOnStartup = false; settings.Current.CheckForUpdates = false;
        settings.Current.LastSeenVersion = AppInfo.Version;
        var vm = (MainWindowViewModel)App.Services.GetService(typeof(MainWindowViewModel))!;
        if (asio.Length > 0)
        {
            var driver = vm.Engine.AsioDrivers().Single(d => d.Name == asio);
            vm.Engine.SetAudioSystem(AudioSystem.Asio); vm.Engine.SetAsioDriver(driver.Uid); vm.ApplyAudioSettings();
        }
        if (!vm.Engine.IsRunning || (asio.Length > 0 && vm.Engine.ActiveAudioSystem != AudioSystem.Asio))
            throw new InvalidOperationException("Requested backend unavailable: " + vm.Engine.AudioFallbackReason);
        using var stop = new CancellationTokenSource();
        Exception? error = null; bool completed = false;
        DispatcherTimer.RunOnce(() =>
        {
            try
            {
                var main = (MainWindow)lifetime.MainWindow!;
                main.Width = 1400; main.Height = 960; main.WindowState = WindowState.Normal;
                while (vm.Engine.TrackCount > 0) { vm.Engine.TryGetTrackInfo(0, out var info); vm.Engine.RemoveTrack(info.Id); }
                string wave = Path.Combine(scratch, "meter-test.wav"); WriteSignal(wave);
                int track = vm.Engine.AddAudioTrack();
                if (vm.Engine.AddAudioClip(track, wave, 0) < 0) throw new InvalidOperationException("Could not import generated signal");
                vm.Engine.SetTrackName(track, "Meter cadence test");
                var timeline = main.FindControl<ArrangementView>("Timeline")!;
                timeline.Refresh(); timeline.SelectTrackFromOutside(track);
                Call(main, "ShowMixer");
                var mixer = (MixerView)Field(main, "_mixer")!; mixer.Refresh();
                var prefs=new PreferencesWindow(new SettingsViewModel(settings),vm);
                prefs.Show(); prefs.GetType().GetMethod("Select",Hidden)!.Invoke(prefs,new object[]{7}); prefs.UpdateLayout();
                var picker=Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(prefs).OfType<ComboBox>().Single(c=>c.Name=="MeterRefreshRate");
                if(picker.ItemCount!=2)throw new Exception("Expected exactly two refresh options");
                picker.SelectedIndex=hz==60?0:1; picker.SelectedIndex=hz==60?1:0;
                if(settings.Current.MeterRefreshHz!=hz)throw new Exception("Real Appearance selection failed");
                picker.BringIntoView(); prefs.UpdateLayout();
                using(var bitmap=new RenderTargetBitmap(new PixelSize((int)prefs.Bounds.Width,(int)prefs.Bounds.Height)))
                {bitmap.Render(prefs);bitmap.Save(Path.ChangeExtension(output,".png"));}
                prefs.Close();
                if(args.Length>4 && args[4]=="capture")
                {
                    Dispatcher.UIThread.Post(()=>{
                        typeof(MainWindow).GetField("_closeConfirmed",Hidden)!.SetValue(main,true);
                        main.Close();completed=true;stop.Cancel();
                    },DispatcherPriority.Background);
                    return;
                }
                var meters = new List<MeterSample> {
                    new("transport master", () => (MeterBar)Field(main, "_masterMeter")!),
                    new("arrangement track", () => ((Dictionary<int, MeterBar>)Field(timeline, "_meters")!)[track]),
                    new("arrangement master", () => (MeterBar)Field(timeline, "_masterRowMeter")!)
                };
                foreach (var value in (IEnumerable)Field(mixer, "_meters")!)
                {
                    int id = (int)value.GetType().GetField("Item1")!.GetValue(value)!;
                    meters.Add(new(id < 0 ? "mixer master" : "mixer track",
                        () => ((List<(int, MeterBar)>)Field(mixer, "_meters")!).Single(m => m.Item1 == id).Item2));
                }
                if (meters.Count != 5) throw new InvalidOperationException("Expected five meter instances");
                main.IsEnabled = false; // isolate automatic updates from real hover/zoom/edit input
                vm.Engine.SetCountIn(0); vm.Engine.SetMetronome(false); vm.Engine.Seek(0); vm.Engine.Play();
                var clock = Stopwatch.StartNew();
                var frames = new List<double>(); var ticks = new List<double>();
                int cursorChanges=0; double cursor=double.NaN;
                TimeSpan cpuStart=default; long allocationStart=0; bool measuring=false;
                var xrunStart = vm.Engine.XrunCount;
                void Tick(double _) { double t = clock.Elapsed.TotalMilliseconds; if (t >= 1000 && t < 11000) ticks.Add(t); }
                vm.PlayheadUpdated += Tick;
                Action<TimeSpan>? sample = null;
                sample = _ =>
                {
                    try
                    {
                        double now = clock.Elapsed.TotalMilliseconds;
                        if (now >= 1000 && now < 11000)
                        {
                            frames.Add(now);
                            if(!measuring){measuring=true;cpuStart=Environment.CpuUsage.TotalTime;allocationStart=GC.GetTotalAllocatedBytes(false);}
                            double position=(double)Field(timeline,"_displayPlayheadBeats")!;
                            if(position!=cursor){cursorChanges++;cursor=position;}
                            foreach (var meter in meters)
                            {
                                if (!meter.Control.IsEffectivelyVisible) throw new InvalidOperationException("Hidden meter: " + meter.Name);
                                meter.Sample(now);
                            }
                        }
                        if (now >= 11000)
                        {
                            double processCpuMs=(Environment.CpuUsage.TotalTime-cpuStart).TotalMilliseconds;
                            long allocatedBytes=GC.GetTotalAllocatedBytes(false)-allocationStart;
                            if (meters.Any(m => m.Changes.Count < 10 || m.Max <= m.Min))
                                throw new InvalidOperationException("Test signal failed to produce varying meter readings");
                            if (asio.Length > 0 && vm.Engine.ActiveAudioSystem != AudioSystem.Asio)
                                throw new InvalidOperationException("ASIO fell back during measurement");
                            var drawing=Stopwatch.StartNew();
                            for(int i=0;i<1000;i++) { var group=new DrawingGroup(); using var context=group.Open(); foreach(var m in meters)m.Control.Render(context); }
                            drawing.Stop();
                            var checks=new List<string>{"real Appearance picker has exactly two choices and applies live"};
                            var result = new { platform = Environment.OSVersion.ToString(), hz, theme,
                                backend = vm.Engine.ActiveAudioSystem.ToString(), requestedAsioDriver = asio,
                                sampleRate = vm.Engine.SampleRate, bufferFrames = vm.Engine.NegotiatedBufferFrames,
                                measuredSeconds = 10, animationCallbacks = Stats(frames), serviceTicks = Stats(ticks),
                                meters = meters.Select(m => m.Result()).ToArray(), dropouts = vm.Engine.XrunCount - xrunStart,
                                cursorChanges, processCpuMs, allocatedBytes,
                                fiveMeterRecordingMicroseconds=drawing.Elapsed.TotalMilliseconds*1000/1000, checks };
                            vm.PlayheadUpdated -= Tick;
                            var master=(MeterBar)Field(main,"_masterMeter")!;
                            var mixerBar=meters.First(m=>m.Name=="mixer master").Control;
                            double Level(MeterBar m)=>(double)Field(m,"_levelL")!;
                            void Require(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
                            void Later(Action action)=>DispatcherTimer.RunOnce(()=>{try{action();}catch(Exception e){error=e;stop.Cancel();}},TimeSpan.FromMilliseconds(400));
                            Call(main,"HideMixer"); double hidden=Level(mixerBar);
                            Later(()=>{
                                Require(Level(mixerBar)==hidden,"hidden mixer receives no level updates");
                                Call(main,"ShowMixer"); main.GetType().GetMethod("SetMixerFloating",Hidden)!.Invoke(main,new object[]{true});
                                main.IsEnabled=true; main.WindowState=WindowState.Minimized; double ownHidden=Level(master);
                                var floating=(Window)Field(main,"_mixerWindow")!;
                                Later(()=>{
                                    Require(ReferenceEquals(Field(main,"_playheadFrameOwner"),floating),"floating mixer owns frames with main minimized");
                                    Require(Level(master)==ownHidden,"minimized main receives no level updates");
                                    vm.SuspendEnginePolling=true;
                                    Later(()=>{
                                        Require(Field(main,"_playheadFrameOwner") is null,"export suspension stops presentation loop");
                                        vm.SuspendEnginePolling=false;
                                        Later(()=>{
                                            Require(ReferenceEquals(Field(main,"_playheadFrameOwner"),floating),"resuming restarts floating presentation");
                                            floating.WindowState=WindowState.Minimized;
                                            Later(()=>{
                                                Require(Field(main,"_playheadFrameOwner") is null,"all minimized windows stop the shared loop");
                                                main.WindowState=WindowState.Normal; floating.WindowState=WindowState.Normal; vm.Engine.StopTransport();
                                                Later(()=>{
                                                    Require(Field(main,"_playheadFrameOwner") is not null,"visible live-input meters keep refreshing while stopped");
                                                    File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
                                                    Dispatcher.UIThread.Post(()=>{
                                                        typeof(MainWindow).GetField("_closeConfirmed",Hidden)!.SetValue(main,true);main.Close();completed=true;stop.Cancel();
                                                    },DispatcherPriority.Background);
                                                });
                                            });
                                        });
                                    });
                                });
                            }); return;
                        }
                        main.RequestAnimationFrame(sample!);
                    }
                    catch (Exception e) { error = e; stop.Cancel(); }
                };
                main.RequestAnimationFrame(sample);
            }
            catch (Exception e) { error = e; stop.Cancel(); }
        }, TimeSpan.FromMilliseconds(800));
        using var watchdog = new Timer(_ => Dispatcher.UIThread.Post(() => stop.Cancel()), null, 25000, Timeout.Infinite);
        Dispatcher.UIThread.MainLoop(stop.Token);
        if (error is not null) throw error;
        if (!completed) throw new InvalidOperationException("Timed out before measurement completed");
        Console.WriteLine(File.Exists(output)?File.ReadAllText(output):"Appearance capture completed");
    }
    static void WriteSignal(string path)
    {
        const int rate = 48000, samples = rate * 20;
        using var w = new BinaryWriter(File.Create(path));
        void Tag(string s) => w.Write(System.Text.Encoding.ASCII.GetBytes(s));
        Tag("RIFF"); w.Write(36 + samples * 4); Tag("WAVE"); Tag("fmt ");
        w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * 4);
        w.Write((short)4); w.Write((short)16); Tag("data"); w.Write(samples * 4);
        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / rate;
            double ampL = .003 + .012 * (.5 + .5 * Math.Sin(2 * Math.PI * 1.71 * t));
            double ampR = .003 + .012 * (.5 + .5 * Math.Sin(2 * Math.PI * 2.37 * t + .4));
            w.Write((short)(32767 * ampL * Math.Sin(2 * Math.PI * 440 * t)));
            w.Write((short)(32767 * ampR * Math.Sin(2 * Math.PI * 660 * t)));
        }
    }
}
