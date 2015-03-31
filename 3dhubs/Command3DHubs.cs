using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.DocObjects;

namespace _3dhubs
{
    [
        GuidAttribute("77b0f019-3560-4597-b24a-59345c7b152a"),
        CommandStyle(Rhino.Commands.Style.ScriptRunner)
    ]
    public class Command3DHubs : Command
    {
        const string DefaultHelpUrl = "https://www.3dhubs.com";

        [DllImport("user32.dll")]
        public static extern bool EnableWindow(IntPtr hwnd, bool bEnable);

        private int progressAtLastReport;

        public Command3DHubs()
        {
            Instance = this;
        }

        public static Command3DHubs Instance { get; private set; }

        public override string EnglishName
        {
            get { return "3DHubs"; }
        }

        protected override void OnHelp()
        {
            var plugIn = this.PlugIn as PlugIn3DHubs;
            string helpUrl;
            if (!plugIn.Configuration.Main.TryGetValue("HelpUrl", out helpUrl))
                helpUrl = DefaultHelpUrl;
            if (helpUrl.Length != 0)
                Process.Start(helpUrl);
        }

        Result PromptForSelection()
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select objects to upload to 3D Hubs");
            go.SubObjectSelect = false;
            go.GeometryFilter =
                ObjectType.Surface |
                ObjectType.PolysrfFilter |
                ObjectType.Mesh;
            // go.GeometryAttributeFilter =
            //     GeometryAttributeFilter.ClosedSurface |
            //     GeometryAttributeFilter.ClosedPolysrf |
            //     GeometryAttributeFilter.ClosedMesh;
            go.GetMultiple(1, 0);
            return go.CommandResult();
        }

        /* Handler for the uploader's state-changed event. Prints progress 
         * messages to the console. */
        private void ReportUploadProgress(object sender, EventArgs e)
        {
            const int ProgressReportThreshold = 10;

            var plugIn = this.PlugIn as PlugIn3DHubs;
            var uploader = plugIn.Uploader;

            string message = null;
            switch (uploader.Stage)
            {
                case UploadStage.Cancelling:
                    message = "Interrupting upload, please wait.";
                    break;
                case UploadStage.Exporting:
                    message = "Exporting model file. This may take a few moments.";
                    break;
                case UploadStage.Encoding:
                    message = "Preparing the model for upload.";
                    break;
                case UploadStage.Communicating:
                    message = "Communicating with the 3D Hubs server.";
                    break;
                case UploadStage.Uploading:
                    var progress = uploader.Progress;
                    if (progress - progressAtLastReport < ProgressReportThreshold)
                        break;
                    message = string.Format("Uploading model to 3D Hubs: {0}/{1}.",
                        Utilities.FormatDataSize(uploader.BytesUploaded),
                        Utilities.FormatDataSize(uploader.UploadSize));
                    progressAtLastReport = progress;
                    break;
                case UploadStage.PostUpload:
                    message = "Creating a 3D Hubs cart.";
                    break;
                case UploadStage.Complete:
                    var error = uploader.Error;
                    if (error != null && error.ErrorType != UploadErrorType.None)
                        message = error.FormatDescription();
                    else
                        message = string.Format("Success! View your model at {0}", uploader.CartUrl);
                    break;
            }

            if (message != null)
                RhinoApp.WriteLine(string.Format("3D Hubs: {0}", message));
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var plugIn = this.PlugIn as PlugIn3DHubs;
            var uploadForm = plugIn.UploadForm;

            if (!plugIn.Uploader.Reset())
                return Result.Nothing;

            /* Ask the user to select objects if required. */
            var selectionResult = PromptForSelection();
            if (selectionResult != Result.Success)
                return selectionResult;

            /* Start the export-upload process. */
            var uploader = plugIn.Uploader;
			uploader.Document = doc;

            /* In interactive mode, show the GUI and block until file transfer 
             * begins (at which time we are done interacting with the Rhino 
             * document). In scripted mode, don't show the GUI and block until 
             * the upload completes. */
            UploadStage blockUntil = UploadStage.Complete;
            IntPtr hwndMain = RhinoApp.MainApplicationWindow.Handle;
            bool interactive = mode == RunMode.Interactive;
            if (interactive)
            {
                if (!uploadForm.Visible)
                    uploadForm.Show(RhinoApp.MainWindow());
                EnableWindow(hwndMain, false);
                blockUntil = UploadStage.Communicating;
            } else {
                progressAtLastReport = -100;
                uploader.StateChanged += ReportUploadProgress;
            }
            uploader.Start();
            uploader.BlockUntilStage(blockUntil, true);
            if (interactive)
                EnableWindow(hwndMain, true);
            else
                uploader.StateChanged -= ReportUploadProgress;
            
            return Result.Success;
        }
    }
}
