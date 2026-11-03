// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nota.App;
using Nota.Application;
using Nota.Application.Editing;
using Nota.Presentation;

internal static class Program
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object? Get(object o,string name)=>o.GetType().GetField(name,Hidden)!.GetValue(o);
    static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethods(Hidden)
        .Single(m=>m.Name==name && m.GetParameters().Length==args.Length).Invoke(o,args);
    [STAThread] static int Main(string[] args)
    {
        if(args[0]=="--read-lanes")
        {
            Environment.SetEnvironmentVariable("NOTA_DATA_DIR",Path.GetFullPath(args[1]));
            var heights=new Nota.Infrastructure.SettingsService().Current.PianoRollLaneHeights;
            bool ok=heights.GetValueOrDefault("Velocity")==122 && heights.GetValueOrDefault("extra:0:cc:1")==157
                && heights.GetValueOrDefault("extra:1:cc:11")==197;
            Console.WriteLine(ok?"fresh process restores all lane heights":"lane height restart failed");return ok?0:1;
        }
        try { Run(Path.GetFullPath(args[0])); return 0; }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Run(string root)
    {
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("NOTA_DATA_DIR",Path.Combine(root,"probe-data"));
        var lifetime=new ClassicDesktopStyleApplicationLifetime { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        AppBuilder.Configure<Nota.App.App>().UseWin32().UseSkia().UseHarfBuzz().WithNotaFonts().SetupWithLifetime(lifetime);
        var settings=(ISettingsService)App.Services.GetService(typeof(ISettingsService))!;
        settings.Current.ShowWelcomeOnStartup=false; settings.Current.CheckForUpdates=false; settings.Current.LastSeenVersion=AppInfo.Version;
        settings.Current.PianoRollLaneHeights.Clear();
        var vm=(MainWindowViewModel)App.Services.GetService(typeof(MainWindowViewModel))!;
        using var stop=new CancellationTokenSource(); Exception? failure=null; bool done=false;
        var checks=new List<string>();
        void Require(bool ok,string label) { if(!ok)throw new Exception(label); checks.Add(label); Console.WriteLine(label); }
        DispatcherTimer.RunOnce(async ()=> {
            Window? window=null;
            try {
                var main=(MainWindow)lifetime.MainWindow!;
                main.WindowState=WindowState.Normal; main.Width=1280;main.Height=900;
                int id=vm.Engine.AddInstrumentTrack(); int ci=vm.Engine.AddMidiClip(id,0,64);
                var notes=new[]{new NotaNote(60,5,1,.8f),new NotaNote(64,8,.25,.5f)};
                vm.Engine.SetClipNotes(id,ci,notes);
                var timeline=main.FindControl<ArrangementView>("Timeline")!;
                timeline.Refresh(); timeline.SelectTrackFromOutside(id);
                Call(main,"ShowDevices",id,true); main.UpdateLayout();
                if(!(bool)Get(main,"_mixerDocked")!)Call(main,"ToggleMixer");
                main.UpdateLayout();
                var panel=main.FindControl<Border>("DetailPanel")!;
                var host=main.FindControl<ContentControl>("DetailBody")!;
                var chain=Get(main,"_deviceChain"); double wanted=(double)Get(main,"_detailWanted")!;
                vm.Engine.Seek(5);vm.Engine.Play();
                Call(main,"OpenClipEditor",id,ci); main.UpdateLayout();
                await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);
                await Task.Delay(100);
                var firstRoll=(PianoRollView)Get(main,"_editorRoll")!;
                Require((bool)Get(main,"_detailMaximized")! && host.Content is ClipEditorView && panel.IsVisible,
                    "MIDI activation enters the main workspace");
                Require(!main.FindControl<Grid>("BrowserGrid")!.IsVisible && !main.FindControl<Control>("DetailSplitter")!.IsVisible
                    && !main.FindControl<Control>("MixerPanel")!.IsVisible,"workspace removes main/Devices/Mixer space and splitter");
                Require(main.FindControl<TextBlock>("DetailReturnLabel")!.IsVisible,"workspace has an explicit return label");
                Require(firstRoll.GridFocused,"workspace focuses note editing");
                Require(vm.Engine.IsPlaying && vm.Engine.TryGetTrackMeter(id,out var sounding) && sounding.Peak>0,
                    "hidden Devices continue processing audible instrument playback");
                Call(main,"SetDetailMaximized",false);main.UpdateLayout();
                Require(ReferenceEquals(host.Content,chain) && panel.IsVisible && Math.Abs((double)Get(main,"_detailWanted")!-wanted)<.01,
                    "return restores the same device chain and prior desired height");
                Require(main.FindControl<Control>("MixerPanel")!.IsVisible,"return restores a previously visible docked Mixer");
                Call(main,"OpenClipEditor",id,ci);main.UpdateLayout();
                Require(ReferenceEquals(firstRoll,Get(main,"_editorRoll")),"reopening the same MIDI selection reuses its roll");
                Call(main,"SetDetailMaximized",false);
                Call(main,"CloseDetail");main.UpdateLayout();
                Require(!panel.IsVisible && ((RowDefinition)Get(main,"_detailRow")!).Height.Value==0,
                    "docked close removes the complete detail row");
                Call(main,"OpenClipEditor",id,ci); Call(main,"SetDetailMaximized",false);main.UpdateLayout();
                Require(!panel.IsVisible,"return preserves a manually closed detail panel");
                Call(main,"ShowDevices",id,true);Call(main,"OpenClipEditor",id,ci);Call(main,"SetDetailMaximized",false);
                Call(main,"OnToggleDetailWindow",main,new RoutedEventArgs());main.UpdateLayout();
                var floating=(Window)Get(main,"_detailWindow")!;floating.UpdateLayout();
                var chainHost=(ContentControl)floating.GetType().GetProperty("ChainHost")!.GetValue(floating)!;
                var devicesVisible=floating.GetType().GetProperty("DevicesVisible")!;
                var close=floating.GetVisualDescendants().OfType<Button>().Single(b=>ToolTip.GetTip(b)?.ToString()=="Close devices panel");
                close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); floating.UpdateLayout();
                Require(!(bool)devicesVisible.GetValue(floating)! && !chainHost.IsEffectivelyVisible
                    && ((Grid)((Control)Get(floating,"_devices")!).Parent!).RowDefinitions[1].ActualHeight<1,
                    "floating close hides the actual Volt/Sends container");
                Call(main,"OnTrackSelected",id);floating.UpdateLayout();
                Require(!(bool)devicesVisible.GetValue(floating)!,"track selection does not reopen manually hidden floating Devices");
                Call(main,"OpenClipEditor",id,ci);main.UpdateLayout();
                Require(!floating.IsVisible && (bool)Get(main,"_detailMaximized")!,"floating editor moves into main workspace without a new window");
                var movedRoll=Get(main,"_editorRoll");
                Call(main,"SetDetailMaximized",false);floating.UpdateLayout();
                Require(ReferenceEquals(floating,Get(main,"_detailWindow")) && floating.IsVisible
                    && !(bool)devicesVisible.GetValue(floating)! && ReferenceEquals(movedRoll,Get(main,"_editorRoll")),
                    "return restores the same floating window, editor and hidden Devices state");
                Call(main,"OpenClipEditor",id,ci);main.UpdateLayout();
                Call(main,"OnToggleDetailWindow",main,new RoutedEventArgs());floating.UpdateLayout();
                Require(ReferenceEquals(floating,Get(main,"_detailWindow")) && floating.IsVisible,
                    "workspace pop-out returns to the existing detached window instead of closing it");
                Call(main,"ShowDevices",id,true);floating.UpdateLayout();
                Require((bool)devicesVisible.GetValue(floating)! && ReferenceEquals(chainHost.Content,chain),
                    "existing Devices command reopens the same floating chain");
                floating.Close();
                Require(vm.Engine.GetClipNotes(id,ci).SequenceEqual(notes),"workspace changes preserve MIDI notes");
                vm.Engine.Stop();
                string wav=Path.Combine(root,"probe-data","audio.wav");
                using(var output=new BinaryWriter(File.Create(wav)))
                {
                    output.Write("RIFF"u8);output.Write(36+960);output.Write("WAVEfmt "u8);output.Write(16);
                    output.Write((short)1);output.Write((short)1);output.Write(48000);output.Write(96000);
                    output.Write((short)2);output.Write((short)16);output.Write("data"u8);output.Write(960);
                    for(int i=0;i<480;i++)output.Write((short)(2000*Math.Sin(i*.1)));
                }
                int audio=vm.Engine.AddAudioTrack();int audioClip=vm.Engine.AddAudioClip(audio,wav,0);
                Call(main,"OpenAudioClipEditor",audio,audioClip);main.UpdateLayout();
                Require(!(bool)Get(main,"_detailMaximized")! && host.Content is AudioClipEditorView,
                    "audio clip activation keeps the ordinary split editor");

                var roll=new PianoRollView { LaneSettings=settings };
                roll.SetNotes(notes,256);
                var data=new Dictionary<int,AutomationPoint[]> {[1]=[new(0,.2f),new(4,.8f)],[11]=[new(0,.3f),new(8,.7f)]};
                int commits=0;roll.Commit=_=>commits++;roll.ControllersInClip=()=>data.Keys.ToArray();
                roll.GetController=c=>data.GetValueOrDefault(c,[]);roll.SetController=(c,p)=>{data[c]=p;commits++;};
                window=new Window { Content=roll,Width=1100,Height=850,Title="Piano roll workspace validation" };
                window.Show();window.UpdateLayout();
                var add=roll.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="AddControllerLane");
                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
                var lanes=(IList)Get(roll,"_controllerLanes")!;
                Grid Row(int i)=>(Grid)lanes[i]!.GetType().GetProperty("Row")!.GetValue(lanes[i])!;
                string Key(int i)=>(string)lanes[i]!.GetType().GetProperty("HeightKey")!.GetValue(lanes[i])!;
                var primary=(Grid)Get(roll,"_laneRow")!;
                void Drag(Grid row,double growth)
                {
                    var divider=((DockPanel)row.Parent!).Children.OfType<Border>().Single();
                    var p=divider.TranslatePoint(new Point(20,3),window)!.Value;
                    using var pointer=new Avalonia.Input.Pointer(42,PointerType.Mouse,true);
                    var down=new PointerPointProperties(RawInputModifiers.LeftMouseButton,PointerUpdateKind.LeftButtonPressed);
                    divider.RaiseEvent(new PointerPressedEventArgs(divider,pointer,window,p,1,down,KeyModifiers.None,1));
                    divider.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent,divider,pointer,window,p.WithY(p.Y-growth),2,down,KeyModifiers.None));
                    divider.RaiseEvent(new PointerReleasedEventArgs(divider,pointer,window,p.WithY(p.Y-growth),3,
                        new PointerPointProperties(RawInputModifiers.None,PointerUpdateKind.LeftButtonReleased),KeyModifiers.None,MouseButton.Left));
                    window.UpdateLayout();
                }
                Drag(primary,50);Drag(Row(0),45);Drag(Row(1),85);
                Require(primary.Height==122 && Row(0).Height==157 && Row(1).Height==197,
                    "actual pointer drags resize Velocity and two CC lanes independently");
                Require(settings.Current.PianoRollLaneHeights["Velocity"]==122 && settings.Current.PianoRollLaneHeights[Key(0)]==157
                    && settings.Current.PianoRollLaneHeights[Key(1)]==197,"lane resize release persists three independent heights");
                var duplicate=lanes[1]!.GetType().GetProperty("Lane")!.GetValue(lanes[1])!;
                duplicate.GetType().GetProperty("Controller",Hidden)!.SetValue(duplicate,1);
                Call(roll,"FitControllerLanes");
                Drag(Row(1),20);
                Require(Row(0).Height==157 && Row(1).Height==132,"even two lanes for the same CC resize independently");
                duplicate.GetType().GetProperty("Controller",Hidden)!.SetValue(duplicate,11);
                Call(roll,"FitControllerLanes");
                var reopened=new PianoRollView {LaneSettings=settings,ControllersInClip=roll.ControllersInClip,GetController=roll.GetController};reopened.SetNotes(notes,256);
                var reopenedWindow=new Window {Content=reopened,Width=1100,Height=850};reopenedWindow.Show();reopenedWindow.UpdateLayout();
                Require(((Grid)Get(reopened,"_laneRow")!).Height==122,"reopening an editor restores its primary height");
                var reopenedAdd=reopened.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="AddControllerLane");
                reopenedAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));reopenedAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));reopenedWindow.UpdateLayout();
                var restoredLanes=(IList)Get(reopened,"_controllerLanes")!;
                Require(((Grid)restoredLanes[0]!.GetType().GetProperty("Row")!.GetValue(restoredLanes[0])!).Height==157
                    && ((Grid)restoredLanes[1]!.GetType().GetProperty("Row")!.GetValue(restoredLanes[1])!).Height==197,
                    "readding CC lanes restores their individual saved heights");reopenedWindow.Close();
                foreach(double invalid in new[]{-100.0,0.0,99999.0,double.NaN,double.PositiveInfinity})
                    Require(MidiLaneHeight.Normalize(invalid,112) is >=48 and <=480,"lane height safely normalizes "+invalid);
                for(int i=0;i<8;i++)add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.Height=300;window.UpdateLayout();window.UpdateLayout();
                Require(((ScrollViewer)Get(roll,"_bodyScroll")!).Viewport.Height>=40,"short editors retain note editing with resized CC lanes");
                window.Width=540;window.UpdateLayout();Require(roll.Bounds.Width>0,"narrow windows retain a valid roll layout");
                window.Width=1100;window.Height=850;window.UpdateLayout();
                var vel=(Control)Get(roll,"_vel")!;
                foreach(double scale in new[]{1.0,1.25,1.5,2.0})
                {
                    window.Content=null;
                    var scaled=new LayoutTransformControl { LayoutTransform=new ScaleTransform(scale,scale), Child=roll };
                    window.Content=scaled;window.UpdateLayout();
                    foreach(double ppb in new[]{6.0,400.0,1600.0,3200.0})
                    foreach(int gridIndex in new[]{0,1,3})
                    {
                    EditorGrids.Shared.Set(EditorKind.PianoRoll,new GridSetting {Adaptive=false,FixedIndex=gridIndex});
                    typeof(PianoRollView).GetField("_pixelsPerBeat",Hidden)!.SetValue(roll,ppb);
                    Call(roll,"SetScrollBeats",4.8125);
                    double expected=(double)Call(roll,"BeatToX",5.0)!;
                    var drawing=new DrawingGroup();using(var context=drawing.Open())vel.Render(context);
                    var stems=drawing.Children.OfType<GeometryDrawing>().Where(d=>d.Geometry is { } shape && Math.Abs(shape.Bounds.Width-2)<.001).ToArray();
                    Require(stems.Length==2 && Math.Abs((stems[0].Geometry!.Bounds.Center.X-expected)*scale)<1e-8,
                        $"velocity onset is exact at {ppb} px/beat, {scale*100}% scale, grid index {gridIndex}");
                    double stemX=vel.TranslatePoint(new Point(stems[0].Geometry!.Bounds.Center.X,0),window)!.Value.X;
                    double noteX=((Control)Get(roll,"_gridControl")!).TranslatePoint(new Point(expected,0),window)!.Value.X;
                    Require(Math.Abs(stemX-noteX)<1e-8,"scaled lane and note-grid origins coincide");
                    int hit=(int)Call(vel,"Nearest",new Point(expected,vel.Bounds.Height-.8*(vel.Bounds.Height-8)))!;
                    Require(hit==0,"velocity cap picking shares its exact onset");
                    }
                    scaled.Child=null;window.Content=roll;window.UpdateLayout();
                }
                Call(roll,"ZoomAt",1000.0,5.0);
                Require((double)Get(roll,"_pixelsPerBeat")! ==3200,"maximum zoom reaches eight times the old limit");
                Call(roll,"SetScrollBeats",4.0);double anchor=4.1;double before=(double)Call(roll,"BeatToX",anchor)!;
                Call(roll,"ZoomAt",.5,anchor);
                Require(Math.Abs((double)Call(roll,"BeatToX",anchor)!-before)<1e-8,"zoom retains pointer anchoring");
                Call(roll,"SetScrollBeats",double.MaxValue);
                var horizontal=(Avalonia.Controls.Primitives.ScrollBar)Get(roll,"_hScroll")!;
                Require(Math.Abs(horizontal.Value-horizontal.Maximum)<1e-8,"extreme horizontal scrolling clamps accurately");
                Call(roll,"ZoomAt",.000001,0.0);Require((double)Get(roll,"_pixelsPerBeat")! ==6,"original zoom-out limit remains unchanged");
                Call(roll,"ZoomAt",100000.0,5.0);Call(roll,"SetScrollBeats",4.8125);
                var watch=Stopwatch.StartNew();
                for(int i=0;i<200;i++){var drawing=new DrawingGroup();using var context=drawing.Open();((Control)Get(roll,"_ruler")!).Render(context);}
                watch.Stop();Require(watch.ElapsedMilliseconds<2000,"extreme-zoom ruler rendering stays bounded");
                Require(commits==0 && data[1].Length==2 && data[11].Length==2,"layout, scaling and zoom never create MIDI edits or undo steps");
                while(lanes.Count>2)Call(roll,"RemoveControllerLane",lanes[lanes.Count-1]!);
                Call(roll,"ZoomAt",400.0/(double)Get(roll,"_pixelsPerBeat")!,5.0);Call(roll,"SetScrollBeats",4.0);window.UpdateLayout();
                typeof(MainWindow).Assembly.GetType("Nota.App.NotaThemeService")!.GetMethod("Set",BindingFlags.Public|BindingFlags.Static)!
                    .Invoke(null,[new AppearanceChoice(false,"logic-grey","studio-dark","studio-light")]);
                using(var bitmap=new RenderTargetBitmap(new PixelSize(1100,850))){bitmap.Render(window);bitmap.Save(Path.Combine(root,"logic-grey-lanes.png"));}
                File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{checks,ruler200Ms=watch.ElapsedMilliseconds,
                    renderScaling=window.RenderScaling,settings= settings.Current.PianoRollLaneHeights},new JsonSerializerOptions{WriteIndented=true}));
                window.Close();window=null;
                Dispatcher.UIThread.Post(()=>{ typeof(MainWindow).GetField("_closeConfirmed",Hidden)!.SetValue(main,true);main.Close();done=true;stop.Cancel();},DispatcherPriority.Background);
            } catch(Exception e){failure=e;window?.Close();stop.Cancel();}
        },TimeSpan.FromMilliseconds(1000));
        using var watchdog=new Timer(_=>Dispatcher.UIThread.Post(()=>stop.Cancel()),null,30000,Timeout.Infinite);
        Dispatcher.UIThread.MainLoop(stop.Token);
        if(failure is not null)throw failure;if(!done)throw new Exception("Probe timed out");
        Console.WriteLine(string.Join(Environment.NewLine,checks));
    }
}
