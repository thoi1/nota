// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
using System.Collections;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nota.App;
using Nota.Application;
using Nota.Presentation;

internal static class Program
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object? Get(object o, string n) => o.GetType().GetField(n, Hidden)!.GetValue(o);
    static object? Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Hidden)!.Invoke(o,a);
    [STAThread] static int Main(string[] args)
    {
        try { Run(Path.GetFullPath(args[0])); return 0; } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Run(string root)
    {
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("NOTA_DATA_DIR", Path.Combine(root,"probe-data"));
        var lifetime = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppBuilder.Configure<Nota.App.App>().UseWin32().UseSkia().UseHarfBuzz().WithNotaFonts().SetupWithLifetime(lifetime);
        var settings = (ISettingsService)App.Services.GetService(typeof(ISettingsService))!;
        settings.Current.ShowWelcomeOnStartup=false; settings.Current.CheckForUpdates=false; settings.Current.LastSeenVersion=AppInfo.Version;
        using var stop = new CancellationTokenSource(); Exception? failure=null; bool done=false;
        var checks = new List<string>();
        void Require(bool ok,string label) { if(!ok) throw new Exception(label); checks.Add(label); }
        DispatcherTimer.RunOnce(()=> {
            try {
                var roll = new PianoRollView();
                var notes = new[] { new NotaNote(60,0,1,.8f), new NotaNote(64,2,1,.7f) };
                roll.SetNotes(notes,16);
                var data = new Dictionary<int,AutomationPoint[]> {
                    [1]=[new(0,.2f),new(4,.8f)], [11]=[new(0,.4f),new(8,.9f)], [64]=[new(1,1),new(3,0)] };
                int commits=0;
                roll.ControllersInClip=()=>data.Keys.ToArray();
                roll.GetController=id=>data.GetValueOrDefault(id,[]);
                roll.SetController=(id,p)=>{ data[id]=p; commits++; };
                var window = new Window { Content=roll,Width=1000,Height=720,Title="CC lane validation",
                    Background=(IBrush)Avalonia.Application.Current!.Resources["Brush.BgApp"]! };
                window.Show(); window.UpdateLayout();
                var add = roll.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="AddControllerLane");
                void Add()=>add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Add();Add();Add(); window.UpdateLayout();
                var lanes=(IList)Get(roll,"_controllerLanes")!;
                Require(lanes.Count==3,"plus adds three independent lane views");
                object Lane(int i)=>lanes[i]!.GetType().GetProperty("Lane")!.GetValue(lanes[i])!;
                int Ctl(int i)=>(int)Lane(i).GetType().GetProperty("Controller",Hidden)!.GetValue(Lane(i))!;
                Require(new[]{Ctl(0),Ctl(1),Ctl(2)}.Distinct().Count()==3,"new lanes choose different controllers");
                Require(new[]{Ctl(0),Ctl(1),Ctl(2)}.Contains(64),"switch-type sustain shares the existing block editor");
                string Snapshot()=>JsonSerializer.Serialize(data,new JsonSerializerOptions{IncludeFields=true});
                string original=Snapshot();
                var remove=roll.GetVisualDescendants().OfType<Button>().First(b=>ToolTip.GetTip(b)?.ToString()?.StartsWith("Hide controller lane")==true);
                remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(lanes.Count==2 && Snapshot()==original && commits==0,"minus removes only a view and preserves all CC points");
                Add(); Require(lanes.Count==3 && Snapshot()==original,"readding a lane preserves controller data");
                int target=Ctl(0); var untouched=data.Where(p=>p.Key!=target).ToDictionary(p=>p.Key,p=>p.Value);
                Call(roll,"StoreController",target,new List<AutomationPoint>{new(0,.1f),new(2,.7f)});
                Require(commits==1 && untouched.All(p=>data[p.Key].SequenceEqual(p.Value)),"existing commit boundary edits only its target controller");
                var lane=Lane(0); var drag=lane.GetType().GetField("_drag",Hidden)!;
                drag.SetValue(lane,Enum.Parse(drag.FieldType,"Point"));
                lane.GetType().GetField("_work",Hidden)!.SetValue(lane,new List<AutomationPoint>{new(1,.5f)});
                roll.SetNotes(notes,16);
                Require(drag.GetValue(lane)!.ToString()=="None" && Get(lane,"_work") is null && lanes.Count==3,
                    "reload cancels stale controller gestures and keeps lane layout");
                for(int i=0;i<7;i++)Add();
                Call(roll,"ZoomAt",2.0,4.0); Call(roll,"SetScrollBeats",2.0); window.UpdateLayout();
                var sc=roll.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="ControllerLanes");
                Require(sc.Extent.Height>sc.Viewport.Height && sc.Bounds.Height<=224.1,"many lanes scroll in a bounded area");
                Require(((Control)Get(roll,"_gridControl")!).Bounds.Height>0,"note grid remains available");
                window.Height=300; window.UpdateLayout(); window.UpdateLayout();
                Require(((ScrollViewer)Get(roll,"_bodyScroll")!).Viewport.Height>=40,
                    "short editors retain a visible note viewport with many CC lanes");
                window.Height=720; window.UpdateLayout();
                Require(lanes.Cast<object>().All(x=>((Grid)x.GetType().GetProperty("Row")!.GetValue(x)!).ColumnDefinitions[0].Width.Value
                    ==((Grid)((Control)Get(roll,"_gridControl")!).Parent!).ColumnDefinitions[0].Width.Value),"all lane gutters follow the note-grid column");
                foreach(string theme in new[]{"studio-dark","logic-grey"}) {
                    typeof(MainWindow).Assembly.GetType("Nota.App.NotaThemeService")!.GetMethod("Set",BindingFlags.Public|BindingFlags.Static)!
                        .Invoke(null,new object[]{new AppearanceChoice(false,theme,"studio-dark","studio-light")});
                    using var bitmap=new RenderTargetBitmap(new PixelSize(1000,720)); bitmap.Render(window); bitmap.Save(Path.Combine(root,theme+"-cc.png"));
                }
                Require(commits==1,"view, theme and zoom operations never write controller data");
                File.WriteAllText(Path.Combine(root,"cc-results.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
                window.Close();
                // Drain theme-change work before disposing the real app's engine.
                Dispatcher.UIThread.Post(()=> {
                    typeof(MainWindow).GetField("_closeConfirmed",Hidden)!.SetValue(lifetime.MainWindow,true);
                    lifetime.MainWindow!.Close(); done=true; stop.Cancel();
                },DispatcherPriority.Background);
            }catch(Exception e){failure=e;stop.Cancel();}
        },TimeSpan.FromMilliseconds(900));
        using var watchdog=new Timer(_=>Dispatcher.UIThread.Post(()=>stop.Cancel()),null,15000,Timeout.Infinite);
        Dispatcher.UIThread.MainLoop(stop.Token);
        if(failure is not null)throw failure; if(!done)throw new Exception("Probe timed out");
        Console.WriteLine(string.Join(Environment.NewLine,checks));
    }
}
