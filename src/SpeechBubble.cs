using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Qa3moz {
    // Own temporary click-through window. No OS notification, activation, capture or input polling.
    internal sealed class SpeechBubble : Form {
        IntPtr dc,image;
        Size renderedSize;
        double expires;
        int lastX=Int32.MinValue,lastY=Int32.MinValue;
        public SpeechBubble(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;}
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var cp=base.CreateParams;cp.ExStyle|=0x80000|0x80|0x20|0x08000000;return cp;}}
        protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
        public static string TextFor(LittleMessage message) {
            return message==LittleMessage.Break?"\u0627\u0633\u062a\u0631\u0627\u062d\u0629 \u0635\u063a\u064a\u0631\u0629\u061f":"\u0634\u0648\u064a\u0629 \u062d\u0646\u0627\u0646\u061f";
        }
        public static Bitmap Render(LittleMessage message,double scale) {
            scale=Math.Max(.75,Math.Min(2.5,scale));int w=(int)Math.Round(220*scale),h=(int)Math.Round(68*scale);
            var bitmap=new Bitmap(w,h,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(bitmap))using(var body=new GraphicsPath()) {
                g.SmoothingMode=SmoothingMode.AntiAlias;float margin=2*(float)scale,r=12*(float)scale;
                var rect=new RectangleF(margin,margin,w-2*margin,h-12*(float)scale-margin);
                body.AddArc(rect.Left,rect.Top,r,r,180,90);body.AddArc(rect.Right-r,rect.Top,r,r,270,90);
                body.AddArc(rect.Right-r,rect.Bottom-r,r,r,0,90);body.AddArc(rect.Left,rect.Bottom-r,r,r,90,90);body.CloseFigure();
                using(var fill=new SolidBrush(PetColors.Background))using(var border=new Pen(PetColors.Emerald,Math.Max(1,(float)scale))) {
                    g.FillPath(fill,body);g.DrawPath(border,body);
                    g.FillPolygon(fill,new[]{new PointF(w/2-7*(float)scale,rect.Bottom-1),new PointF(w/2+7*(float)scale,rect.Bottom-1),new PointF(w/2,h-2*(float)scale)});
                }
                using(var font=new Font("Segoe UI",15*(float)scale,FontStyle.Regular,GraphicsUnit.Pixel))
                using(var color=new SolidBrush(PetColors.Cream))using(var format=new StringFormat()) {
                    format.Alignment=StringAlignment.Center;format.LineAlignment=StringAlignment.Center;format.FormatFlags=StringFormatFlags.DirectionRightToLeft;
                    g.DrawString(TextFor(message),font,color,rect,format);
                }
            }
            return bitmap;
        }
        public static Vec Position(Display d,Vec pet,int width,int height) {
            double x=pet.X+d.PetWidth/2-width/2,y=pet.Y-height-8*d.Scale;
            if(y<d.Top)y=pet.Y+d.PetHeight+8*d.Scale;
            return new Vec(Math.Max(d.Left,Math.Min(d.Right-width,x)),Math.Max(d.Top,Math.Min(d.Bottom-height,y)));
        }
        public void ShowMessage(LittleMessage message,Display display,Vec pet,double now) {
            Dismiss();if(display.Width<165 || display.Height<51)return;
            double scale=Math.Min(display.Scale,Math.Min(display.Width/220,display.Height/68));
            using(var bitmap=Render(message,scale)) {
                renderedSize=bitmap.Size;image=bitmap.GetHbitmap(Color.FromArgb(0));
            }
            if(dc==IntPtr.Zero)dc=Native.CreateCompatibleDC(IntPtr.Zero);
            Native.Require(dc!=IntPtr.Zero && image!=IntPtr.Zero,"Cannot render own speech bubble");
            expires=now+5;Place(display,pet);Show();Native.EnsureOwnTopmost(Handle);
        }
        void Place(Display display,Vec pet) {
            Vec p=Position(display,pet,renderedSize.Width,renderedSize.Height);
            int x=(int)Math.Round(p.X),y=(int)Math.Round(p.Y);if(x==lastX && y==lastY)return;
            var at=new Native.POINT((int)Math.Round(p.X),(int)Math.Round(p.Y));var origin=new Native.POINT(0,0);
            var size=new Native.SIZE(renderedSize.Width,renderedSize.Height);var blend=new Native.BLEND{Alpha=255,Format=1};
            IntPtr previous=Native.SelectObject(dc,image);
            try{Native.Require(Native.UpdateLayeredWindow(Handle,IntPtr.Zero,ref at,ref size,dc,ref origin,0,ref blend,2),"Cannot place own speech bubble");}
            finally{Native.SelectObject(dc,previous);}
            lastX=x;lastY=y;
        }
        public void Tick(double now,Display display,Vec pet) {if(!Visible)return;if(now>=expires){Dismiss();return;}Place(display,pet);}
        public void Dismiss(){Hide();lastX=lastY=Int32.MinValue;if(image!=IntPtr.Zero){Native.DeleteObject(image);image=IntPtr.Zero;}}
        protected override void Dispose(bool disposing){if(disposing){Dismiss();if(dc!=IntPtr.Zero){Native.DeleteDC(dc);dc=IntPtr.Zero;}}base.Dispose(disposing);}
    }
}
