using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Linq;
using System.Security.Cryptography;

using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.DocObjects;

using OAuth;

namespace _3dhubs
{
    public enum UploadStage
    {
        Idle,
        Exporting,
        Encoding,
        Communicating,
        Uploading,
        PostUpload,
        Cancelling,
        Complete
    }

    public enum UploadErrorType
    {
        None,
        Unexpected,
        Cancelled,
        UnsupportedUnitSystem,
        PhysicalSizeInvalid,
        ExportFailure,
        NothingSelected,
        ConnectionFailure,
        NetworkTimeout,
        Api
    }

    public class UploadException : Exception
    {
        public UploadErrorType ErrorType { get; private set; }

        public UploadException(UploadErrorType type, string message = null)
            : base(message)
        {
            ErrorType = type;
        }

        public string FormatDescription()
        {
            switch (ErrorType)
            {
                case UploadErrorType.None:
                    return "Success.";
                case UploadErrorType.Cancelled:
                    return "Operation cancelled.";
                case UploadErrorType.ConnectionFailure:
                    return "Unable to connect to the 3D Hubs server.";
                case UploadErrorType.UnsupportedUnitSystem:
                    return "3D Hubs requires models measured in millimeters. Please change the unit system and click \"Retry\".";
                case UploadErrorType.PhysicalSizeInvalid:
                    return "3D Hubs requires that models measure at least 3mm on each axis.";
                case UploadErrorType.ExportFailure:
                    return "Unable to export model.";
                case UploadErrorType.NothingSelected:
                    return "Please select objects to export and click \"Retry\".";
                case UploadErrorType.NetworkTimeout:
                    return "Upload failed due to a network error.";
                case UploadErrorType.Api:
                case UploadErrorType.Unexpected:
                    var message = "Sorry, there was a problem with the upload.";
                    if (Message != null && Message.Length != 0)
                        message += string.Format(" Diagnostic: \"{0}\".", Message);
                    return message;
            }
            return "Unexpected error.";
        }
    }

    public delegate bool UserConfirmationHandler(Uploader sender, string title, string message);

    public class Uploader
    {
        readonly Uri ApiBaseUrl = new Uri("http://www.3dhubs.com/api/v1/");

        const string DefaultConsumerKey = "";
        const string DefaultConsumerSecret = "";
        const int DefaultMaxFileSize = 0x1000000;
        const int DefaultMinModelSize = 3;

        const string ExportCommandPattern = "-_Export \"{0}\" {1} _Enter _DetailedOptions {2} _AdvancedOptions {3} _Enter _Enter";

        private Thread workThread;
        private volatile bool interrupt;
        private Configuration config;

        public RhinoDoc Document { get; set; }

        public UploadStage Stage { get; private set; }
        public UploadException Error { get; private set; }
        public int Progress { get; private set; }
        public long FileSize { get; private set; }
        public long UploadSize { get; private set; }
        public long BytesUploaded { get; private set; }
        public Uri CartUrl { get; private set; }
        public string UploadedModelId { get; private set; }

        public event EventHandler StateChanged;
        public event UserConfirmationHandler UserConfirmationRequired;

        private readonly KeyValuePair<string, string>[] BasicOptions = {
            new KeyValuePair<string, string>("ExportFileAs", "Binary"),
            new KeyValuePair<string, string>("ExportUnfinishedObjects", "Yes"),
            new KeyValuePair<string, string>("UseSimpleDialog", "No"),
            new KeyValuePair<string, string>("UseSimpleParameters", "No")
        };

        private readonly KeyValuePair<string, string>[] DetailedOptions = {
            new KeyValuePair<string, string>("JaggedSeams", "No"),
            new KeyValuePair<string, string>("PackTextures", "No"),
            new KeyValuePair<string, string>("Refine", "Yes"),
            new KeyValuePair<string, string>("SimplePlane", "No")
        };

