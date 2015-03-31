using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace _3dhubs
{
    [System.Runtime.InteropServices.Guid("013FDADE-0197-4753-9344-A8BDD9D55FE1")]
    public partial class UploadForm : Form
    {
        private Configuration config;
        private Uploader uploader;
        private ButtonAction buttonAction;

        private enum ButtonAction
        {
            Close,
            Cancel,
            Retry,
            OpenLink
        }

        public UploadForm(Configuration config, Uploader uploader)
        {
            InitializeComponent();
            this.config = config;
            this.uploader = uploader;
            uploader.StateChanged += UploaderStateChanged;
            uploader.UserConfirmationRequired += UserConfirmationRequired;
        }

        public static Guid PanelId
        {
            get { return typeof(UploadForm).GUID; }
        }

        public void UpdateControlsAsync()
        {
            if (InvokeRequired)
                Invoke(new Action(() => { UpdateControls(); }));
            else
                UpdateControls();
        }

        public void UpdateControls()
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = uploader.Progress;
            actionButton.Enabled = uploader.Stage != UploadStage.Cancelling;
            buttonAction = ButtonAction.Cancel;

            switch (uploader.Stage)
            {
                case UploadStage.Idle:
                    title.Text = "Initializing";
                    status.Text = "Please wait.";
                    break;
                case UploadStage.Cancelling:
                    title.Text = "Cancelling";
                    status.Text = "Interrupting upload, please wait.";
                    break;
                case UploadStage.Exporting:
                    title.Text = "Exporting";
                    status.Text = "Exporting model file. This may take a few moments.";
                    progressBar.Style = ProgressBarStyle.Marquee;
                    break;
                case UploadStage.Encoding:
                    title.Text = "Encoding";
                    status.Text = "Preparing the model for upload.";
                    progressBar.Style = ProgressBarStyle.Marquee;
                    break;
                case UploadStage.Communicating:
                    title.Text = "Communicating";
                    status.Text = "Communicating with the 3D Hubs server.";
                    progressBar.Style = ProgressBarStyle.Marquee;
                    break;
                case UploadStage.Uploading:
                    title.Text = "Uploading";
                    status.Text = string.Format("Uploading model to 3D Hubs: {0}/{1}.", 
                        Utilities.FormatDataSize(uploader.BytesUploaded), 
                        Utilities.FormatDataSize(uploader.UploadSize));
                    break;
                case UploadStage.PostUpload:
                    title.Text = "Creating Cart";
                    status.Text = "Creating a 3D Hubs cart.";
                    break;
                case UploadStage.Complete:
                    buttonAction = ButtonAction.Close;
                    var error = uploader.Error;
                    if (error != null && error.ErrorType != UploadErrorType.None)
                    {
                        progressBar.Value = 0;
                        UpdateStatusFromError(error);
                    }
                    else
                    {
                        title.Text = "Success!";
                        status.Text = "Click \"View\" to see the model on 3D Hubs.";
                        buttonAction = ButtonAction.OpenLink;
                        progressBar.Value = 100;
                    }
                    break;
            }

            UpdateActionButtonCaption();
        }

        private void UpdateActionButtonCaption()
        {
            switch (buttonAction)
            {
                case ButtonAction.Cancel:
                    actionButton.Text = "&Cancel";
                    break;
                case ButtonAction.Close:
                    actionButton.Text = "&Close";
                    break;
                case ButtonAction.Retry:
                    actionButton.Text = "&Retry";
                    break;
                case ButtonAction.OpenLink:
                    actionButton.Text = "&View";
                    break;
            }
        }

        /* Updates text controls based on the type and content of an uploader error. */
        private void UpdateStatusFromError(UploadException e)
        {
            status.Text = e.FormatDescription();
            switch (e.ErrorType)
            {
                case UploadErrorType.Cancelled:
                    title.Text = "Cancelled";
                    break;
                case UploadErrorType.ConnectionFailure:
                    title.Text = "Connection Failure";
                    buttonAction = ButtonAction.Retry;
                    break;
                case UploadErrorType.UnsupportedUnitSystem:
                    title.Text = "Unsupported Unit System";
                    buttonAction = ButtonAction.Retry;
                    break;
                case UploadErrorType.PhysicalSizeInvalid:
                    title.Text = "Too Small";
                    buttonAction = ButtonAction.Retry;
                    break;
                case UploadErrorType.ExportFailure:
                    title.Text = "Export Failure";
                    buttonAction = ButtonAction.Retry;
                    break;
                case UploadErrorType.NothingSelected:
                    title.Text = "Nothing Selected";
                    buttonAction = ButtonAction.Retry;
                    break;
                case UploadErrorType.NetworkTimeout:
                    title.Text = "Network Error";
                    buttonAction = ButtonAction.Retry;
                    break;
                case UploadErrorType.Api:
                case UploadErrorType.Unexpected:
                    title.Text = "Upload Failed";
                    break;
            }
        }

        private void UploaderStateChanged(object sender, EventArgs e)
        {
            UpdateControlsAsync();
        }

        private void ActionButtonClick(object sender, EventArgs e)
        {
            switch (buttonAction)
            {
                case ButtonAction.Cancel:
                    uploader.Cancel();
                    break;
                case ButtonAction.Close:
                    Close();
                    break;
                case ButtonAction.Retry:
                    uploader.Start();
                    break;
                case ButtonAction.OpenLink:
                    if (uploader.CartUrl != null)
                        System.Diagnostics.Process.Start(uploader.CartUrl.ToString());
                    break;
            }
        }

        private void UploadFormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private delegate bool ConfirmationDelegate();

        private bool ShowConfirmation(string title, string message)
        {
            var result = MessageBox.Show(this, message, title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return result == DialogResult.Yes;
        }

        private bool UserConfirmationRequired(Uploader uploader, string title, string message)
        {
            if (InvokeRequired)
                return (bool)Invoke(new ConfirmationDelegate(() => ShowConfirmation(title, message)));
            else
                return ShowConfirmation(title, message);
        }

        private void StatusTextEnter(object sender, EventArgs e)
        {
            ActiveControl = actionButton;
        }
    }
}
