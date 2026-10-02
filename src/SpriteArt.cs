using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Script.Serialization;

namespace Qa3moz {
    internal sealed class PixelArt : IDisposable {
        public static readonly string[] Names={"idle","walk_right","jump","land","held_surprise","petting_happy","yawn","stretch","curl_sleep","sleep_loop","wake"};
        public static readonly int[] Counts={4,4,3,1,4,3,4,4,4,2,4};
        public static readonly string[] MoveNames={"light_run","happy_jump","somersault","ear_scratch","sleepy_stretch","request_petting"};
        public static readonly int[] MoveCounts={4,4,4,4,6,4};
        readonly Dictionary<string,Bitmap> strips=new Dictionary<string,Bitmap>();
        readonly Dictionary<string,int[]> durations=new Dictionary<string,int[]>();
        public bool Ready { get { return strips.Count>=Names.Length; } }
        public bool HasMoves { get { return strips.Count==Names.Length+MoveNames.Length; } }
        public string Problem { get; private set; }
        public const int Cell=128;
        // A complete manifest and complete art set are required. Never activate a partial set.
        public PixelArt(string directory) {
            Problem="Pixel assets not supplied";
            if(!Directory.Exists(directory)) return;
            try {
                string manifest=Path.Combine(directory,"runtime-manifest.json");
                if(!File.Exists(manifest)) throw new InvalidDataException("Validated runtime-manifest.json not supplied");
                var root=(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(manifest));
                var states=(Dictionary<string,object>)root["states"];
                var names=new List<string>(Names);var counts=new List<int>(Counts);
                if(Array.Exists(MoveNames,name=>states.ContainsKey(name))){names.AddRange(MoveNames);counts.AddRange(MoveCounts);}
                for(int i=0;i<names.Count;i++) {
                    string name=names[i]; var spec=(Dictionary<string,object>)states[name];
                    int[] timing=TimingAdapter.Read(spec["durations_ms"],counts[i],name);
                    string path=Path.Combine(directory,name+".png");
                    using(var raw=new Bitmap(path)) {
                        if(raw.Height!=Cell || raw.Width!=Cell*counts[i]) throw new InvalidDataException("Wrong strip geometry: "+name);
                        if(!Image.IsAlphaPixelFormat(raw.PixelFormat)) throw new InvalidDataException("Strip lacks alpha: "+name);
                        for(int frame=0;frame<counts[i];frame++) {
                            bool art=false,transparent=false;
                            for(int y=0;y<Cell;y++) for(int x=0;x<Cell;x++) {
                                int a=raw.GetPixel(frame*Cell+x,y).A;
                                art|=a>=16; transparent|=a==0;
                            }
                            if(!art || !transparent) throw new InvalidDataException("Empty or opaque cell: "+name+" "+frame);
                        }
                        strips.Add(name,new Bitmap(raw)); durations.Add(name,timing);
                    }
                }
                Problem=null; Diagnostics.Write("PIXEL ART validated: 128px cells, "+(HasMoves?63:37)+" frames, manifest timing; sixMoves="+HasMoves);
            } catch(Exception error) { Dispose(); Problem=error.Message; Diagnostics.Write("PIXEL ART pending: "+Problem); }
        }
        public Bitmap Strip(string name){return strips[name];}
        int TotalMilliseconds(string name){int total=0;foreach(int ms in durations[name])total+=ms;return total;}
        public double Duration(string name){return TotalMilliseconds(name)/1000.0;}
        public int Frame(string name,double time,bool loop) {
            int[] timing=durations[name]; double total=TotalMilliseconds(name);
            if(Double.IsNaN(time) || Double.IsInfinity(time))time=0;
            double t=Math.Max(0,time*1000); if(loop)t%=total;
            for(int i=0;i<timing.Length;i++){if(t<timing[i])return i;t-=timing[i];}
            return timing.Length-1;
        }
        public void Dispose(){foreach(var b in strips.Values)b.Dispose();strips.Clear();durations.Clear();}
    }
    internal static class SpriteArt {
        public static Bitmap Render(Bitmap source,Rectangle cell,int width,int height,bool pixel,bool mirror) {
            var image=new Bitmap(width,height,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(image)) {
                g.CompositingMode=CompositingMode.SourceCopy;
                g.InterpolationMode=pixel?InterpolationMode.NearestNeighbor:InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode=pixel?PixelOffsetMode.Half:PixelOffsetMode.HighQuality;
                int side=Math.Min(width,height);
                Rectangle dest=pixel?new Rectangle((width-side)/2,height-side,side,side):new Rectangle(0,0,width,height);
                if(mirror){g.TranslateTransform(width,0);g.ScaleTransform(-1,1);}
                g.DrawImage(source,dest,cell,GraphicsUnit.Pixel);
            }
            // Layered-window zero-alpha pixels pass through even to other processes.
            // Match the hit mask by clearing barely visible antialias fringe once per cached frame.
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                if(image.GetPixel(x,y).A<16)image.SetPixel(x,y,Color.FromArgb(0,0,0,0));
            return image;
        }
    }
}