        private readonly KeyValuePair<string, string>[] AdvancedOptions = {
            new KeyValuePair<string, string>("Angle", "15"),
            new KeyValuePair<string, string>("AspectRatio", "0"),
            new KeyValuePair<string, string>("Distance", "0.01"),
            new KeyValuePair<string, string>("Grid", "16"),
            new KeyValuePair<string, string>("MaxEdgeLength", "0"),
            new KeyValuePair<string, string>("MinEdgeLength", "0.0001")
        };

        public Uploader(Configuration config)
        {
            this.config = config;
        }

        /* Returns "_" if the supplied string is a Rhinoceros command
         * identifier. The prefix disables localized identifier lookup. */
        private static string IdentifierPrefix(string s)
        {
            return s.Length != 0 && Char.IsLetter(s[0]) ? "_" : "";
        }

        /* Builds an argument string for a Rhinoceros command consisting of
         * a key-value pair for each item in 'items'. Values from 'config' are
         * used if present. */
        private string MakeArgumentString(KeyValuePair<string, string>[] items,
            Dictionary<string, string> config)
        {
            var result = new StringBuilder();
            foreach (var item in items)
            {
                string value;
                if (!config.TryGetValue(item.Key, out value))
                    value = item.Value;
                if (result.Length != 0)
                    result.Append(' ');
                result.AppendFormat("{0}{1}={2}{3}",
                    IdentifierPrefix(item.Key), item.Key,
                    IdentifierPrefix(item.Value), value);
            }
            return result.ToString();
        }

        private void CheckUnitSystem()
        {
            if (Document.ModelUnitSystem != UnitSystem.Millimeters)
                throw new UploadException(UploadErrorType.UnsupportedUnitSystem);
        }

        private void CheckPhysicalSize()
        {
            /* Compute the world space AABB of the selected geometry. */
            var selected = Document.Objects.GetSelectedObjects(false, false);
            var haveSelection = false;
            BoundingBox bounds = new BoundingBox();
            foreach (var obj in selected)
            {
                haveSelection = true;
                var geometry = obj.Geometry;
                if (geometry == null)
                    continue;
                var objectBounds = geometry.GetBoundingBox(true);
                bounds.Union(objectBounds);
            }

            /* Make sure there's something to export. */
            if (!haveSelection)
                throw new UploadException(UploadErrorType.NothingSelected);

            /* Ensure that the AABB measures at least 3mm on each axis. */
            int minimumSizeMM = config.GetInteger("MinModelSize", DefaultMinModelSize);
            Vector3d dimensions = bounds.Diagonal;
            if (dimensions.X < minimumSizeMM ||
                dimensions.Y < minimumSizeMM ||
                dimensions.Z < minimumSizeMM)
                throw new UploadException(UploadErrorType.PhysicalSizeInvalid);
        }

        private string ExportStl()
        {
            string filePath = Utilities.BuildTempFilePath("stl");
            var macro = string.Format(ExportCommandPattern, filePath,
                MakeArgumentString(BasicOptions, config.Export),
                MakeArgumentString(DetailedOptions, config.Export),
                MakeArgumentString(AdvancedOptions, config.Export));
            try
            {
                Utilities.RunMacroOnMainThread(macro);
            }
            catch (Exception e)
            {
                throw new UploadException(UploadErrorType.ExportFailure,
                    string.Format("Export failed: {0}", e.Message));
            }
            return filePath;
        }

        private void NotifyStateChanged()
        {
            if (StateChanged != null)
                StateChanged(this, EventArgs.Empty);
        }

        private void SetStage(UploadStage stage)
        {
            if (interrupt)
                throw new UploadException(UploadErrorType.Cancelled);
            this.Stage = stage;
            NotifyStateChanged();
        }

        private void SetProgress(int progress)
        {
            if (interrupt)
                throw new UploadException(UploadErrorType.Cancelled);
            if (progress != this.Progress)
            {
                this.Progress = progress;
                NotifyStateChanged();
            }
        }

