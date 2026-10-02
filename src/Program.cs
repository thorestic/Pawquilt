using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Qa3moz {
    static class Program {
        [STAThread] public static int Main(string[] args) {
            if(args.Length>0 && (args[0]=="--enable-startup" || args[0]=="--disable-startup")) {
                try{StartupLink.Set(args[0]=="--enable-startup");return 0;}catch(Exception error){Diagnostics.Write("STARTUP SHORTCUT ERROR "+error.Message);return 2;}
            }
            if(args.Length>0 && args[0]=="--self-test") return SelfTests.Run();
            string asset=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","spritesheet-extended.png");
            if(args.Length>0 && args[0]=="--check-asset") return SelfTests.CheckAsset(asset);
            Diagnostics.Start();
            try {
                if(!File.Exists(asset)) throw new FileNotFoundException(
                    "The Qa3moz sprite sheet is missing. Extract the complete Pawquilt release ZIP, keeping the adjacent assets folder.");
                bool first;
                using(var single=new Mutex(true,"Local\\Qa3mozDesktopCompanion",out first)) {
                    if(!first) throw new InvalidOperationException("Pawquilt or the previous Qa3moz Companion is already running. Use its tray icon to pause or exit.");
                    Native.DpiAware(); Diagnostics.Write(Native.OwnEnvironment());
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Companion(asset,args.Length>0 && args[0]=="--paused"));
                }
                return 0;
            } catch(Exception error) {
                Diagnostics.Write("STARTUP ERROR "+error.ToString());
                MessageBox.Show(error.Message,"Pawquilt",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1;
            }
        }
    }
}
