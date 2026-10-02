using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Qa3moz {
    internal static class SelfTests {
        static int passed;
        static void Assert(bool condition,string name) { if(!condition) throw new Exception(name); passed++; }
        static List<Display> TwoDisplays() {
            return new List<Display>{new Display("left",-1920,0,0,1040,1),new Display("right",0,-320,2560,1120,1.5)};
        }
        public static int Run() {
            try {
                passed=0;
                var model=new Motion(TwoDisplays(),27);
                int seen=0,jumps=0,transfers=0;
                for(int i=0;i<90000;i++) {
                    model.Tick(.033,0,null);
                    Assert(model.FitsWorkAreas,"Every visible frame fits monitor work areas or their contiguous union");
                    Assert(model.Alpha>=0 && model.Alpha<=1,"Alpha bounds");
                    Assert(!Double.IsNaN(model.Position.X+model.Position.Y),"Finite position");
                    seen|=1<<model.Monitor;
                    if(model.State==Mood.Jump) jumps++;
                    if(model.State==Mood.Transfer) transfers++;
                }
                Assert(seen==3,"Both monitors visited"); Assert(jumps>0,"Variable jumps"); Assert(transfers>0,"Cross-display transfers");
                model.Paused=true; Vec frozen=model.Position; double time=model.StateTime;
                for(int i=0;i<300;i++) model.Tick(.033,999,new Vec(0,0));
                Assert(model.Position.X==frozen.X && model.Position.Y==frozen.Y && model.StateTime==time,"Pause freezes position and state");
                model.Paused=false; model.Tick(.033,181,null);
                Assert(model.State==Mood.Rest,"Idle time enters rest");
                Vec resting=model.Position;
                for(int i=0;i<100;i++) model.Tick(.033,400,null);
                Assert(model.Position.X==resting.X && model.Position.Y==resting.Y,"Rest stays still");
                model.Tick(.033,0,null); Assert(model.State==Mood.Idle,"Activity wakes gently");
                model.Reposition(1,new Vec(2500,1000)); Assert(model.Current.ContainsPet(model.Position),"Reposition bounded");
                model.Reset(new List<Display>{new Display("small",-400,-300,-80,-20,2.5)});
                for(int i=0;i<30000;i++) {
                    model.Tick(.5,0,null); Assert(model.Current.ContainsPet(model.Position),"Small negative-coordinate display and long stalls");
                }
                model.Follow=true;
                for(int i=0;i<6000;i++) {
                    model.Tick(.033,0,new Vec(-100,-40)); Assert(model.Current.ContainsPet(model.Position),"Cursor approach remains bounded");
                }
                model.Reposition(0,new Vec(-400,-300));
                Vec center=model.Position+new Vec(model.Current.PetWidth/2,model.Current.PetHeight/2);
                Assert(model.LookIndex(center+new Vec(0,-100))==0,"Look up");
                Assert(model.LookIndex(center+new Vec(100,0))==4,"Look right");
                Assert(model.LookIndex(center+new Vec(0,100))==8,"Look down");
                Assert(model.LookIndex(center+new Vec(-100,0))==12,"Look left");
                var separated=new Motion(new List<Display>{new Display("a",-1400,-800,-400,0,1),new Display("b",400,700,1800,1700,2)},7);
                int both=0;
                for(int i=0;i<30000;i++) {
                    separated.Tick(.033,0,null); Assert(separated.Current.ContainsPet(separated.Position),"Gapped monitor hop stays inside work areas");
                    both|=1<<separated.Monitor;
                }
                Assert(both==3,"Gapped displays visited");
                int count=0; foreach(int frames in AtlasLayout.Counts) count+=frames;
                Assert(count==73,"Original 73-frame contract");
                for(int row=0;row<9;row++) for(int frame=0;frame<1000;frame++) {
                    int result=AtlasLayout.Frame(row,frame*.07);
                    Assert(result>=0 && result<AtlasLayout.Counts[row],"Playback never enters an unused cell");
                }
                Assert(AtlasLayout.Frame(4,0)==0 && AtlasLayout.Frame(4,.15)==1,"Jump frame timing");
                TestInteractions();
                TestPortableTransfers();
                passed+=BehaviorTests.Run();
                Console.WriteLine("PASS: "+passed+" assertions; movement, mixed DPI/work areas, pause, alpha/head hit tests, drag threshold/cancel/release, petting rate, sleep/wake, pixel renderer and art validation.");
                return 0;
            } catch(Exception error) { Console.Error.WriteLine("FAIL: "+error.Message); return 1; }
        }
        static void Advance(Interaction i,double seconds,double idle) {
            for(int n=0;n<(int)Math.Ceiling(seconds/.025);n++)i.Tick(.025,idle,180);
        }
        static void TestPortableTransfers() {
            var adjacent=new Motion(TwoDisplays(),19);int continuousFrames=0;bool disconnected=false;
            for(int n=0;n<20000;n++) {
                bool wasContinuous=adjacent.IsContinuousTransfer;
                Vec before=adjacent.Position+new Vec(adjacent.Current.PetWidth/2,adjacent.Current.PetHeight/2);
                adjacent.Tick(.025,0,null);
                Assert(adjacent.FitsWorkAreas,"Continuous jumps remain in adjoining work-area union");
                if(wasContinuous && adjacent.IsContinuousTransfer) {
                    Vec after=adjacent.Position+new Vec(adjacent.Current.PetWidth/2,adjacent.Current.PetHeight/2);
                    Assert((after-before).Length<25,"Adjacent crossing center has no teleport, including DPI handoff");
                    Assert(adjacent.Alpha==1,"Adjacent monitor jump remains fully visible");continuousFrames++;
                }
                if(!disconnected && adjacent.IsContinuousTransfer && !adjacent.Current.ContainsPet(adjacent.Position)) {
                    adjacent.Reset(new System.Collections.Generic.List<Display>{new Display("replacement",-800,-200,900,900,1.25)});
                    Assert(!adjacent.IsContinuousTransfer && adjacent.Current.ContainsPet(adjacent.Position),"Disconnect mid-flight cancels/clamps to newly enumerated monitor");disconnected=true;
                }
            }
            Assert(continuousFrames>0 && disconnected,"Continuous crossing and disconnect scenario exercised");
            foreach(var displays in new[]{
                new System.Collections.Generic.List<Display>{new Display("single",-3000,-1200,300,1000,2)},
                new System.Collections.Generic.List<Display>{new Display("upper",-1600,-1080,0,0,1),new Display("lower",-1600,0,0,1440,1.5)},
                new System.Collections.Generic.List<Display>{new Display("a",0,0,1600,1000,1),new Display("b",1600,0,3520,1080,1.25),new Display("c",3520,-200,5440,1000,1.5)}
            }) {
                var model=new Motion(displays,77);int visited=0;
                for(int n=0;n<25000;n++){model.Tick(.033,0,null);Assert(model.FitsWorkAreas,"Single/vertical/three-monitor layouts remain bounded");visited|=1<<model.Monitor;}
                Assert(visited==(1<<displays.Count)-1,"Automatically discovered monitor count visited");
            }
            var dragModel=new Motion(TwoDisplays(),4);var grab=new Interaction();
            grab.Down(new Vec(0,0),.5,.75);grab.Move(new Vec(30,0),.5,.75,1,0);grab.Cancel(dragModel,true);
            dragModel.Reset(new System.Collections.Generic.List<Display>{new Display("remaining",-1920,0,0,1040,1)});
            Assert(!grab.Dragging && dragModel.Current.ContainsPet(dragModel.Position),"Display-change handler cancellation/re-enumeration model during drag");
        }
        static void TestInteractions() {
            var m=new Motion(TwoDisplays(),11);m.Paused=true;var i=new Interaction();
            Assert(Interaction.Head(.53,.30),"Head center interactive");
            Assert(!Interaction.Head(.1,.95) && !Interaction.Head(1.1,.30),"Body and outside excluded from head");
            i.Down(new Vec(-500,500),.5,.75);
            Assert(!i.Move(new Vec(-495,500),.5,.75,1,0),"Tiny body movement does not drag");
            Assert(i.Move(new Vec(-491,500),.5,.75,1,.1),"Body threshold starts drag");
            Assert(i.Dragging && i.State==Reaction.Held,"Held response while dragging");
            i.DragTo(m,new Vec(1200,500));
            Assert(m.Monitor==1 && m.Current.ContainsPet(m.Position),"Drag crosses display and DPI safely");
            i.DragTo(m,new Vec(50000,-50000));
            Assert(m.Current.ContainsPet(m.Position),"Out-of-workarea release clamps");
            i.Up(m);Assert(!i.Armed && !i.Dragging && i.State==Reaction.Landing,"Release clears grab and starts landing");
            Advance(i,.4,0);Assert(i.State==Reaction.None,"Landing is bounded");
            i.Down(new Vec(0,0),.5,.75);i.Move(new Vec(20,0),.5,.75,1,0);
            i.Cancel(m);Assert(!i.IsBusy && !i.Dragging && !i.Armed,"Capture loss/cancel clears gesture");
            i.Down(new Vec(0,0),.53,.30);
            Assert(!i.Move(new Vec(12,0),.6,.30,1,2),"Head stroke is distinct from drag");
            i.Move(new Vec(24,0),.6,.30,1,2.1);
            Assert(i.State==Reaction.Pet && !i.Dragging,"Head stroke happy reaction");
            i.Up(m);Advance(i,.2,0);double elapsed=i.Time;
            i.Move(new Vec(30,0),.6,.30,1,2.2);i.Move(new Vec(42,0),.6,.30,1,2.3);
            Assert(i.Time==elapsed,"Pet response cannot reset too often");
            i.Cancel(m);i.Down(new Vec(0,0),.53,.30);
            Assert(i.Move(new Vec(20,50),.53,.8,1,4),"Head grab moved toward body becomes drag");
            i.Cancel(m);
            i=new Interaction(); // New automatic-idle scenario, independent of the prior direct-interaction cooldown.
            i.Tick(.025,181,180);Assert(i.State==Reaction.Yawn,"Idle starts yawn");
            Advance(i,1.1,181);Assert(i.State==Reaction.Stretch,"Yawn then stretch");
            Advance(i,1.0,181);Assert(i.State==Reaction.Curl,"Stretch then curl");
            Advance(i,1.1,181);Assert(i.State==Reaction.Sleep,"Curl then true sleep state");
            i.Tick(.025,0,180);Assert(i.State==Reaction.Wake,"Aggregate input wakes");
            Advance(i,1,0);Assert(i.State==Reaction.None,"Wake completes");
            i.RestNow();Advance(i,3.3,1);Assert(i.State==Reaction.Sleep,"Menu rest reaches sleep");
            for(int n=0;n<400;n++)i.Tick(.025,n%3==0?0:.1,180);
            Assert(i.State==Reaction.Sleep && i.ManualRest,"Manual menu rest survives unrelated continuous global input");
            i.Cancel(m,true);Assert(i.State==Reaction.Sleep && i.ManualRest,"Menu/capture/session cancellation preserves manual rest latch");
            i.Tick(.025,0,180);Assert(i.State==Reaction.Sleep,"Unlock/menu mouse activity cannot wake manual sleep");
            i.WakeNow();Assert(i.State==Reaction.Wake && !i.ManualRest,"Explicit Wake clears manual rest");
            Advance(i,1,999);Assert(i.State==Reaction.None && i.AutoSleepCooldown>0,"Wake cooldown prevents immediate re-sleep on stale idle value");
            i.RestNow();Advance(i,3.3,0);i.Down(new Vec(0,0),.5,.75);
            Assert(!i.ManualRest && i.Armed,"Direct body grab wakes manual rest");i.Cancel(m);
            i.RestNow();Advance(i,3.3,0);i.Move(new Vec(0,0),.53,.30,1,100);i.Move(new Vec(15,0),.53,.30,1,101);
            Assert(!i.ManualRest && i.State==Reaction.Pet,"Direct head petting wakes manual sleep pleasantly");
            i.Cancel(m);Assert(!i.ManualRest,"Cancel clears rest");
            foreach(int size in new[]{128,192,256}) {
                foreach(Display display in m.Displays){display.SizeFactor=size/192.0;display.Square=true;}
                m.CancelMotion();Assert(m.Current.ContainsPet(m.Position),"Each menu size remains bounded");
                Assert(m.Current.PetWidth==m.Current.PetHeight,"Pixel size uses square canvas with stable normalized head region");
            }
            using(var source=new Bitmap(2,2)) {
                source.SetPixel(0,0,Color.Red);source.SetPixel(1,0,Color.Blue);source.SetPixel(0,1,Color.FromArgb(1,255,0,0));
                using(var render=SpriteArt.Render(source,new Rectangle(0,0,2,2),8,10,true,false)) {
                    Assert(!Interaction.Opaque(render,0,0),"Letterbox/transparent area passes clicks");
                    Assert(Interaction.Opaque(render,1,3),"Visible silhouette receives clicks");
                    Assert(!Interaction.Opaque(render,-1,0) && !Interaction.Opaque(render,99,99),"Out-of-bounds hit passes through");
                    Assert(render.GetPixel(1,3).ToArgb()==Color.Red.ToArgb(),"Nearest-neighbor preserves palette");
                    Assert(render.GetPixel(1,8).A==0,"Low-alpha fringe becomes a real crossprocess hole");
                }
                using(var mirrored=SpriteArt.Render(source,new Rectangle(0,0,2,2),8,8,true,true))
                    Assert(mirrored.GetPixel(1,1).ToArgb()==Color.Blue.ToArgb(),"Walk mirroring preserves frame order/colors");
            }
            string dir=Path.Combine(Path.GetTempPath(),"Qa3mozArtTest-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try {
                using(var absent=new PixelArt(dir))Assert(!absent.Ready,"Missing manifest cannot activate partial art");
                var states=new Dictionary<string,object>();
                for(int n=0;n<PixelArt.Names.Length;n++) {
                    int count=PixelArt.Counts[n];int[] timing=new int[count];for(int f=0;f<count;f++)timing[f]=100*(f+1);
                    states[PixelArt.Names[n]]=new Dictionary<string,object>{{"durations_ms",timing}};
                    using(var strip=new Bitmap(count*128,128)) {
                        for(int f=0;f<count;f++)strip.SetPixel(f*128+64,122,Color.Red);
                        strip.Save(Path.Combine(dir,PixelArt.Names[n]+".png"),System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                string originalManifest=Path.Combine(dir,"original-manifest.json");
                string inputText=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"states",states}});
                File.WriteAllText(originalManifest,inputText);
                string runtimeManifest=Path.Combine(dir,"runtime-manifest.json");
                TimingAdapter.Prepare(originalManifest,runtimeManifest,"states","durations_ms");
                Assert(File.ReadAllText(originalManifest)==inputText,"Timing adaptation preserves original manifest bytes");
                bool rejected=false;
                try{TimingAdapter.Prepare(originalManifest,originalManifest,"states","durations_ms");}catch(InvalidDataException){rejected=true;}
                Assert(rejected,"Adapter rejects overwriting original");
                rejected=false;
                try{TimingAdapter.Prepare(originalManifest,runtimeManifest,"states","durations_ms");}catch(IOException){rejected=true;}
                Assert(rejected,"Adapter rejects existing output instead of overwriting");
                rejected=false;
                try{TimingAdapter.Read(new object[]{100,100.5,200,300},4,"idle");}catch(InvalidDataException){rejected=true;}
                Assert(rejected,"Fractional timings cannot be silently rounded");
                rejected=false;
                try{TimingAdapter.Read(new object[]{100,0,200,300},4,"idle");}catch(InvalidDataException){rejected=true;}
                Assert(rejected,"Zero/unbounded timings rejected");
                rejected=false;
                try{TimingAdapter.Read(new object[]{100,200},4,"idle");}catch(InvalidDataException){rejected=true;}
                Assert(rejected,"Wrong timing array length rejected");
                using(var art=new PixelArt(dir)) {
                    Assert(art.Ready,"Final variable-count manifest and complete strips validate");
                    Assert(art.Frame("idle",.15,true)==1 && art.Frame("idle",1.15,false)==3,"Manifest frame timing and once-end bounded");
                    Assert(art.Frame("idle",.099,true)==0 && art.Frame("idle",.100,true)==1,"First unequal-duration boundary");
                    Assert(art.Frame("idle",.299,true)==1 && art.Frame("idle",.300,true)==2,"Second unequal-duration boundary");
                    Assert(art.Frame("idle",.599,true)==2 && art.Frame("idle",.600,true)==3,"Third unequal-duration boundary");
                    Assert(art.Frame("idle",1,true)==0 && art.Frame("idle",1,false)==3,"Exact full-duration loop versus once");
                    Assert(art.Frame("idle",1000.15,true)==1,"Long-running animation loop remains bounded");
                    Assert(art.Frame("idle",-1,true)==0 && art.Frame("idle",Double.NaN,true)==0,"Invalid/negative times bounded");
                    Assert(art.Frame("land",999,true)==0,"Single-frame landing bounded");
                    Assert(art.Frame("jump",.6,true)==0 && art.Frame("jump",.6,false)==2,"Fractional-second loop total uses integer milliseconds");
                    Assert(Math.Abs(art.Duration("idle")-1)<.001,"Unequal manifest durations sum correctly");
                    var sequence=new Interaction();
                    sequence.YawnDuration=art.Duration("yawn");sequence.StretchDuration=art.Duration("stretch");sequence.CurlDuration=art.Duration("curl_sleep");sequence.WakeDuration=art.Duration("wake");
                    sequence.Tick(.025,181,180);Advance(sequence,.95,181);Assert(sequence.State==Reaction.Yawn,"Manifest yawn duration honored before boundary");
                    Advance(sequence,.1,181);Assert(sequence.State==Reaction.Stretch,"Manifest yawn duration advances to stretch");
                    Advance(sequence,2.1,181);Assert(sequence.State==Reaction.Sleep,"Manifest unequal timings drive full sleep sequence");
                    sequence.Tick(.025,0,180);Advance(sequence,.95,0);Assert(sequence.State==Reaction.Wake,"Manifest wake duration honored");
                    Advance(sequence,.1,0);Assert(sequence.State==Reaction.None,"Manifest wake completes");
                }
                File.Delete(Path.Combine(dir,"wake.png"));
                using(var incomplete=new PixelArt(dir))Assert(!incomplete.Ready,"One absent state rejects entire new set");
            }finally {
                // The random test-only directory is created here; delete only its known files.
                foreach(string name in PixelArt.Names){string p=Path.Combine(dir,name+".png");if(File.Exists(p))File.Delete(p);}
                File.Delete(Path.Combine(dir,"runtime-manifest.json"));File.Delete(Path.Combine(dir,"original-manifest.json"));Directory.Delete(dir);
            }
        }
        public static int CheckAsset(string path) {
            try {
                if(!File.Exists(path)) throw new FileNotFoundException("Qa3moz atlas is not imported yet",path);
                using(var image=new Bitmap(path)) {
                    if(image.Width!=AtlasLayout.Width || image.Height!=AtlasLayout.Height) throw new InvalidDataException("Wrong atlas dimensions");
                    int art=0;
                    for(int row=0;row<11;row++) for(int col=0;col<8;col++) {
                        bool found=false;
                        for(int y=row*208;y<(row+1)*208;y++) {
                            for(int x=col*192;x<(col+1)*192;x++) if(image.GetPixel(x,y).A>0) { found=true; break; }
                            if(found) break;
                        }
                        if(col<AtlasLayout.Counts[row]) { if(!found) throw new InvalidDataException("Empty used cell "+row+","+col); art++; }
                        else if(found) throw new InvalidDataException("Artwork in unused cell "+row+","+col);
                    }
                    Console.WriteLine("PASS: v2 atlas dimensions, 73 used cells, empty unused cells. Human visual inspection remains required.");
                    return 0;
                }
            } catch(Exception error) { Console.Error.WriteLine("ASSET BLOCKED: "+error.Message); return 2; }
        }
        public static int CheckPixel(string directory) {
            try {
                using(var art=new PixelArt(directory)) {
                    if(!art.Ready)throw new InvalidDataException(art.Problem);
                    int rendered=0;
                    foreach(string name in PixelArt.Names) {
                        int count=Array.IndexOf(PixelArt.Names,name);count=PixelArt.Counts[count];
                        for(int frame=0;frame<count;frame++)foreach(int size in new[]{128,192,256}) {
                            using(var image=SpriteArt.Render(art.Strip(name),new Rectangle(frame*128,0,128,128),size,size,true,false)) {
                                bool visible=false,hole=false;
                                for(int y=0;y<size;y++)for(int x=0;x<size;x++){int a=image.GetPixel(x,y).A;visible|=a>=16;hole|=a==0;}
                                if(!visible || !hole)throw new InvalidDataException("Rendered cell empty or lacks click-through: "+name+" "+frame);
                                rendered++;
                            }
                        }
                        Console.WriteLine("PASS: "+name+" cells="+count+" durationMs="+(int)Math.Round(art.Duration(name)*1000));
                    }
                    Console.WriteLine("PASS: 37 supplied cells, 111 nearest-neighbor render checks at128/192/256, alpha/click mask and authoritative timing. Human interaction QA remains required.");
                    return 0;
                }
            }catch(Exception error){Console.Error.WriteLine("PIXEL BLOCKED: "+error.Message);return 2;}
        }
    }
    static class TestProgram {
        [STAThread] public static int Main(string[] args) {
            if(args.Length>0 && args[0]=="--render-bubble") {
                using(var bitmap=SpeechBubble.Render(LittleMessage.Affection,1.25))bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"bubble-preview.png"),System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("Rendered own Arabic bubble bitmap; no overlay launched.");return 0;
            }
            if(args.Length>0 && args[0]=="--render-menu") {
                Native.DpiAware();System.Windows.Forms.Application.EnableVisualStyles();
                using(var companion=new Companion(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","spritesheet-extended.png")))
                    companion.SaveMenuPreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"menu-preview.png"));
                Console.WriteLine("Rendered own menu offscreen; no overlay launched.");return 0;
            }
            if(args.Length>0 && args[0]=="--prepare-timing") {
                if(args.Length!=5){Console.Error.WriteLine("Usage: --prepare-timing original.json runtime-manifest.json statesProperty durationProperty");return 2;}
                try{TimingAdapter.Prepare(args[1],args[2],args[3],args[4]);Console.WriteLine("PASS: timing adapter written separately; original unchanged. Inspect art before activation.");return 0;}
                catch(Exception error){Console.Error.WriteLine("TIMING BLOCKED: "+error.Message);return 2;}
            }
            if(args.Length>0 && args[0]=="--preflight") return Preflight.Run();
            if(args.Length>0 && args[0]=="--check-pixel") {
                return SelfTests.CheckPixel(args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","pixel"));
            }
            return args.Length>0 && args[0]=="--check-asset"
                ?SelfTests.CheckAsset(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","spritesheet-extended.png"))
                :SelfTests.Run();
        }
    }
}
