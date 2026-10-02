using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Qa3moz {
    public enum ActivityChoice { Walk, Jump, Transfer, Linger }
    public enum LittleMessage { None, Affection, Break }
    // No OS calls, input contents, logging or networking. All randomness is seedable.
    public sealed class ActivityChooser {
        readonly Random random;
        ActivityChoice last=ActivityChoice.Linger,previous=ActivityChoice.Linger;
        public ActivityChooser(int seed){random=new Random(seed);}
        public ActivityChoice Pick(double energy,double warmth,int monitors,bool nearby,bool calm,bool jumpAllowed,bool transferAllowed) {
            energy=Math.Max(0,Math.Min(1,energy));warmth=Math.Max(.35,Math.Min(1,warmth));
            double[] weights={45+25*energy+(nearby?12:0),6+24*energy*warmth,10+18*energy,12+32*(1-energy)};
            if(monitors<2 || !transferAllowed)weights[2]=0;
            if(!jumpAllowed || last==ActivityChoice.Jump || previous==ActivityChoice.Jump)weights[1]=0;
            if(calm){weights[1]*=.15;weights[2]*=.4;weights[3]*=2;}
            weights[(int)last]*=.25;weights[(int)previous]*=.55;
            double total=0;foreach(double value in weights)total+=value;
            double pick=random.NextDouble()*total;int chosen=weights.Length-1;
            for(int n=0;n<weights.Length;n++){pick-=weights[n];if(pick<0){chosen=n;break;}}
            previous=last;last=(ActivityChoice)chosen;return last;
        }
    }
    public sealed class BehaviorOptions {
        public bool Variation=true,Affection=true,Breaks=true,AvoidFullscreen=true,Quiet=false;
        public static BehaviorOptions Load(string path) {
            try { if(File.Exists(path) && new FileInfo(path).Length<=4096)return new JavaScriptSerializer().Deserialize<BehaviorOptions>(File.ReadAllText(path))??new BehaviorOptions(); }
            catch { }return new BehaviorOptions();
        }
        public void Save(string path){File.WriteAllText(path,new JavaScriptSerializer().Serialize(this));}
    }
    public sealed class LocalHabits {
        readonly Random random;
        readonly BehaviorOptions options;
        public double Time {get;private set;}
        public double Energy {get;private set;}
        public double Warmth {get;private set;}
        public double ActiveWork {get;private set;}
        public double NextAffection {get;private set;}
        public double NextBreakAllowed {get;private set;}
        double lastMessage=-10000,nextFriendly;
        public LocalHabits(int seed,BehaviorOptions settings) {
            random=new Random(seed);options=settings;Energy=.7;Warmth=.65;
            NextAffection=Between(12*60,20*60);nextFriendly=Between(120,300);
        }
        double Between(double a,double b){return a+random.NextDouble()*(b-a);}
        public void Pet(){Warmth=Math.Min(1,Warmth+.25);Energy=Math.Min(1,Energy+.03);NextAffection=Math.Max(NextAffection,Time+20*60);}
        public void DeferMessages(){NextAffection=Math.Max(NextAffection,Time+120);nextFriendly=Math.Max(nextFriendly,Time+60);lastMessage=Time;}
        public void Tick(double dt,double idle,bool sleeping,bool suppressed) {
            dt=Math.Max(0,Math.Min(5,dt));Time+=dt;
            Energy=Math.Max(.3,Math.Min(1,Energy+dt*(sleeping?.0015:-.00008)));
            Warmth=Math.Max(.55,Math.Min(1,Warmth+(.6-Warmth)*dt/1800));
            if(idle>=120)ActiveWork=0;
            else if(!suppressed && !sleeping && idle<=30)ActiveWork+=dt;
            if(suppressed || sleeping){NextAffection=Math.Max(NextAffection,Time+120);nextFriendly=Math.Max(nextFriendly,Time+60);}
        }
        public LittleMessage Message(double idle,bool safe) {
            if(!safe || options.Quiet || idle>30 || Time-lastMessage<120)return LittleMessage.None;
            if(options.Breaks && ActiveWork>=50*60 && Time>=NextBreakAllowed) {
                ActiveWork=0;NextBreakAllowed=Time+60*60;lastMessage=Time;return LittleMessage.Break;
            }
            if(options.Affection && Time>=NextAffection) {
                NextAffection=Time+Between(20*60,30*60);lastMessage=Time;return LittleMessage.Affection;
            }
            return LittleMessage.None;
        }
        public bool Friendly(bool safe) {
            if(!options.Variation || !safe || Energy<.45 || Time<nextFriendly)return false;
            nextFriendly=Time+Between(120,300);return true;
        }
    }
    public struct ForegroundSnapshot {
        public bool Valid,Own,Minimized;
        public long Window;
        public uint Process;
        public double Left,Top,Right,Bottom;
    }
    public sealed class ScreenPolicy {
        readonly HashSet<string> blocked=new HashSet<string>();
        readonly Dictionary<long,ForegroundSnapshot> known=new Dictionary<long,ForegroundSnapshot>();
        readonly List<long> order=new List<long>();
        ForegroundSnapshot legacy;
        public bool AnyFullscreen {get{return blocked.Count>0;}}
        public List<ForegroundSnapshot> KnownWindows {get{return new List<ForegroundSnapshot>(known.Values);}}
        public bool IsBlocked(string id){return blocked.Contains(id);}
        public void Update(List<Display> displays,ForegroundSnapshot window,bool enabled) {
            Update(displays,new[]{window},enabled);
        }
        bool Covers(Display d,ForegroundSnapshot window) {
            double tolerance=3*d.Scale;
            return window.Valid && !window.Own && !window.Minimized && window.Left<=d.FullLeft+tolerance && window.Top<=d.FullTop+tolerance &&
                window.Right>=d.FullRight-tolerance && window.Bottom>=d.FullBottom-tolerance;
        }
        public void Update(List<Display> displays,IEnumerable<ForegroundSnapshot> observations,bool enabled) {
            if(!enabled){blocked.Clear();known.Clear();order.Clear();legacy=new ForegroundSnapshot();return;}
            foreach(var window in observations) {
                if(window.Own)continue;
                bool fullscreen=displays.Exists(d=>Covers(d,window));
                if(window.Window!=0) {
                    if(!fullscreen){known.Remove(window.Window);order.Remove(window.Window);continue;}
                    if(!known.ContainsKey(window.Window)){order.Add(window.Window);if(order.Count>8){known.Remove(order[0]);order.RemoveAt(0);}}
                    known[window.Window]=window;
                } else if(window.Valid)legacy=window;
            }
            blocked.Clear();
            foreach(Display d in displays) {
                if(Covers(d,legacy))blocked.Add(d.Id);
                foreach(var window in known.Values)if(Covers(d,window))blocked.Add(d.Id);
            }
        }
        public List<Display> Allowed(List<Display> displays) {
            return displays.FindAll(d=>!blocked.Contains(d.Id));
        }
    }
}
