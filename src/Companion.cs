using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Qa3moz {
    // Owns one new window; no other process/window is enumerated or addressed.
    internal sealed class Companion : Form {
        readonly Motion motion;
        readonly Bitmap atlas;
        readonly Interaction interaction=new Interaction();
        readonly Random animationRandom=new Random(Environment.TickCount^7727);
        readonly PixelArt pixel;
        readonly BehaviorOptions options;
        readonly LocalHabits habits;
        readonly ScreenPolicy screens=new ScreenPolicy();
        readonly SpeechBubble speech=new SpeechBubble();
        List<Display> allDisplays;
        readonly string optionsPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"behavior-settings.json");
        readonly Timer timer=new Timer();
        readonly Stopwatch clock=new Stopwatch();
        readonly NotifyIcon tray=new NotifyIcon();
        readonly PetMenu menu=new PetMenu();
        readonly ToolStripMenuItem pauseItem=new ToolStripMenuItem();
        readonly ToolStripMenuItem followItem=new ToolStripMenuItem("Occasional cursor approach");
        readonly ToolStripMenuItem startupItem=new ToolStripMenuItem("Start when I sign in");
        readonly ToolStripMenuItem quietItem=new ToolStripMenuItem("Quiet / presentation mode");
        readonly Dictionary<string,IntPtr> frames=new Dictionary<string,IntPtr>();
        readonly Dictionary<string,Bitmap> masks=new Dictionary<string,Bitmap>();
        Bitmap hitFrame;
        IntPtr memoryDc;
        double previous,cacheScale=-1;
        double lastScreenPoll=-10,lastBehaviorPoll=-10,cachedIdle;
        bool contextHidden;
        bool locked,suspended,ready,cleaned,exiting,hidden;
        bool firstDraw=true,releasingCapture;
        double sizeFactor=.70;
        IntPtr lastImage;
        int lastX,lastY,lastAlpha=-1;
        const uint Mods=0x4000|1|2|4; // NOREPEAT + Alt + Ctrl + Shift; no keyboard hook.
        const int PauseKey=0x50, ExitKey=0x51, FollowKey=0x46;
        public Companion(string asset,bool startPaused=true) {
            options=BehaviorOptions.Load(optionsPath);habits=new LocalHabits(Environment.TickCount^113,options);
            using(var source=new Bitmap(asset)) {
                if(source.Width!=AtlasLayout.Width || source.Height!=AtlasLayout.Height)
                    throw new InvalidDataException("Expected the original 1536 x 2288 v2 sprite sheet");
                atlas=new Bitmap(source);
            }
            pixel=new PixelArt(Path.Combine(Path.GetDirectoryName(asset),"pixel"));
            if(pixel.Ready) {
                interaction.PetDuration=pixel.Duration("petting_happy"); interaction.LandingDuration=pixel.Duration("land");
                interaction.YawnDuration=pixel.Duration("yawn"); interaction.StretchDuration=pixel.Duration("stretch");
                interaction.CurlDuration=pixel.Duration("curl_sleep"); interaction.WakeDuration=pixel.Duration("wake");
                if(pixel.HasMoves){interaction.UseSixMoves=true;interaction.StretchDuration=pixel.Duration("sleepy_stretch");
                    interaction.HappyDuration=pixel.Duration("happy_jump");interaction.SomersaultDuration=pixel.Duration("somersault");
                    interaction.ScratchDuration=pixel.Duration("ear_scratch");interaction.RequestDuration=pixel.Duration("request_petting");}
            }
            motion=new Motion(Native.Displays(),Environment.TickCount); motion.Paused=startPaused;
            allDisplays=motion.Displays;motion.Variation=options.Variation;
            motion.UseSixMoves=pixel.HasMoves;
            if(pixel.Ready) {
                sizeFactor=256/192.0;
                foreach(Display display in motion.Displays){display.SizeFactor=sizeFactor;display.Square=true;}
                motion.CancelMotion();
            }
            FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
            StartPosition=FormStartPosition.Manual; Text="Pawquilt";
            AutoScaleMode=AutoScaleMode.None;
            BuildMenu();
            timer.Interval=34; timer.Tick+=Tick;
            Load+=Loaded;
            Shown+=delegate {
                if(contextHidden || hidden || options.Quiet)Hide();
                Diagnostics.Write("SHOWN "+Native.OwnWindow(Handle));
                BeginInvoke((Action)delegate { Diagnostics.Write("AFTER SHOWN "+Native.OwnWindow(Handle)); });
            };
            foreach(Display display in motion.Displays)
                Diagnostics.Write("MONITOR "+display.Id+" work="+display.Left+","+display.Top+","+display.Right+","+display.Bottom+" dpiScale="+display.Scale);
            SystemEvents.DisplaySettingsChanged+=DisplayChanged;
            SystemEvents.PowerModeChanged+=PowerChanged;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get {
                CreateParams cp=base.CreateParams;
                cp.ExStyle|=0x80000|0x80|0x08000000; // LAYERED, TOOLWINDOW, NOACTIVATE. Alpha controls hit testing.
                return cp;
            }
        }
        void BuildMenu() {
            pauseItem.Text="Start roaming";
            pauseItem.Click+=delegate { TogglePause(); };
            followItem.CheckOnClick=true;
            followItem.Click+=delegate { motion.Follow=followItem.Checked; UpdateControls(); };
            var reset=new ToolStripMenuItem("Move to display");
            reset.DropDownOpening+=delegate {
                reset.DropDownItems.Clear();
                for(int i=0;i<motion.Displays.Count;i++) {
                    int index=i; var display=motion.Displays[i];
                    var item=new ToolStripMenuItem("Display "+(i+1));
                    item.Click+=delegate {
                        CancelInteraction();
                        motion.Reposition(index,display.Center-new Vec(display.PetWidth/2,display.PetHeight/2));
                        DrawPet();
                    };
                    reset.DropDownItems.Add(item);
                }
            };
            var sleep=new ToolStripMenuItem("Rest after user idle");
            foreach(int seconds in new[]{60,180,600}) {
                int threshold=seconds;
                var item=new ToolStripMenuItem((seconds/60)+" minutes");
                item.Checked=seconds==180;
                item.Click+=delegate {
                    motion.SleepAfter=threshold;
                    foreach(ToolStripMenuItem option in sleep.DropDownItems) option.Checked=option==item;
                };
                sleep.DropDownItems.Add(item);
            }
            var restNow=new ToolStripMenuItem("Rest now");
            restNow.Click+=delegate {
                CancelInteraction(); interaction.RestNow(); DrawPet();
            };
            var wakeNow=new ToolStripMenuItem("Wake up");
            wakeNow.Click+=delegate {CancelInteraction();interaction.WakeNow();DrawPet();};
            var sizes=new ToolStripMenuItem("Pet size");
            foreach(int logical in new[]{128,192,256}) {
                int chosen=logical; var item=new ToolStripMenuItem(logical+" pixels");
                item.Click+=delegate {
                    CancelInteraction(); sizeFactor=chosen/192.0;
                    foreach(Display display in allDisplays)display.SizeFactor=sizeFactor;
                    motion.CancelMotion();ClearFrames();cacheScale=-1;DrawPet();
                }; sizes.DropDownItems.Add(item);
            }
            var visibility=new ToolStripMenuItem("Hide / show Qa3moz");
            visibility.Click+=delegate {
                hidden=!hidden;
                CancelInteraction();
                habits.DeferMessages();lastScreenPoll=-10;
                if(hidden) Hide(); else if(!locked && !suspended && !contextHidden && !options.Quiet) { Show(); Native.EnsureOwnTopmost(Handle); DrawPet(); }
            };
            var info=new ToolStripMenuItem("Controls and privacy");
            info.Click+=delegate { MessageBox.Show(
                "Ctrl+Alt+Shift+P: pause/resume\nCtrl+Alt+Shift+Q: immediate exit\nCtrl+Alt+Shift+F: cursor approach on/off\n\n"+
                "Grab the body to drag; stroke the head gently. Right-click the pet or tray for controls.\n"+
                "Transparent pixels pass clicks through. Capture is limited to an active user drag.\n"+
                "No screen capture, input logging or networking. Login startup is optional.\n"+
                "Fullscreen avoidance reads only foreground window bounds; no titles or content.\n"+
                (pixel.Ready?"Pixel sleeping/held poses loaded.\n":"Original artwork retained; dedicated sleeping/held pixel art pending.\n")+
                "The pet hides on lock/sleep. It cannot appear over the Windows lock screen.",
                "Pawquilt",MessageBoxButtons.OK,MessageBoxIcon.Information); };
            var quit=new ToolStripMenuItem("Exit Pawquilt");
            quit.Click+=delegate { ExitNow(); };
            startupItem.Click+=delegate {
                try{StartupLink.Set(!StartupLink.Enabled());UpdateControls();}
                catch(Exception error){MessageBox.Show(error.Message,"Pawquilt startup",MessageBoxButtons.OK,MessageBoxIcon.Information);}
            };
            var little=new ToolStripMenuItem("Little habits");
            AddOption(little,"Varied playful behavior",()=>options.Variation,value=>{options.Variation=value;motion.Variation=value;});
            AddOption(little,"Occasional affection bubble",()=>options.Affection,value=>options.Affection=value);
            AddOption(little,"Gentle break reminder",()=>options.Breaks,value=>options.Breaks=value);
            AddOption(little,"Avoid fullscreen apps",()=>options.AvoidFullscreen,value=>options.AvoidFullscreen=value);
            quietItem.CheckOnClick=true;quietItem.Click+=delegate{options.Quiet=quietItem.Checked;SaveOptions();CancelInteraction();habits.DeferMessages();lastScreenPoll=-10;if(options.Quiet){contextHidden=true;Hide();}};
            menu.Items.AddRange(new ToolStripItem[]{new ToolStripLabel("PAWQUILT  -  your desk friend"),pauseItem,followItem,new ToolStripSeparator(),sizes,restNow,wakeNow,sleep,little,new ToolStripSeparator(),reset,visibility,quietItem,startupItem,new ToolStripSeparator(),info,quit});
            menu.Opening+=delegate { CancelInteraction(); UpdateControls(); };
            tray.Icon=SystemIcons.Application; tray.Text="Pawquilt: paused"; tray.ContextMenuStrip=menu;
            tray.DoubleClick+=delegate { TogglePause(); };
        }
        void AddOption(ToolStripMenuItem parent,string title,Func<bool> read,Action<bool> write) {
            var item=new ToolStripMenuItem(title);item.CheckOnClick=true;item.Checked=read();
            parent.DropDownOpening+=delegate{item.Checked=read();};
            item.Click+=delegate{write(item.Checked);SaveOptions();speech.Dismiss();lastScreenPoll=-10;};parent.DropDownItems.Add(item);
        }
        void SaveOptions(){try{options.Save(optionsPath);}catch(Exception error){Diagnostics.Write("BEHAVIOR SETTINGS SAVE ERROR "+error.Message);}}
        void Loaded(object sender,EventArgs e) {
            try {
                Diagnostics.Write("LOAD "+Native.OwnWindow(Handle)+" paused="+motion.Paused+" follow="+motion.Follow);
                // Fail closed: never start an overlay without both stop controls.
                Native.Require(Native.RegisterHotKey(Handle,1,Mods,PauseKey),"Pause shortcut unavailable");
                Native.Require(Native.RegisterHotKey(Handle,2,Mods,ExitKey),"Emergency exit shortcut unavailable");
                Native.Require(Native.RegisterHotKey(Handle,3,Mods,FollowKey),"Follow shortcut unavailable");
                Native.Require(Native.WTSRegisterSessionNotification(Handle,0),"Session-lock notification unavailable");
                memoryDc=Native.CreateCompatibleDC(IntPtr.Zero);
                Native.Require(memoryDc!=IntPtr.Zero,"Cannot create sprite drawing context");
                tray.Visible=true; ready=true; clock.Start();PollScreens(0);Native.EnsureOwnTopmost(Handle); DrawPet(); timer.Start();
                Diagnostics.Write("READY tray="+tray.Visible+" pixel="+pixel.Ready+" hotkeys=P,Q,F registered; silhouette interaction enabled "+Native.OwnWindow(Handle));
            } catch(Exception error) {
                Diagnostics.Write("LOAD ERROR "+error.ToString());
                MessageBox.Show("Qa3moz did not start. "+error.Message,"Pawquilt",MessageBoxButtons.OK,MessageBoxIcon.Error);
                ExitNow();
            }
        }
        void UpdateControls(bool readStartup=true) {
            pauseItem.Text=motion.Paused?"Start roaming":"Pause roaming";
            followItem.Checked=motion.Follow;
            followItem.Text="Occasional cursor approach";
            if(readStartup)try{startupItem.Checked=StartupLink.Enabled();}catch{startupItem.Checked=false;}
            quietItem.Checked=options.Quiet;
            tray.Text="Pawquilt: "+(options.Quiet?"quiet":contextHidden?"waiting for a free display":motion.Paused?"paused":interaction.State==Reaction.Sleep?"resting":"roaming");
        }
        internal void SaveMenuPreview(string path) {
            Size preferred=menu.GetPreferredSize(Size.Empty);menu.Size=preferred;menu.CreateControl();
            using(var preview=new Bitmap(preferred.Width,preferred.Height)) {
                menu.DrawToBitmap(preview,new Rectangle(Point.Empty,preferred));preview.Save(path,ImageFormat.Png);
            }
        }
        void TogglePause() {
            if(!ready) return;
            motion.Paused=!motion.Paused;
            CancelInteraction();habits.DeferMessages();lastScreenPoll=-10;UpdateControls(); DrawPet();
        }
        void Tick(object sender,EventArgs e) {
            if(!ready || exiting || locked || suspended) return;
            double now=clock.Elapsed.TotalSeconds;
            double dt=Math.Min(.05,Math.Max(0,now-previous)); previous=now;
            if(hidden || menu.Visible || options.Quiet){speech.Dismiss();return;}
            try {
                if(now-lastScreenPoll>=1 && !interaction.Armed && !interaction.Dragging)PollScreens(now);
                if(contextHidden)return;
                if(motion.Paused && !interaction.IsBusy){speech.Dismiss();return;}
                speech.Tick(now,motion.Current,motion.Position);
                bool wasBusy=interaction.IsBusy;
                if(interaction.Dragging) {
                    if(!Capture){CancelInteraction();return;}
                    Vec? cursor=Native.Cursor(); if(cursor.HasValue)interaction.DragTo(motion,cursor.Value);
                }
                if(now-lastBehaviorPoll>=1) {
                    double interval=lastBehaviorPoll<0?1:now-lastBehaviorPoll;lastBehaviorPoll=now;
                    cachedIdle=interaction.ManualRest?0:Native.IdleSeconds();
                    bool sleeping=interaction.State>=Reaction.Yawn && interaction.State<=Reaction.Sleep;
                    habits.Tick(interval,cachedIdle,sleeping,motion.Paused || screens.AnyFullscreen);
                    motion.Energy=habits.Energy;motion.Warmth=habits.Warmth;motion.CalmContext=screens.AnyFullscreen;
                }
                double idle=cachedIdle;
                interaction.Tick(dt,idle,motion.Paused?Double.MaxValue:motion.SleepAfter);
                if(wasBusy!=interaction.IsBusy)motion.CancelMotion();
                if(!interaction.IsBusy && !speech.Visible) {
                    Mood before=motion.State;motion.Tick(dt,0,motion.Follow && !screens.AnyFullscreen?Native.Cursor():null);
                    if((before==Mood.Jump || before==Mood.Transfer) && motion.State==Mood.Idle)interaction.Land();
                }
                bool safe=!motion.Paused && !interaction.IsBusy && motion.State==Mood.Idle && !screens.AnyFullscreen && !speech.Visible;
                LittleMessage message=habits.Message(cachedIdle,safe);
                if(message!=LittleMessage.None){motion.CancelMotion();if(message==LittleMessage.Affection)interaction.RequestPetting();speech.ShowMessage(message,motion.Current,motion.Position,now);}
                else if(habits.Friendly(safe)){motion.CancelMotion();interaction.Friendly(animationRandom.NextDouble()<.30);}
                DrawPet();
            } catch(Exception error) {
                Diagnostics.Write("RENDER ERROR "+error.ToString());
                motion.Paused=true; timer.Stop(); Hide();
                ExitNow();
            }
        }
        void PollScreens(double now) {
            lastScreenPoll=now;var snapshots=new List<ForegroundSnapshot>();
            if(options.AvoidFullscreen){foreach(var prior in screens.KnownWindows)snapshots.Add(Native.RecheckKnownForeground(prior));snapshots.Add(Native.ForegroundBounds());}
            screens.Update(allDisplays,snapshots,options.AvoidFullscreen);
            if(screens.AnyFullscreen)habits.DeferMessages();
            List<Display> allowed=screens.Allowed(allDisplays);
            if(options.Quiet || allowed.Count==0 || motion.Paused && screens.IsBlocked(motion.Current.Id)) {
                if(!contextHidden){CancelInteraction();habits.DeferMessages();Diagnostics.Write("CONTEXT HIDE quiet="+options.Quiet+" allowedDisplays="+allowed.Count+" paused="+motion.Paused);}contextHidden=true;speech.Dismiss();Hide();UpdateControls(false);return;
            }
            bool changed=allowed.Count!=motion.Displays.Count;
            if(!changed)for(int n=0;n<allowed.Count;n++)if(allowed[n].Id!=motion.Displays[n].Id){changed=true;break;}
            if(changed) {
                string old=motion.Current.Id;Vec position=motion.Position;bool relocation=allowed.FindIndex(d=>d.Id==old)<0;
                CancelInteraction();motion.Reset(allowed);
                if(relocation) {
                    int nearest=0;double best=Double.MaxValue;
                    for(int n=0;n<allowed.Count;n++){double distance=(allowed[n].Center-position).Length;if(distance<best){best=distance;nearest=n;}}
                    Display destination=allowed[nearest];motion.Reposition(nearest,destination.Center-new Vec(destination.PetWidth/2,destination.PetHeight/2));
                }
                ClearFrames();cacheScale=-1;
            }
            bool restore=contextHidden;contextHidden=false;
            if(restore && !hidden && !locked && !suspended){Show();Native.EnsureOwnTopmost(Handle);DrawPet();Diagnostics.Write("CONTEXT RESTORE "+Native.OwnWindow(Handle));}
            UpdateControls(false);
        }
        void ClearFrames() {
            lastImage=IntPtr.Zero;lastAlpha=-1;
            hitFrame=null; foreach(IntPtr image in frames.Values)Native.DeleteObject(image);frames.Clear();
            foreach(Bitmap mask in masks.Values)mask.Dispose();masks.Clear();
        }
        IntPtr Sprite(int row,int col,string state,int frame,bool mirror,double scale) {
            if(Math.Abs(cacheScale-scale)>.0001) { ClearFrames(); cacheScale=scale; }
            string key=state==null?row+":"+col:state+":"+frame+":"+mirror; IntPtr bitmap;
            if(frames.TryGetValue(key,out bitmap)){hitFrame=masks[key];return bitmap;}
            if(frames.Count>=24)ClearFrames(); // Bounded GDI + hit-mask cache, including high DPI.
            int width=(int)motion.Current.PetWidth,height=(int)motion.Current.PetHeight;
            var image=SpriteArt.Render(state==null?atlas:pixel.Strip(state),
                state==null?new Rectangle(col*192,row*208,192,208):new Rectangle(frame*128,0,128,128),width,height,state!=null,mirror);
            try {
                bitmap=image.GetHbitmap(Color.FromArgb(0));
                if(firstDraw) {
                    int opaque=0,maxAlpha=0;
                    for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
                        int a=image.GetPixel(x,y).A; if(a>0) opaque++; maxAlpha=Math.Max(maxAlpha,a);
                    }
                    Diagnostics.Write("SPRITE row="+row+" col="+col+" pixelState="+(state??"original")+" frame="+frame+" size="+width+"x"+height+" alphaPixels="+opaque+" maxAlpha="+maxAlpha);
                    image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostic-frame.png"),ImageFormat.Png);
                }
                Native.Require(bitmap!=IntPtr.Zero,"Cannot allocate sprite");frames.Add(key,bitmap);masks.Add(key,image);hitFrame=image;return bitmap;
            }catch{image.Dispose();throw;}
        }
        void DrawPet() {
            if(!ready || exiting || hidden || locked || suspended || contextHidden || options.Quiet) return;
            int row=0,col=0;
            double alpha=motion.Alpha;
            if(!motion.Paused) {
                if(motion.State==Mood.Walk) row=motion.FacesRight?1:2;
                else if(motion.State==Mood.Jump || motion.State==Mood.Transfer) row=4;
                if(motion.State==Mood.Rest) { col=0; alpha*=.95+.05*Math.Cos(motion.StateTime*1.8); }
                else {
                    col=AtlasLayout.Frame(row,motion.StateTime);
                    if(!pixel.Ready && motion.State==Mood.Idle && motion.Follow && !screens.AnyFullscreen) {
                        Vec? cursor=Native.Cursor();
                        if(cursor.HasValue) {
                            Vec center=motion.Position+new Vec(motion.Current.PetWidth/2,motion.Current.PetHeight/2);
                            double distance=(cursor.Value-center).Length;
                            if(distance>100*motion.Current.Scale && distance<600*motion.Current.Scale) {
                                int look=motion.LookIndex(cursor.Value); row=9+look/8; col=look%8;
                            }
                        }
                    }
                }
            }
            string state=null;int frame=0;bool mirror=false;
            if(pixel.Ready) {
                double time=motion.Paused?0:motion.StateTime;bool loop=true;state="idle";
                if(motion.State==Mood.Walk){state=pixel.HasMoves && motion.Running?"light_run":"walk_right";mirror=!motion.FacesRight;}
                else if(motion.State==Mood.Jump || motion.State==Mood.Transfer){state="jump";loop=false;time=motion.StateTime*pixel.Duration("jump")/(motion.State==Mood.Jump?.84:1.0);}
                if(interaction.State!=Reaction.None) {
                    time=interaction.Time;loop=false;
                    switch(interaction.State) {
                        case Reaction.Held:state="held_surprise";loop=true;break;
                        case Reaction.Pet:state="petting_happy";break;
                        case Reaction.Landing:state="land";break;
                        case Reaction.Yawn:state="yawn";break;
                        case Reaction.Stretch:state=pixel.HasMoves?"sleepy_stretch":"stretch";break;
                        case Reaction.Curl:state="curl_sleep";break;
                        case Reaction.Sleep:state="sleep_loop";loop=true;break;
                        case Reaction.Wake:state="wake";break;
                        // These strips already contain their jump offsets. The own window stays anchored.
                        case Reaction.HappyJump:state="happy_jump";break;
                        case Reaction.Somersault:state="somersault";break;
                        case Reaction.Scratch:state="ear_scratch";break;
                        case Reaction.Request:state="request_petting";break;
                    }
                }
                frame=pixel.Frame(state,time,loop);
            } else {
                // Existing art is an interim response; it does not depict the requested new poses.
                if(interaction.State==Reaction.Pet){row=0;col=1;}
                if(interaction.State==Reaction.Held){row=6;col=AtlasLayout.Frame(6,interaction.Time);}
                if(interaction.State==Reaction.Landing){row=4;col=4;}
                if(interaction.State>=Reaction.Yawn){row=0;col=0;}
            }
            IntPtr image=Sprite(row,col,state,frame,mirror,motion.Current.PetScale);
            int drawX=(int)Math.Round(motion.Position.X),drawY=(int)Math.Round(motion.Position.Y);
            int opacity=(int)Math.Round(255*Math.Max(0,Math.Min(1,alpha)));
            if(!firstDraw && lastImage==image && lastX==drawX && lastY==drawY && lastAlpha==opacity)return;
            IntPtr previousObject=Native.SelectObject(memoryDc,image);
            try {
                var point=new Native.POINT((int)Math.Round(motion.Position.X),(int)Math.Round(motion.Position.Y));
                var origin=new Native.POINT(0,0);
                var size=new Native.SIZE((int)motion.Current.PetWidth,(int)motion.Current.PetHeight);
                var blend=new Native.BLEND { Op=0,Flags=0,Alpha=(byte)Math.Round(255*Math.Max(0,Math.Min(1,alpha))),Format=1 };
                Native.Require(Native.UpdateLayeredWindow(Handle,IntPtr.Zero,ref point,ref size,memoryDc,ref origin,0,ref blend,2),"Cannot render pet");
                lastImage=image;lastX=drawX;lastY=drawY;lastAlpha=opacity;
                if(firstDraw) { Diagnostics.Write("FIRST DRAW alpha="+blend.Alpha+" "+Native.OwnWindow(Handle)); firstDraw=false; }
            } finally { Native.SelectObject(memoryDc,previousObject); }
        }
        void OnUi(Action action) {
            if(exiting || IsDisposed || !IsHandleCreated) return;
            if(InvokeRequired) { try { BeginInvoke(action); } catch(InvalidOperationException) { } }
            else action();
        }
        void DisplayChanged(object sender,EventArgs e) {
            OnUi(delegate { try { CancelInteraction();allDisplays=Native.Displays();foreach(Display d in allDisplays){d.SizeFactor=sizeFactor;d.Square=pixel.Ready;}motion.Reset(allDisplays); ClearFrames(); cacheScale=-1;PollScreens(clock.Elapsed.TotalSeconds);DrawPet(); } catch { ExitNow(); } });
        }
        void PowerChanged(object sender,PowerModeChangedEventArgs e) {
            OnUi(delegate {
                if(e.Mode==PowerModes.Suspend) { CancelInteraction(); suspended=true; timer.Stop(); Hide(); }
                if(e.Mode==PowerModes.Resume) {
                    suspended=false; previous=clock.Elapsed.TotalSeconds; motion.CancelMotion();
                    lastScreenPoll=-10;if(!locked && !hidden && !contextHidden && !options.Quiet) { Show(); Native.EnsureOwnTopmost(Handle); DrawPet(); }
                    if(!locked) timer.Start();
                }
            });
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x21) { m.Result=new IntPtr(3); return; } // MA_NOACTIVATE.
            if(m.Msg==0x84) {
                Vec screen=MessagePoint(m.LParam);
                m.Result=new IntPtr(Interaction.Opaque(hitFrame,(int)(screen.X-motion.Position.X),(int)(screen.Y-motion.Position.Y))?1:-1);return;
            }
            if(ready && (m.Msg==0x200 || m.Msg==0x201 || m.Msg==0x202 || m.Msg==0x205)) {
                Native.TrackOwnLeave(Handle);
                Vec local=MessagePoint(m.LParam),screen=motion.Position+local;
                double nx=local.X/motion.Current.PetWidth,ny=local.Y/motion.Current.PetHeight;
                if(m.Msg==0x201){speech.Dismiss();motion.CancelMotion();interaction.Down(screen,nx,ny);}
                else if(m.Msg==0x200) {
                    bool held=interaction.Dragging;
                    Reaction before=interaction.State;double oldTime=interaction.Time;
                    if(interaction.Move(screen,nx,ny,motion.Current.Scale,clock.Elapsed.TotalSeconds)) {
                        if(!held){Capture=true;if(!Capture){CancelInteraction();return;}}
                        interaction.DragTo(motion,screen);
                    } else if(interaction.IsBusy)motion.CancelMotion();
                    if(interaction.State==Reaction.Pet && (before!=Reaction.Pet || interaction.Time<oldTime)){habits.Pet();speech.Dismiss();}
                } else if(m.Msg==0x202){interaction.Up(motion);ReleaseDragCapture();}
                else {CancelInteraction();menu.Show(this,new Point((int)local.X,(int)local.Y));}
                DrawPet();m.Result=IntPtr.Zero;return;
            }
            if(ready && m.Msg==0x2A3 && !interaction.Dragging && interaction.Armed){CancelInteraction();DrawPet();} // Pointer left before grab threshold.
            if(ready && m.Msg==0x215 && !releasingCapture && interaction.Dragging){CancelInteraction();DrawPet();} // Capture lost.
            if(ready && (m.Msg==0x1F || m.Msg==0x1C && m.WParam==IntPtr.Zero)){CancelInteraction();DrawPet();} // Cancel mode / application deactivation.
            if(m.Msg==0x0312) {
                if(m.WParam.ToInt32()==1) TogglePause();
                if(m.WParam.ToInt32()==2) ExitNow();
                if(m.WParam.ToInt32()==3) { motion.Follow=!motion.Follow; UpdateControls(); }
                return;
            }
            if(m.Msg==0x02B1) { // WM_WTSSESSION_CHANGE, current session only.
                int reason=m.WParam.ToInt32();
                if(reason==7 || reason==2 || reason==3 || reason==4 || reason==6) { CancelInteraction(); locked=true; timer.Stop(); Hide(); }
                if(reason==8) { // Unlock, not merely wake or reconnect.
                    locked=false; previous=clock.Elapsed.TotalSeconds; motion.CancelMotion();
                    lastScreenPoll=-10;if(!suspended && !hidden && !contextHidden && !options.Quiet) { Show(); Native.EnsureOwnTopmost(Handle); DrawPet(); }
                    if(!suspended)timer.Start();
                }
            }
            if(m.Msg==0x02E0) { // PMv2 physical sizing is owned by our sprite/monitor model.
                m.Result=IntPtr.Zero; return;
            }
            base.WndProc(ref m);
        }
        static Vec MessagePoint(IntPtr value) { long bits=value.ToInt64();return new Vec((short)(bits&65535),(short)((bits>>16)&65535)); }
        void ReleaseDragCapture(){releasingCapture=true;try{if(Capture)Capture=false;}finally{releasingCapture=false;}}
        void CancelInteraction(){interaction.Cancel(motion,true);ReleaseDragCapture();speech.Dismiss();}
        void ExitNow() {
            if(exiting) return; exiting=true; CancelInteraction();timer.Stop(); Hide(); Cleanup(); Close();
        }
        void Cleanup() {
            if(cleaned) return; cleaned=true;
            SystemEvents.DisplaySettingsChanged-=DisplayChanged;
            SystemEvents.PowerModeChanged-=PowerChanged;
            if(IsHandleCreated) {
                for(int id=1;id<=3;id++) Native.UnregisterHotKey(Handle,id);
                Native.WTSUnRegisterSessionNotification(Handle);
            }
            tray.Visible=false; tray.Dispose(); menu.Dispose(); timer.Dispose();
            ClearFrames(); if(memoryDc!=IntPtr.Zero) { Native.DeleteDC(memoryDc); memoryDc=IntPtr.Zero; }
            atlas.Dispose();pixel.Dispose();speech.Dispose();
        }
        protected override void Dispose(bool disposing) { if(disposing) Cleanup(); base.Dispose(disposing); }
    }
}