        private void SetError(UploadException error)
        {
            if (this.Error == null) // The first error takes precedence.
                this.Error = error;
            this.Stage = UploadStage.Complete;
            this.Progress = 0;
            NotifyStateChanged();
        }

        private bool DoUserConfirmation(string title, string message)
        {
            if (UserConfirmationRequired == null)
                return false;
            return UserConfirmationRequired(this, title, message);
        }

        private void CheckFileSize(string filePath)
        {
            int maxSize = config.GetInteger("MaxFileSize", DefaultMaxFileSize);
            var info = new FileInfo(filePath);
            FileSize = info.Length;
            if (info.Length <= maxSize)
                return;
            string message = string.Format(
                "This model is {0}, which exceeds the 3D Hubs maximum recommended size of {1}. Upload anyway?",
                Utilities.FormatDataSize(info.Length),
                Utilities.FormatDataSize(maxSize));
            string title = "Upload Large File?";
            if (!DoUserConfirmation(title, message))
                throw new UploadException(UploadErrorType.Cancelled);
        }

        /* Creates an OAuth request signer with credentials from the configuration file. */
        private RequestSigner CreateSigner()
        {
            string consumerKey, consumerSecret;
            if (!config.Main.TryGetValue("ConsumerKey", out consumerKey))
                consumerKey = DefaultConsumerKey;
            if (!config.Main.TryGetValue("ConsumerSecret", out consumerSecret))
                consumerSecret = DefaultConsumerSecret;
            return new RequestSigner(consumerKey, consumerSecret);
        }

        /* Attempts to upload the exported model file and, if successful, create
         * a cart from the ID of the uploaded model. */
        private void Upload(string filePath)
        {
            var signer = CreateSigner();

            /* Upload the model. */
            var modelRequest = DoModelRequest(signer, filePath);
            using (var response = modelRequest.GetResponse())
                HandleModelResponse(response);

            /* Create a cart from the uploaded model ID. */
            var cartRequest = DoCartRequest(signer);
            using (var response = cartRequest.GetResponse())
                HandleCartResponse(response);
        }

