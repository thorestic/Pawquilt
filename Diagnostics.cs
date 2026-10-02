using System;
using System.IO;

namespace Qa3moz {
    // Startup and error metadata for this app only. No user input, titles, screenshots, or cursor logs.
    internal static class Diagnostics {
        static string PathName { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostic.txt"); } }
        public static void Start() { try { File.WriteAllText(PathName,"Qa3moz own-app startup diagnostics\r\n"); } catch { } }
        public static void Write(string message) {
            try { File.AppendAllText(PathName,DateTime.UtcNow.ToString("o")+" "+message+"\r\n"); } catch { }
        }
    }
}
