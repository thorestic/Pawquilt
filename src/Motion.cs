using System;
using System.Collections.Generic;

namespace Qa3moz {
    public struct Vec {
        public double X, Y;
        public Vec(double x, double y) { X = x; Y = y; }
        public static Vec operator +(Vec a, Vec b) { return new Vec(a.X+b.X,a.Y+b.Y); }
        public static Vec operator -(Vec a, Vec b) { return new Vec(a.X-b.X,a.Y-b.Y); }
        public static Vec operator *(Vec a, double k) { return new Vec(a.X*k,a.Y*k); }
        public double Length { get { return Math.Sqrt(X*X+Y*Y); } }
        public static Vec Lerp(Vec a, Vec b, double t) { return a+(b-a)*t; }
    }
    public class Display {
        public string Id;
        public double Left, Top, Right, Bottom, Scale;
        public double FullLeft,FullTop,FullRight,FullBottom;
        public double SizeFactor=.70;
        public bool Square;
        public Display(string id, double l, double t, double r, double b, double s) {
            Id=id; Left=l; Top=t; Right=r; Bottom=b; Scale=Math.Max(.75,Math.Min(4,s));
            FullLeft=l;FullTop=t;FullRight=r;FullBottom=b;
        }
        public double Width { get { return Math.Max(1,Right-Left); } }
        public double Height { get { return Math.Max(1,Bottom-Top); } }
        public Vec Center { get { return new Vec((Left+Right)/2,(Top+Bottom)/2); } }
        public double PetScale { get { return Math.Min(2.1,Math.Min(SizeFactor*Scale,Math.Min(Width/192,Height/(Square?192:208)))); } }
        public double PetWidth { get { return Math.Max(1,Math.Round(192*PetScale)); } }
        public double PetHeight { get { return Math.Max(1,Math.Round((Square?192:208)*PetScale)); } }
        public Vec Clamp(Vec p) {
            return new Vec(Math.Max(Left,Math.Min(Right-PetWidth,p.X)),
                           Math.Max(Top,Math.Min(Bottom-PetHeight,p.Y)));
        }
        public bool Contains(Vec p) { return p.X>=Left && p.Y>=Top && p.X<Right && p.Y<Bottom; }
        public bool ContainsPet(Vec p) {
            return p.X>=Left-.001 && p.Y>=Top-.001 && p.X+PetWidth<=Right+.001 && p.Y+PetHeight<=Bottom+.001;
        }
    }
    public enum Mood { Idle, Walk, Jump, Transfer, Rest }
    // Pure model: no desktop, file, input, or network APIs. All coordinates are physical pixels.
    public class Motion {
        readonly Random rng;
        readonly ActivityChooser chooser;
        public List<Display> Displays;
        public int Monitor;
        public Vec Position;
        public Mood State { get; private set; }
        public bool Paused, Follow;
        public bool FacesRight=true;
        public double Alpha=1;
        public double SleepAfter=180;
        public bool Variation,CalmContext;
        public bool UseSixMoves,Running;
        public double Energy=.7,Warmth=.65;
        public bool IsContinuousTransfer {get{return State==Mood.Transfer && transferAdjacent;}}
        public bool FitsWorkAreas {
            get {
                if(Current.ContainsPet(Position))return true;
                if(State!=Mood.Transfer || !transferAdjacent)return false;
                Display a=Displays[transferSource],b=Displays[transferMonitor];
                return Position.X>=Math.Min(a.Left,b.Left)-.001 && Position.X+Current.PetWidth<=Math.Max(a.Right,b.Right)+.001 &&
                    Position.Y>=Math.Max(a.Top,b.Top)-.001 && Position.Y+Current.PetHeight<=Math.Min(a.Bottom,b.Bottom)+.001;
            }
        }
        public double StateTime { get { return elapsed; } }
        double elapsed, deadline, speed;
        Vec target, jumpBase, transferFrom, transferTo;
        int transferMonitor,transferSource;
        bool transferAdjacent,transferPending;
        double jumpCooldown,transferCooldown;
        public Display Current { get { return Displays[Monitor]; } }
        public Motion(List<Display> displays, int seed) {
            if(displays==null || displays.Count==0) throw new ArgumentException("No usable displays");
            rng=new Random(seed);chooser=new ActivityChooser(seed^31991); Displays=displays; Monitor=0;
            Position=Current.Clamp(new Vec(Current.Left+Current.Width*.55,Current.Top+Current.Height*.72));
            EnterIdle();
        }
        double Between(double a,double b) { return a+rng.NextDouble()*(b-a); }
        void EnterIdle() { State=Mood.Idle; Running=false; elapsed=0; deadline=Variation?Between(2+3*(1-Energy),5+6*(1-Energy)):Between(1.6,5.5);if(CalmContext)deadline*=1.4; Alpha=1;transferAdjacent=transferPending=false; }
        public void Reset(List<Display> displays) {
            if(displays==null || displays.Count==0) throw new ArgumentException("No usable displays");
            string oldId=Current.Id; Displays=displays;
            Monitor=Math.Max(0,displays.FindIndex(d=>d.Id==oldId));
            Position=Current.Clamp(Position); EnterIdle();
        }
        public void Reposition(int monitor,Vec p) {
            Monitor=Math.Max(0,Math.Min(Displays.Count-1,monitor)); Position=Current.Clamp(p); EnterIdle();
        }
        public void CancelMotion() { Position=Current.Clamp(Position); EnterIdle(); }
        Vec RandomPoint(Display d) {
            return d.Clamp(new Vec(Between(d.Left,d.Right-d.PetWidth),Between(d.Top,d.Bottom-d.PetHeight)));
        }
        void StartJump() { State=Mood.Jump; elapsed=0; deadline=.84; jumpBase=Position;jumpCooldown=Between(20,45); }
        bool PrepareAdjacentTransfer() {
            Display a=Current,b=Displays[transferMonitor];
            bool right=Math.Abs(a.Right-b.Left)<1,left=Math.Abs(a.Left-b.Right)<1;
            double top=Math.Max(a.Top,b.Top),bottom=Math.Min(a.Bottom,b.Bottom);
            double height=Math.Max(a.PetHeight,b.PetHeight),lift=70*Math.Max(a.Scale,b.Scale);
            if((!right && !left) || bottom-top<height+lift+30)return false;
            double y=Between(top+lift+10,bottom-height-10);
            double seam=right?a.Right:a.Left;
            transferFrom=a.Clamp(new Vec(right?seam-a.PetWidth-12:seam+12,y));
            transferTo=b.Clamp(new Vec(right?seam+12:seam-b.PetWidth-12,y));
            target=transferFrom;transferSource=Monitor;transferAdjacent=transferPending=true;
            speed=Between(80,115)*a.Scale;FacesRight=target.X>=Position.X;State=Mood.Walk;elapsed=0;Alpha=1;
            return true;
        }
        void Plan(Vec? cursor) {
            bool nearby=cursor.HasValue && (cursor.Value-Position).Length<500*Current.Scale;
            ActivityChoice choice=Variation?chooser.Pick(Energy,Warmth,Displays.Count,nearby,CalmContext,jumpCooldown<=0,transferCooldown<=0):ActivityChoice.Walk;
            if(Variation && choice==ActivityChoice.Linger){EnterIdle();return;}
            if(Displays.Count>1 && (Variation?choice==ActivityChoice.Transfer:rng.NextDouble()<.27)) {
                transferCooldown=Between(12,28);
                transferMonitor=(Monitor+1+rng.Next(Displays.Count-1))%Displays.Count;
                if(PrepareAdjacentTransfer())return;
                transferFrom=Position; transferTo=RandomPoint(Displays[transferMonitor]);
                State=Mood.Transfer; elapsed=0; deadline=1.0; return;
            }
            if(Variation?choice==ActivityChoice.Jump:rng.NextDouble()<.20) { StartJump(); return; }
            target=RandomPoint(Current);
            if(Follow && cursor.HasValue && Current.Contains(cursor.Value) && rng.NextDouble()<.5) {
                Vec center=Position+new Vec(Current.PetWidth/2,Current.PetHeight/2);
                Vec delta=center-cursor.Value;
                if(delta.Length>280*Current.Scale && delta.Length<1000*Current.Scale) {
                    target=Current.Clamp(cursor.Value+delta*(220*Current.Scale/delta.Length)
                                         -new Vec(Current.PetWidth/2,Current.PetHeight/2));
                }
            }
            speed=(Variation?Between(55+30*Energy,80+60*Energy):Between(65,125))*Current.Scale*(CalmContext?.65:1);
            Running=UseSixMoves && Variation && !CalmContext && Energy>.55 && rng.NextDouble()<.35;
            if(Running)speed*=1.35;
            FacesRight=target.X>=Position.X; State=Mood.Walk; elapsed=0;
        }
        public void Tick(double dt,double idleSeconds,Vec? cursor) {
            dt=Math.Max(0,Math.Min(.05,dt));
            if(Paused) return;
            jumpCooldown=Math.Max(0,jumpCooldown-dt);transferCooldown=Math.Max(0,transferCooldown-dt);
            if(idleSeconds>=SleepAfter) {
                if(State!=Mood.Rest) { State=Mood.Rest; elapsed=0; Position=Current.Clamp(Position); Alpha=1; }
                elapsed+=dt; return;
            }
            if(State==Mood.Rest) EnterIdle();
            elapsed+=dt;
            if(State==Mood.Idle) { if(elapsed>=deadline) Plan(cursor); return; }
            if(State==Mood.Walk) {
                Vec delta=target-Position;
                double step=speed*(.82+.18*Math.Sin(elapsed*2.4))*dt;
                if(delta.Length<=Math.Max(2,step)) {
                    Position=target;
                    if(transferPending){transferPending=false;State=Mood.Transfer;elapsed=0;deadline=1.0;FacesRight=transferTo.X>=Position.X;}
                    else EnterIdle();return;
                }
                Vec next=Current.Clamp(Position+delta*(step/delta.Length));
                // Optional following keeps a quiet personal-space bubble; no mouse writes or clicks.
                if(Follow && cursor.HasValue &&
                   (next+new Vec(Current.PetWidth/2,Current.PetHeight/2)-cursor.Value).Length<160*Current.Scale) {
                    EnterIdle(); return;
                }
                Position=next; return;
            }
            if(State==Mood.Jump) {
                double t=Math.Min(1,elapsed/deadline);
                Position=Current.Clamp(jumpBase+new Vec(0,-60*Current.Scale*Math.Sin(Math.PI*t)));
                if(t>=1) { Position=jumpBase; EnterIdle(); } return;
            }
            if(State==Mood.Transfer) {
                double t=Math.Min(1,elapsed/deadline);
                if(transferAdjacent) {
                    Display a=Displays[transferSource],b=Displays[transferMonitor];
                    Vec fromCenter=transferFrom+new Vec(a.PetWidth/2,a.PetHeight/2),toCenter=transferTo+new Vec(b.PetWidth/2,b.PetHeight/2);
                    Monitor=t<.5?transferSource:transferMonitor;Alpha=1;
                    Vec center=Vec.Lerp(fromCenter,toCenter,t)+new Vec(0,-70*Math.Max(a.Scale,b.Scale)*Math.Sin(Math.PI*t));
                    Position=center-new Vec(Current.PetWidth/2,Current.PetHeight/2);
                    if(t>=1){Position=Current.Clamp(transferTo);EnterIdle();}return;
                }
                Alpha=Math.Abs(2*t-1);
                if(t<.5) Position=Current.Clamp(transferFrom+new Vec(0,-45*Current.Scale*Math.Sin(2*Math.PI*t)));
                else {
                    Monitor=transferMonitor;
                    Position=Current.Clamp(transferTo+new Vec(0,-45*Current.Scale*Math.Sin(2*Math.PI*(1-t))));
                }
                if(t>=1) { Position=Current.Clamp(transferTo); EnterIdle(); }
            }
        }
        public int LookIndex(Vec cursor) {
            Vec center=Position+new Vec(Current.PetWidth/2,Current.PetHeight/2);
            Vec delta=cursor-center;
            double angle=Math.Atan2(delta.X,-delta.Y)*180/Math.PI;
            return ((int)Math.Round(angle/22.5)+16)%16;
        }
    }
    public static class AtlasLayout {
        public const int Width=1536, Height=2288, CellWidth=192, CellHeight=208;
        public static readonly int[] Counts={6,8,8,4,5,8,6,6,6,8,8};
        static readonly int[][] Durations={
            new[]{280,110,110,140,140,320}, new[]{120,120,120,120,120,120,120,220},
            new[]{120,120,120,120,120,120,120,220}, new[]{140,140,140,280},
            new[]{140,140,140,140,280}, new[]{140,140,140,140,140,140,140,240},
            new[]{150,150,150,150,150,260}, new[]{120,120,120,120,120,220},
            new[]{150,150,150,150,150,280}
        };
        public static int Frame(int row,double seconds) {
            if(row<0 || row>=9) throw new ArgumentOutOfRangeException("row");
            int total=0; foreach(int ms in Durations[row]) total+=ms;
            double t=Math.Max(0,seconds*1000)%total;
            for(int i=0;i<Durations[row].Length;i++) { if(t<Durations[row][i]) return i; t-=Durations[row][i]; }
            return 0;
        }
    }
}
