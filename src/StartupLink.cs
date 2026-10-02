using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Qa3moz {
    internal static class StartupLink {
        static string Link { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Pawquilt.lnk"); } }
        static string Executable { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Pawquilt.exe"); } }
        static object Invoke(object instance,string member,BindingFlags flags,params object[] args) {
            return instance.GetType().InvokeMember(member,flags,null,instance,args);
        }
        static object Open(out object shell) {
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));
            return Invoke(shell,"CreateShortcut",BindingFlags.InvokeMethod,Link);
        }
        static bool Own(object shortcut) {
            string target=Convert.ToString(Invoke(shortcut,"TargetPath",BindingFlags.GetProperty));
            return String.Equals(Path.GetFullPath(target),Executable,StringComparison.OrdinalIgnoreCase);
        }
        public static bool Enabled() {
            if(!File.Exists(Link))return false;object shell=null,shortcut=null;
            try{shortcut=Open(out shell);return Own(shortcut);}finally{if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
        }
        public static void Set(bool enabled) {
            object shell=null,shortcut=null;
            try {
                shortcut=Open(out shell);
                if(File.Exists(Link) && !Own(shortcut))throw new IOException("Existing same-name startup shortcut is unrelated; it was left unchanged");
                if(!enabled){if(File.Exists(Link))File.Delete(Link);return;}
                Invoke(shortcut,"TargetPath",BindingFlags.SetProperty,Executable);
                Invoke(shortcut,"Arguments",BindingFlags.SetProperty,"--roam");
                Invoke(shortcut,"WorkingDirectory",BindingFlags.SetProperty,AppDomain.CurrentDomain.BaseDirectory);
                Invoke(shortcut,"Description",BindingFlags.SetProperty,"Pawquilt desktop companion; remove via its menu");
                Invoke(shortcut,"WindowStyle",BindingFlags.SetProperty,1);
                Invoke(shortcut,"Save",BindingFlags.InvokeMethod);
            }finally{if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
        }
    }
}
