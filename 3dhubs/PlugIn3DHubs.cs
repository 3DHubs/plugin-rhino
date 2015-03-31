using System;
using System.Xml;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Rhino;
using Rhino.PlugIns;

namespace _3dhubs
{
    public class PlugIn3DHubs : PlugIn
    {
        public static PlugIn3DHubs Instance { get; private set; }
        public Configuration Configuration { get; private set; }
        public Uploader Uploader { get; private set; }
        public UploadForm UploadForm { get; private set; }

        public new Guid Id 
        {
            get {
                Assembly a = Assembly.GetExecutingAssembly();
                object[] idattr = a.GetCustomAttributes(typeof(GuidAttribute), true);
                return new Guid(((GuidAttribute)idattr[0]).Value);
            }
        }

        public new string Name
        {
            get {
                Assembly a = Assembly.GetExecutingAssembly();
                object[] idattr = a.GetCustomAttributes(typeof(AssemblyTitleAttribute), true);
                return ((AssemblyTitleAttribute)idattr[0]).Title;
            }
        }

        public new string Version
        {
            get {
                Assembly a = Assembly.GetExecutingAssembly();
                return a.GetName().Version.ToString();
            }
        }

        public PlugIn3DHubs()
        {
            Instance = this;
            Configuration = new Configuration();
            Configuration.Load();
            Uploader = new Uploader(Configuration);
            UploadForm = new UploadForm(Configuration, Uploader);
            Utilities.ScriptInvokeControl = UploadForm;
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            return LoadReturnCode.Success;
        }
    }
}