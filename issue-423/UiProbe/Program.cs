// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Nota.App;
using Nota.Application;
using Nota.Presentation;
internal static class Program {
 const BindingFlags Flags = BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object? Field(object o,string n) => o.GetType().GetField(n,Flags)?.GetValue(o);
 static object? Call(object o,string n,params object[] a) => o.GetType().GetMethods(Flags).Single(m=>m.Name==n&&m.GetParameters().Length==a.Length).Invoke(o,a);
 static object? DrawList(Control c) { var v=ElementComposition.GetElementVisual(c);return v?.GetType().GetProperty("DrawList",Flags)?.GetValue(v); }
 [STAThread] static int Main(string[] args) {
  try { Run(args[0],Path.GetFullPath(args[1]));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
 static void Run(string label,string folder) {
  Directory.CreateDirectory(folder);
  Environment.SetEnvironmentVariable("NOTA_DATA_DIR",Path.Combine(folder,"probe-data",label));
  var lifetime=new ClassicDesktopStyleApplicationLifetime { ShutdownMode=ShutdownMode.OnExplicitShutdown };
  AppBuilder.Configure<Nota.App.App>().UseWin32().UseSkia().UseHarfBuzz().WithNotaFonts().SetupWithLifetime(lifetime);
  var settings=(ISettingsService)App.Services.GetService(typeof(ISettingsService))!;
  settings.Current.ShowWelcomeOnStartup=false;settings.Current.CheckForUpdates=false;settings.Current.LastSeenVersion=AppInfo.Version;
  var vm=(MainWindowViewModel)App.Services.GetService(typeof(MainWindowViewModel))!;
  using var stop=new CancellationTokenSource();Exception? error=null;bool completed=false;
  DispatcherTimer.RunOnce(()=>{
   try {
    var main=(MainWindow)lifetime.MainWindow!;
    main.WindowState=WindowState.Normal;main.Width=1280;main.Height=900;main.Activate();
    while(vm.Engine.TrackCount>0){vm.Engine.TryGetTrackInfo(0,out var t);vm.Engine.RemoveTrack(t.Id);}
    int id=vm.Engine.AddInstrumentTrack();int ci=vm.Engine.AddMidiClip(id,4,64);
    vm.Engine.SetTrackVolume(id,0);
    var notes=Enumerable.Range(0,128).Select(i=>new NotaNote(48+i%24,i*.5,.25,.6f)).ToArray();vm.Engine.SetClipNotes(id,ci,notes);
    var timeline=main.FindControl<ArrangementView>("Timeline")!;timeline.Refresh();timeline.SelectTrackFromOutside(id);
    Call(main,"BuildClipEditor",id,ci);Call(main,"ShowClipPanel");
    var roll=(PianoRollView)Field(main,"_editorRoll")!;var grid=(Control)Field(roll,"_gridControl")!;var ruler=(Control)Field(roll,"_ruler")!;
    vm.Engine.SetCountIn(0);vm.Engine.SetMetronome(false);vm.Engine.Seek(4);vm.Engine.Play();main.IsEnabled=false;
    var times=new List<double>();var arrTimes=new List<double>();int frames=0,gridDraws=0,rulerDraws=0;double last=double.NaN,lastArr=double.NaN;object? previousGrid=null,previousRuler=null;
    var clock=Stopwatch.StartNew();Action<TimeSpan>? sample=null;
    sample=_=>{
     try {
      double now=clock.Elapsed.TotalMilliseconds;
      if(now>=1000&&now<6000){
       frames++;double beat=(double)Field(roll,"_playheadBeat")!;double arr=(double)(Field(timeline,"_displayPlayheadBeats")??Field(timeline,"_playheadBeats"))!;
       if(beat!=last){times.Add(now);last=beat;}if(arr!=lastArr){arrTimes.Add(now);lastArr=arr;}
       var gd=DrawList(grid);var rd=DrawList(ruler);if(gd is null||rd is null)throw new InvalidOperationException("Missing composition draw list");
       if(previousGrid is not null&&!ReferenceEquals(previousGrid,gd))gridDraws++;if(previousRuler is not null&&!ReferenceEquals(previousRuler,rd))rulerDraws++;previousGrid=gd;previousRuler=rd;
      }
      if(now<6000){main.RequestAnimationFrame(sample!);return;}
      var gaps=times.Zip(times.Skip(1),(a,b)=>b-a).Order().ToArray();
      if(gaps.Length==0)throw new InvalidOperationException("No cursor updates sampled");
      if(label=="after"&&(times.Count<frames*.9||gridDraws!=0||rulerDraws!=0))throw new InvalidOperationException($"After cadence/isolation failed: {times.Count}/{frames}; grid {gridDraws}; ruler {rulerDraws}");
      var data=new{label,backend=vm.Engine.ActiveAudioSystem.ToString(),rate=vm.Engine.NegotiatedSampleRate,buffer=vm.Engine.NegotiatedBufferFrames,frames,cursorUpdates=times.Count,arrangementUpdates=arrTimes.Count,cursorUpdatesPerSecond=1000/gaps.Average(),medianGapMs=gaps[gaps.Length/2],p95GapMs=gaps[(int)(gaps.Length*.95)],gridDraws,rulerDraws,dropouts=vm.Engine.XrunCount};
      File.WriteAllText(Path.Combine(folder,"ui-"+label+".json"),JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
      vm.Engine.StopTransport();vm.Engine.SetLoop(false,0,4);vm.Engine.Seek(12);main.IsEnabled=true;
      DispatcherTimer.RunOnce(()=>{
       try {
        var setter=timeline.GetType().GetMethod("SetPlayhead")!;setter.Invoke(timeline,setter.GetParameters().Length==1?new object[]{12.0}:new object[]{12.0,false});roll.SetPlayhead(8,true);
        DispatcherTimer.RunOnce(()=>{
         try {using var image=new RenderTargetBitmap(new PixelSize(1280,900));image.Render(main);image.Save(Path.Combine(folder,"piano-roll-"+label+".png"));
          typeof(MainWindow).GetField("_closeConfirmed",Flags)!.SetValue(main,true);main.Close();completed=true;stop.Cancel();
         }catch(Exception e){error=e;stop.Cancel();}
        },TimeSpan.FromMilliseconds(100));
       }catch(Exception e){error=e;stop.Cancel();}
      },TimeSpan.FromMilliseconds(400));
     }catch(Exception e){error=e;stop.Cancel();}
    };main.RequestAnimationFrame(sample);
   }catch(Exception e){error=e;stop.Cancel();}
  },TimeSpan.FromMilliseconds(1000));
  using var watchdog=new Timer(_=>Dispatcher.UIThread.Post(()=>stop.Cancel()),null,25000,Timeout.Infinite);Dispatcher.UIThread.MainLoop(stop.Token);
  if(error is not null)throw error;if(!completed)throw new InvalidOperationException("UI probe timed out");Console.WriteLine(File.ReadAllText(Path.Combine(folder,"ui-"+label+".json")));
 }
}
