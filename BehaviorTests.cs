using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Qa3moz {
    internal static class BehaviorTests {
        static int count;
        static void Check(bool result,string reason){if(!result)throw new Exception(reason);count++;}
        static void Seconds(LocalHabits habits,int seconds,double idle,bool sleep,bool suppress){for(int n=0;n<seconds;n++)habits.Tick(1,idle,sleep,suppress);}
        public static int Run() {
            count=0;
            var a=new ActivityChooser(17);var b=new ActivityChooser(17);ActivityChoice previous=ActivityChoice.Linger,last=ActivityChoice.Linger;
            int[] seen=new int[4];
            for(int n=0;n<20000;n++) {
                ActivityChoice choice=a.Pick(.8,.8,3,n%3==0,false,true,true);
                Check(choice==b.Pick(.8,.8,3,n%3==0,false,true,true),"Weighted selection is deterministic with a supplied seed");
                if(choice==ActivityChoice.Jump)Check(previous!=ActivityChoice.Jump && last!=ActivityChoice.Jump,"Anti-repeat memory prevents adjacent jump choices");
                previous=last;last=choice;seen[(int)choice]++;
            }
            foreach(int frequency in seen)Check(frequency>0,"Weighted scheduler exercises every eligible behavior");
            var single=new ActivityChooser(4);var restricted=new ActivityChooser(3);
            for(int n=0;n<1000;n++) {
                Check(single.Pick(.9,.9,1,false,false,true,true)!=ActivityChoice.Transfer,"Single display cannot choose transfer");
                ActivityChoice choice=restricted.Pick(.9,.9,4,true,false,false,false);
                Check(choice!=ActivityChoice.Jump && choice!=ActivityChoice.Transfer,"Movement cooldowns exclude jumps/transfers");
            }
            int calmJumps=0,playfulJumps=0;var calm=new ActivityChooser(21);var playful=new ActivityChooser(21);
            for(int n=0;n<10000;n++) {
                if(calm.Pick(.9,.9,2,false,true,true,true)==ActivityChoice.Jump)calmJumps++;
                if(playful.Pick(.9,.9,2,false,false,true,true)==ActivityChoice.Jump)playfulJumps++;
            }
            Check(calmJumps<playfulJumps,"Calm fullscreen context lowers jump frequency");
            var flags=new BehaviorOptions();var affection=new LocalHabits(19,flags);
            Check(affection.NextAffection>=720 && affection.NextAffection<=1200,"First affection message waits12..20 active minutes");
            Seconds(affection,719,0,false,false);Check(affection.Message(0,true)==LittleMessage.None,"No early affection message");
            Seconds(affection,482,0,false,false);Check(affection.Message(0,true)==LittleMessage.Affection,"Eligible affection message appears");
            Check(affection.NextAffection-affection.Time>=1200 && affection.NextAffection-affection.Time<=1800,"Affection repeats no sooner than20..30minutes");
            Check(affection.Message(0,true)==LittleMessage.None,"Message cannot fire twice");
            double warmth=affection.Warmth;affection.Pet();Check(affection.Warmth>warmth && affection.NextAffection>=affection.Time+1200,"Petting improves warmth and defers affection requests");
            Seconds(affection,2400,0,false,false);Check(affection.Message(0,false)==LittleMessage.None,"Drag/menu/sleep/pause safety gate suppresses messages");
            affection.DeferMessages();Check(affection.Message(0,true)==LittleMessage.None,"Leaving game/quiet mode does not immediately show a bubble");
            Check(affection.NextAffection>=affection.Time+120,"Deferred context has two-minute quiet period");
            flags.Affection=false;flags.Breaks=false;Seconds(affection,10000,0,false,false);
            Check(affection.Message(0,true)==LittleMessage.None,"Message toggles disable both kinds");
            flags.Variation=false;Check(!affection.Friendly(true),"Variation toggle disables spontaneous happy reaction");
            var breaks=new LocalHabits(8,new BehaviorOptions{Affection=false});
            Seconds(breaks,2999,0,false,false);Check(breaks.Message(0,true)==LittleMessage.None,"No break reminder before50 active minutes");
            breaks.Tick(1,0,false,false);Check(breaks.Message(0,true)==LittleMessage.Break,"Break reminder at50 active minutes");
            Seconds(breaks,3000,0,false,false);Check(breaks.Message(0,true)==LittleMessage.None,"Break reminder cannot repeat within one hour");
            Seconds(breaks,601,0,false,false);Check(breaks.Message(0,true)==LittleMessage.Break,"Hourly cooldown can re-enable reminder");
            breaks.Tick(1,120,false,false);Check(breaks.ActiveWork==0,"Two-minute actual idle resets active-work accumulation");
            Seconds(breaks,1000,0,false,true);Check(breaks.ActiveWork==0,"Suppressed companion does not count input time");
            var noInput=new LocalHabits(9,new BehaviorOptions());Seconds(noInput,3000,31,false,false);
            Check(noInput.ActiveWork==0,"Idle greater than30seconds is not treated as active work");
            double oldEnergy=noInput.Energy;Seconds(noInput,600,300,true,false);Check(noInput.Energy>oldEnergy,"Sleeping restores local energy");
            Check(noInput.Energy<=1 && noInput.Warmth>=.55,"Mood values remain bounded and never become punishment state");
            var quietFlags=new BehaviorOptions{Quiet=true};var quiet=new LocalHabits(3,quietFlags);Seconds(quiet,5000,0,false,false);
            Check(quiet.Message(0,true)==LittleMessage.None,"Quiet mode suppresses messages independently of due time");
            TestScreens();TestVariedMotion();TestBubble();TestOptions();return count;
        }
        static List<Display> Displays() {
            return new List<Display>{new Display("left",-1920,0,0,1040,1){FullBottom=1080},new Display("right",0,-200,2560,1200,1.25){FullBottom=1240}};
        }
        static void TestScreens() {
            var displays=Displays();var policy=new ScreenPolicy();
            policy.Update(displays,new ForegroundSnapshot{Valid=true,Left=-1920,Top=0,Right=0,Bottom=1040},true);
            Check(!policy.AnyFullscreen,"Ordinary maximized work area does not cover full monitor including taskbar");
            policy.Update(displays,new ForegroundSnapshot{Valid=true,Left=-1920,Top=0,Right=0,Bottom=1080},true);
            Check(policy.IsBlocked("left") && !policy.IsBlocked("right") && policy.Allowed(displays).Count==1,"Fullscreen monitor alone excluded");
            policy.Update(displays,new ForegroundSnapshot{Valid=true,Own=true},true);Check(policy.IsBlocked("left"),"Own menu does not clear last fullscreen restriction");
            policy.Update(displays,new ForegroundSnapshot(),true);Check(policy.IsBlocked("left"),"Transient null foreground does not flicker restriction");
            policy.Update(displays,new ForegroundSnapshot{Valid=true,Left=-1920,Top=-200,Right=2560,Bottom=1240},true);
            Check(policy.Allowed(displays).Count==0,"Spanning fullscreen can exclude all monitors");
            var manual=new Interaction();var motion=new Motion(displays,5);motion.Paused=true;manual.RestNow();manual.Tick(.025,0,180);double time=manual.Time;
            manual.Cancel(motion,true);Check(motion.Paused && manual.ManualRest && manual.Time==time,"Pause and manual sleep survive fullscreen hide/cancellation");
            policy.Update(displays,new ForegroundSnapshot{Valid=true,Minimized=true,Left=-1920,Top=-200,Right=2560,Bottom=1240},true);
            Check(!policy.AnyFullscreen,"Minimized foreground window clears fullscreen restriction");
            policy.Update(displays,new ForegroundSnapshot{Valid=true,Left=-1928,Top=-8,Right=8,Bottom=1088},true);
            Check(policy.IsBlocked("left"),"Invisible border extension is accepted by bounds heuristic");
            policy.Update(displays,new ForegroundSnapshot{Valid=true,Left=-500,Top=200,Right=-100,Bottom=600},true);
            Check(policy.Allowed(displays).Count==2,"Normal foreground restores both monitors");
            policy.Update(displays,new ForegroundSnapshot{Valid=true,Left=-1920,Top=0,Right=0,Bottom=1080},false);
            Check(!policy.AnyFullscreen,"Fullscreen-avoidance toggle clears restrictions");
            policy.Update(displays,new ForegroundSnapshot{Valid=true,Left=-1920,Top=0,Right=0,Bottom=1080},true);
            var safe=new Motion(policy.Allowed(displays),12){Variation=true,Follow=true};
            for(int n=0;n<10000;n++){safe.Tick(.033,0,null);Check(safe.Current.Id=="right" && safe.FitsWorkAreas,"Fullscreen-filtered model never roams into excluded monitor");}
            var seen=new ScreenPolicy();
            var left=new ForegroundSnapshot{Window=1,Process=10,Valid=true,Left=-1920,Top=0,Right=0,Bottom=1080};
            var right=new ForegroundSnapshot{Window=2,Process=11,Valid=true,Left=0,Top=-200,Right=2560,Bottom=1240};
            seen.Update(displays,left,true);seen.Update(displays,new[]{left,right},true);
            Check(seen.KnownWindows.Count==2 && seen.Allowed(displays).Count==0,"Two separate observed fullscreen windows can hide all monitors");
            left.Valid=false;seen.Update(displays,new[]{left,right},true);
            Check(seen.Allowed(displays).Count==1 && seen.IsBlocked("right"),"Closed/minimized/reused known fullscreen window releases its monitor");
            for(int n=3;n<20;n++){right.Window=n;seen.Update(displays,right,true);}
            Check(seen.KnownWindows.Count<=8,"Background rechecks bounded to eight previously observed foreground windows");
        }
        static void TestVariedMotion() {
            var a=new Motion(Displays(),23){Variation=true};var b=new Motion(Displays(),23){Variation=true};int visited=0;
            for(int n=0;n<40000;n++) {
                double energy=.4+.5*Math.Sin(n*.001)*Math.Sin(n*.001);a.Energy=b.Energy=energy;a.Warmth=b.Warmth=.8;
                a.Tick(.033,0,null);b.Tick(.033,0,null);
                Check(a.FitsWorkAreas,"Context-weighted movement respects mixed-DPI work-area union");
                Check(a.State==b.State && a.Position.X==b.Position.X && a.Position.Y==b.Position.Y,"Weighted movement model remains seed deterministic");visited|=1<<a.Monitor;
            }
            Check(visited==3,"Weighted movement still visits both eligible displays");
            a.Paused=true;Vec position=a.Position;double stateTime=a.StateTime;
            for(int n=0;n<100;n++)a.Tick(.033,0,null);
            Check(a.Position.X==position.X && a.Position.Y==position.Y && a.StateTime==stateTime,"Weighted scheduler respects explicit pause");
        }
        static void TestBubble() {
            foreach(double scale in new[]{1.0,1.25,2.0})foreach(LittleMessage message in new[]{LittleMessage.Affection,LittleMessage.Break}) {
                using(var bitmap=SpeechBubble.Render(message,scale)) {
                    Check(bitmap.GetPixel(0,0).A==0,"Bubble corner is transparent");
                    Check(bitmap.GetPixel(bitmap.Width/2,10).A>0,"Bubble body rendered without desktop capture");
                    int textPixels=0;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++) {
                        Color color=bitmap.GetPixel(x,y);if(color.A>128 && color.R>180 && color.G>180)textPixels++;
                    }
                    Check(textPixels>20,"Arabic bubble text produces visible cream glyphs");
                    foreach(Display d in Displays()) {
                        Vec p=SpeechBubble.Position(d,new Vec(d.Left,d.Top),bitmap.Width,bitmap.Height);
                        Check(p.X>=d.Left && p.Y>=d.Top && p.X+bitmap.Width<=d.Right && p.Y+bitmap.Height<=d.Bottom,"Speech bubble clamped across DPI/negative work areas");
                    }
                }
            }
        }
        static void TestOptions() {
            string path=Path.Combine(Path.GetTempPath(),"Qa3mozOptions-"+Guid.NewGuid().ToString("N")+".json");
            try {
                var settings=new BehaviorOptions{Affection=false,Breaks=false,Variation=false,Quiet=true,AvoidFullscreen=false};settings.Save(path);
                var read=BehaviorOptions.Load(path);Check(!read.Affection && !read.Breaks && !read.Variation && read.Quiet && !read.AvoidFullscreen,"Only preference flags persist across restart");
                var raw=new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string,object>;
                Check(raw.Count==5,"Preference storage contains five toggles and no activity/cursor history");
                File.WriteAllText(path,"not json");Check(BehaviorOptions.Load(path).Affection,"Corrupt settings fall back to conservative defaults");
                File.WriteAllText(path,new string('x',5000));Check(BehaviorOptions.Load(path).AvoidFullscreen,"Oversized settings bounded before parsing");
            }finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
