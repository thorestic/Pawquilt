using System;
using System.Drawing;

namespace Qa3moz {
    public enum Reaction { None, Held, Pet, Landing, Yawn, Stretch, Curl, Sleep, Wake, HappyJump, Somersault, Scratch, Request }
    // Pure interaction model. Only own-window mouse events are supplied by the form.
    public sealed class Interaction {
        public bool Armed { get; private set; }
        public bool Dragging { get; private set; }
        public Reaction State { get; private set; }
        public double Time { get; private set; }
        public bool ManualRest;
        public double AutoSleepCooldown {get;private set;}
        public double PetDuration=.84,LandingDuration=.35,YawnDuration=1.08,StretchDuration=.96,CurlDuration=1.08,WakeDuration=.96;
        public bool UseSixMoves;
        public double HappyDuration=.62,SomersaultDuration=.58,ScratchDuration=.83,RequestDuration=1.58;
        public bool IsBusy { get { return Armed || State!=Reaction.None; } }
        Vec down,last,anchor;
        bool downHead,haveStroke;
        double lastPet=-100,strokeDistance;
        public static bool Head(double x,double y) {
            double dx=(x-.53)/.25,dy=(y-.30)/.23; return dx*dx+dy*dy<=1;
        }
        public static bool Opaque(Bitmap frame,int x,int y) {
            return frame!=null && x>=0 && y>=0 && x<frame.Width && y<frame.Height && frame.GetPixel(x,y).A>=16;
        }
        public void Down(Vec screen,double nx,double ny) {
            if(Dragging) return;
            Armed=true; down=last=screen; anchor=new Vec(nx,ny); downHead=Head(nx,ny);
            ManualRest=false;AutoSleepCooldown=5; State=Reaction.None; Time=0; haveStroke=false; strokeDistance=0;
        }
        public bool Move(Vec screen,double nx,double ny,double scale,double now) {
            if(Dragging) return true;
            if(Armed && (screen-down).Length>6*scale && (!downHead || !Head(nx,ny))) {
                Dragging=true; State=Reaction.Held; Time=0; return true;
            }
            if(Head(nx,ny)) {
                if(haveStroke){double step=(screen-last).Length;strokeDistance=step<60*scale?strokeDistance+step:0;}
                haveStroke=true;
                if(strokeDistance>=9*scale && now-lastPet>=1.5) {
                    ManualRest=false;AutoSleepCooldown=5;State=Reaction.Pet; Time=0; lastPet=now; strokeDistance=0;
                }
            } else { haveStroke=false; strokeDistance=0; }
            last=screen; return false;
        }
        public void DragTo(Motion motion,Vec screen) {
            if(!Dragging) return;
            int index=0; double best=Double.MaxValue;
            for(int i=0;i<motion.Displays.Count;i++) {
                Display d=motion.Displays[i];
                if(d.Contains(screen)) { index=i; best=-1; break; }
                Vec p=new Vec(Math.Max(d.Left,Math.Min(d.Right,screen.X)),Math.Max(d.Top,Math.Min(d.Bottom,screen.Y)));
                double distance=(p-screen).Length; if(distance<best){best=distance;index=i;}
            }
            Display target=motion.Displays[index];
            motion.Reposition(index,screen-new Vec(anchor.X*target.PetWidth,anchor.Y*target.PetHeight));
        }
        public void Up(Motion motion) {
            bool held=Dragging; Armed=Dragging=false; haveStroke=false; strokeDistance=0;
            motion.CancelMotion();
            if(held){State=Reaction.Landing;Time=0;}
        }
        public void Land() { State=Reaction.Landing;Time=0; }
        public void Friendly(bool flip=false){if(!IsBusy && !ManualRest){State=UseSixMoves?(flip?Reaction.Somersault:Reaction.Scratch):Reaction.Pet;Time=0;}}
        public void RequestPetting(){if(UseSixMoves && !IsBusy && !ManualRest){State=Reaction.Request;Time=0;}}
        public void Cancel(Motion motion,bool preserveRest=false) {
            Armed=Dragging=false;haveStroke=false;strokeDistance=0;
            if(!preserveRest || !ManualRest){State=Reaction.None;Time=0;ManualRest=false;}
            motion.CancelMotion();
        }
        public void RestNow() { ManualRest=true; State=UseSixMoves?Reaction.Stretch:Reaction.Yawn;Time=0;Armed=Dragging=false; }
        public void WakeNow() { ManualRest=false;AutoSleepCooldown=5;State=Reaction.Wake;Time=0;Armed=Dragging=false; }
        public void Tick(double dt,double idle,double threshold) {
            dt=Math.Max(0,Math.Min(.05,dt));Time+=dt;AutoSleepCooldown=Math.Max(0,AutoSleepCooldown-dt);
            if(Dragging || Armed) return;
            bool rest=ManualRest || AutoSleepCooldown<=0 && idle>=threshold;
            if(!rest && (State==Reaction.Yawn || State==Reaction.Stretch || State==Reaction.Curl || State==Reaction.Sleep)) { State=Reaction.Wake;Time=0;AutoSleepCooldown=5; }
            if(State==Reaction.None && rest){State=UseSixMoves?Reaction.Stretch:Reaction.Yawn;Time=0;}
            if(State==Reaction.Pet && Time>=PetDuration){State=UseSixMoves?Reaction.HappyJump:Reaction.None;Time=0;}
            else if(State==Reaction.HappyJump && Time>=HappyDuration || State==Reaction.Somersault && Time>=SomersaultDuration){State=Reaction.Landing;Time=0;}
            else if(State==Reaction.Scratch && Time>=ScratchDuration || State==Reaction.Request && Time>=RequestDuration || State==Reaction.Landing && Time>=LandingDuration || State==Reaction.Wake && Time>=WakeDuration){State=Reaction.None;Time=0;}
            else if(State==Reaction.Yawn && Time>=YawnDuration){State=Reaction.Stretch;Time=0;}
            else if(State==Reaction.Stretch && Time>=StretchDuration){State=UseSixMoves?Reaction.Sleep:Reaction.Curl;Time=0;}
            else if(State==Reaction.Curl && Time>=CurlDuration){State=Reaction.Sleep;Time=0;}
        }
    }
}