        /* Attempts to delete the exported model file. */
        private void DeleteTemporaryFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException)
            {
            }
        }

        /* Attempts to make a meaningful name for the uploaded file from the document title. */
        private string MakeUploadFileName()
        {
            var stem = Document.Name.Length != 0 ? Document.Name : "untitled";
            return Path.ChangeExtension(stem, ".stl");
        }

        /* Uploads the model to the /model endpoint. */
        private WebRequest DoModelRequest(RequestSigner signer, string filePath)
        {
            SetStage(UploadStage.Encoding);

            using (FileStream fs = File.OpenRead(filePath))
            {
                var fields = new Dictionary<string, ParameterValue>
                {
                    { "file",     new ParameterValue(fs, true)             },
                    { "fileName", new ParameterValue(MakeUploadFileName()) }
                };

                QueryStringStream body;
                var request = signer.BuildSignedRequest(out body,
                    new Uri(ApiBaseUrl, "model"), fields, true);

                SetStage(UploadStage.Communicating);
                using (var stream = request.GetRequestStream())
                {
                    SetProgress(0);
                    SetStage(UploadStage.Uploading);
                    TransferBody(stream, body);
                }

                return request;
            }
        }

        /* Processes a response from the /upload endpoint, storing the returned
         * model ID. */
        private void HandleModelResponse(WebResponse response)
        {
            try {
                var data = Utilities.ParseJsonResponse(response);
                if (data["result"] != "success")
                    throw new UploadException(UploadErrorType.Api,
                        "Model upload unsuccessful.");
                UploadedModelId = data["modelId"].ToString();
            } catch (UploadException) {
                throw;
            } catch (Exception) {
                throw new UploadException(UploadErrorType.Api,
                    "Malformed response from /model.");
            }
        }

        /* Makes a request to the /cart endpoint to create a single-item cart
         * containing the uploaded model. */
        private WebRequest DoCartRequest(RequestSigner signer)
        {
            SetStage(UploadStage.Communicating);
            var fields = new Dictionary<string, ParameterValue> {
                { "items[0][modelId]",  new ParameterValue(UploadedModelId) },
                { "items[0][quantity]", new ParameterValue("1")             }
            };
            QueryStringStream body;
            var request = signer.BuildSignedRequest(out body,
                new Uri(ApiBaseUrl, "cart"), fields, true);
            using (var stream = request.GetRequestStream())
                body.CopyTo(stream);
            return request;
        }

        /* Processes a JSON response from the /cart endpoint. */
        private void HandleCartResponse(WebResponse response)
        {
            try
            {
                var data = Utilities.ParseJsonResponse(response);
                if (data["result"] != "success")
                    throw new UploadException(UploadErrorType.Api,
                        "Cart creation unsuccessful.");
                CartUrl = new Uri(data["url"]);
            }
            catch (UploadException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new UploadException(UploadErrorType.Api,
                    "Malformed response from /cart.");
            }
        }

        /* Transfers a request body in chunks, emitting progress notifications. */
        private int TransferBody(Stream outputStream, QueryStringStream bodyStream)
        {
            const int BlockSize = 0x1000;

            UploadSize = bodyStream.ComputeLength();

            bodyStream.Reset();
            int bytesWritten = 0;
            byte[] block = new byte[BlockSize];
            for (;;)
            {
                /* Update progress, rounding to the nearest percentage point. */
                BytesUploaded = bytesWritten;
                int progress = UploadSize != 0 ? Math.Min(100, (100 * bytesWritten + 50) / (int)UploadSize) : 100;
                SetProgress(progress);

                /* Write a block. */
                int bytesRead = bodyStream.Read(block, 0, BlockSize);
                if (bytesRead == 0)
                    break;
                outputStream.Write(block, 0, bytesRead);

                bytesWritten += bytesRead;
            }

            BytesUploaded = bytesWritten;
            SetProgress(100);
            return bytesWritten;
        }

        private void WorkThread()
        {
            try
            {
                CheckUnitSystem();
                CheckPhysicalSize();
                SetStage(UploadStage.Exporting);
                string filePath = ExportStl();
                SetStage(UploadStage.Communicating);
                CheckFileSize(filePath);
                Upload(filePath);
                DeleteTemporaryFile(filePath);
                SetStage(UploadStage.Complete);
            }
            catch (UploadException e)
            {
                SetError(e);
            }
            catch (Exception e)
            {
                var uploadError = new UploadException(UploadErrorType.Unexpected, e.Message);
                SetError(uploadError);
            }
        }

        private void StartWorkThread()
        {
            if (workThread != null)
            {
                interrupt = true;
                workThread.Join();
                workThread = null;
            }
            workThread = new Thread(WorkThread);
            interrupt = false;
            workThread.Start();
        }

        public bool Reset()
        {
            if (Busy())
                return false;
            Error = null;
            Stage = UploadStage.Idle;
            Progress = 0;
            UploadedModelId = null;
            CartUrl = null;
            NotifyStateChanged();
            return true;
        }

        public bool Start()
        {
            if (!Reset())
                return false;
            StartWorkThread();
            return true;
        }

        public void Cancel()
        {
            SetStage(UploadStage.Cancelling);
            this.Error = new UploadException(UploadErrorType.Cancelled);
            interrupt = true;
        }

        public bool Busy()
        {
            if (workThread != null && !workThread.IsAlive)
                workThread = null;
            return workThread != null;
        }

        /* Blocks the current thread until the uploader reaches the specified stage. */
        public void BlockUntilStage(UploadStage stage, bool interactive)
        {
            while (this.Stage < stage)
            {
                if (interactive)
                    Application.DoEvents();
                else
                    Thread.Sleep(50);
            }
        }
    }
}
