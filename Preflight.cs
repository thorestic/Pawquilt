using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Qa3moz {
    internal sealed class PreflightWindow : NativeWindow, IDisposable {
        public PreflightWindow() {
            CreateParams cp=new CreateParams(); cp.Caption="Qa3moz preflight (message only)";
            cp.Parent=new IntPtr(-3); CreateHandle(cp);
        }
        public void Dispose() { DestroyHandle(); }
    }
    internal static class Preflight {
        // Creates only a private message-only window. No overlay, input writes, or other window lookup.
        public static int Run() {
            try {
                Native.DpiAware();
                var displays=Native.Displays();
                Console.WriteLine("PASS: "+displays.Count+" monitor work areas readable (no titles or screen content).");
                using(var window=new PreflightWindow()) {
                    uint modifiers=0x4000|1|2|4;
                    bool session=false;
                    try {
                        Native.Require(Native.RegisterHotKey(window.Handle,1,modifiers,0x50),"Pause shortcut unavailable");
                        Native.Require(Native.RegisterHotKey(window.Handle,2,modifiers,0x51),"Exit shortcut unavailable");
                        Native.Require(Native.RegisterHotKey(window.Handle,3,modifiers,0x46),"Follow shortcut unavailable");
                        Console.WriteLine("PASS: Ctrl+Alt+Shift+P / Q / F registration. All three released on completion.");
                        Native.Require(Native.WTSRegisterSessionNotification(window.Handle,0),"Lock/unlock notification unavailable");
                        session=true; Console.WriteLine("PASS: current-session lock/unlock notification registration.");
                        double idle=Native.IdleSeconds();
                        if(Double.IsNaN(idle) || idle<0) throw new Exception("Invalid idle duration");
                        Console.WriteLine("PASS: aggregate session idle-duration API readable; no individual input is inspected.");
                    } finally {
                        for(int id=1;id<=3;id++) Native.UnregisterHotKey(window.Handle,id);
                        if(session) Native.WTSUnRegisterSessionNotification(window.Handle);
                    }
                }
                Console.WriteLine("No moving overlay was created. Registration does not prove real hotkey delivery; that needs a user trial.");
                return 0;
            } catch(Exception error) { Console.Error.WriteLine("PREFLIGHT BLOCKED: "+error.Message); return 3; }
        }
    }
}
