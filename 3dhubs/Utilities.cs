using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Windows.Forms;
using System.Net;
using System.Web.Script.Serialization;
using System.Threading;

using Rhino;

namespace _3dhubs
{
    class ScriptInvokeException : Exception 
    { 
        public ScriptInvokeException(string message) : base(message) { }
    }
    
    delegate void ScriptDelegate();

    class Utilities
    {
        /* Qualifies a file path with the plugin directory. */
        public static string BuildPluginPath(string fileName)
        {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            string dir = Path.GetDirectoryName(path);
            return Path.Combine(dir, fileName);
        }

        public static string BuildTempFilePath(string extension)
        {
            string dir = Path.GetTempPath();
            string name = Path.GetRandomFileName();
            return Path.Combine(dir, name) + "." + extension;
        }

        public static Control ScriptInvokeControl { get; set; }

        public static void RunMacroOnMainThread(string macro, bool echo = false)
        {
            bool success = false;
            using (var waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset))
            {
                ScriptDelegate d = () =>
                {
                    success = RhinoApp.RunScript(macro, echo);
                    waitHandle.Set();
                };
                RhinoApp.MainApplicationWindow.Invoke(d);
                waitHandle.WaitOne();
            }
            if (!success)
                throw new ScriptInvokeException("RunScript() returned false");
        }

        /* Formats a size in bytes into a human-readable string. */
        public static string FormatDataSize(long bytes)
        {
            if (bytes < 1 << 20)
                return string.Format("{0:F1}KB", (float)bytes / (float)(1 << 10));
            if (bytes < 1 << 30)
                return string.Format("{0:F2}MB", (float)bytes / (float)(1 << 20));
            return string.Format("{0:F3}GB", (float)bytes / (float)(1 << 30));
        }

        /* Parses JSON into a dictionary of strings. */
        public static Dictionary<string, dynamic> ParseJson(string json)
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Deserialize<dynamic>(json);
        }

        /* Parses the body of a JSON web response into a dictionary of strings. */
        public static Dictionary<string, dynamic> ParseJsonResponse(WebResponse response)
        {
            if (!response.ContentType.StartsWith("application/json") && 
                !response.ContentType.StartsWith("text/json"))
                throw new Exception("Unsupported content type.");
            using (var stream = response.GetResponseStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    return ParseJson(json);
                }
            }
        }
    }
}
