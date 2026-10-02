using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Qa3moz {
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X,Y; public POINT(int x,int y){X=x;Y=y;} }
        [StructLayout(LayoutKind.Sequential)] internal struct SIZE { public int X,Y; public SIZE(int x,int y){X=x;Y=y;} }
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct MONITORINFO {
            public uint Size; public RECT Monitor,Work; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device;
        }
        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint Size,Time; }
        [StructLayout(LayoutKind.Sequential)] struct TRACK { public uint Size,Flags;public IntPtr Window;public uint Hover; }
        [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct BLEND { public byte Op,Flags,Alpha,Format; }
        internal delegate bool MonitorProc(IntPtr monitor,IntPtr dc,ref RECT rect,IntPtr param);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorProc proc,IntPtr param);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        [DllImport("user32.dll")] static extern bool TrackMouseEvent(ref TRACK track);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd,int id);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr destinationDc,ref POINT destination,ref SIZE size,IntPtr sourceDc,ref POINT source,uint color,ref BLEND blend,uint flags);
        [DllImport("gdi32.dll",SetLastError=true)] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("wtsapi32.dll",SetLastError=true)] internal static extern bool WTSRegisterSessionNotification(IntPtr window,uint flags);
        [DllImport("wtsapi32.dll")] internal static extern bool WTSUnRegisterSessionNotification(IntPtr window);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint process);
        [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
        [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
        [DllImport("user32.dll")] static extern IntPtr GetProcessWindowStation();
        [DllImport("user32.dll")] static extern IntPtr GetThreadDesktop(uint thread);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder buffer,uint bytes,out uint needed);
        static string ObjectName(IntPtr h) {
            var name=new StringBuilder(256); uint needed;
            return GetUserObjectInformation(h,2,name,512,out needed)?name.ToString():"unavailable";
        }
        // These methods accept only this app's own Handle; no foreign-window enumeration or content.
        public static string OwnEnvironment() {
            return "station="+ObjectName(GetProcessWindowStation())+" desktop="+ObjectName(GetThreadDesktop(GetCurrentThreadId()));
        }
        public static string OwnWindow(IntPtr hwnd) {
            RECT r; bool bounds=GetWindowRect(hwnd,out r);
            return "hwnd="+hwnd.ToInt64()+" visible="+IsWindowVisible(hwnd)+
                " bounds="+(bounds?r.Left+","+r.Top+","+r.Right+","+r.Bottom:"unavailable")+
                " style=0x"+GetWindowLongPtr(hwnd,-16).ToInt64().ToString("X")+
                " exStyle=0x"+GetWindowLongPtr(hwnd,-20).ToInt64().ToString("X");
        }
        public static void EnsureOwnTopmost(IntPtr hwnd) {
            if((GetWindowLongPtr(hwnd,-20).ToInt64()&8)==0)
                Require(SetWindowPos(hwnd,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10),"Cannot keep own pet above regular windows");
        }
        public static void DpiAware() {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch(EntryPointNotFoundException) { }
        }
        public static List<Display> Displays() {
            var displays=new List<Display>();
            MonitorProc callback=delegate(IntPtr h,IntPtr dc,ref RECT r,IntPtr p) {
                MONITORINFO mi=new MONITORINFO(); mi.Size=(uint)Marshal.SizeOf(typeof(MONITORINFO));
                if(!GetMonitorInfo(h,ref mi)) return true;
                uint x=96,y=96;
                try { if(GetDpiForMonitor(h,0,out x,out y)!=0) x=96; } catch(DllNotFoundException) { } catch(EntryPointNotFoundException) { }
                if(mi.Work.Right>mi.Work.Left && mi.Work.Bottom>mi.Work.Top)
                    displays.Add(new Display(mi.Device,mi.Work.Left,mi.Work.Top,mi.Work.Right,mi.Work.Bottom,x/96.0){FullLeft=mi.Monitor.Left,FullTop=mi.Monitor.Top,FullRight=mi.Monitor.Right,FullBottom=mi.Monitor.Bottom});
                return true;
            };
            if(!EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero) || displays.Count==0)
                throw new InvalidOperationException("Cannot read monitor work areas");
            return displays;
        }
        public static Vec? Cursor() { POINT p; return GetCursorPos(out p)?(Vec?)new Vec(p.X,p.Y):null; }
        // Passive, user-requested fullscreen heuristic: bounds/minimized/own-process only.
        // No titles, classes, content, screenshots, process names or window manipulation.
        public static ForegroundSnapshot ForegroundBounds() {
            return ReadWindowBounds(GetForegroundWindow());
        }
        static ForegroundSnapshot ReadWindowBounds(IntPtr hwnd) {
            var result=new ForegroundSnapshot{Window=hwnd.ToInt64()};uint process;
            if(hwnd==IntPtr.Zero || GetWindowThreadProcessId(hwnd,out process)==0)return result;
            result.Process=process;result.Own=process==GetCurrentProcessId();result.Minimized=IsIconic(hwnd);
            if(result.Own){result.Valid=true;return result;}
            if(!IsWindowVisible(hwnd))return result;
            // A maximized framed app (including custom title bars) is ordinary desktop use.
            // Its outer rectangle can cover a whole monitor; do not hide the pet for it.
            // Desktop/shell surfaces are also not fullscreen applications.
            if(hwnd==GetShellWindow() || hwnd==GetDesktopWindow() ||
                (GetWindowLongPtr(hwnd,-16).ToInt64()&0x00C00000)!=0)return result;
            RECT rect;if(!GetWindowRect(hwnd,out rect))return result;
            result.Valid=true;result.Left=rect.Left;result.Top=rect.Top;result.Right=rect.Right;result.Bottom=rect.Bottom;return result;
        }
        public static ForegroundSnapshot RecheckKnownForeground(ForegroundSnapshot prior) {
            var now=ReadWindowBounds(new IntPtr(prior.Window));
            if(now.Process!=prior.Process)now.Valid=false;return now;
        }
        public static void TrackOwnLeave(IntPtr hwnd) {
            TRACK track=new TRACK();track.Size=(uint)Marshal.SizeOf(typeof(TRACK));track.Flags=2;track.Window=hwnd;TrackMouseEvent(ref track);
        }
        public static double IdleSeconds() {
            LASTINPUTINFO info=new LASTINPUTINFO(); info.Size=(uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if(!GetLastInputInfo(ref info)) throw new InvalidOperationException("Cannot read session idle duration");
            return unchecked((uint)Environment.TickCount-info.Time)/1000.0;
        }
        public static void Require(bool success,string action) {
            if(!success) throw new Win32Exception(Marshal.GetLastWin32Error(),action);
        }
    }
}
