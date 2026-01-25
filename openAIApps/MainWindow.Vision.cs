using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

/*
 * Code for Vision-tab-controls
*/
namespace openAIApps
{
    public partial class MainWindow
    {
        private void AddImageControls(object src)
        {
            if (src == btnMaskImage || src == cbImageEdit)
            {
                lblSelectedMask.Visibility = Visibility.Visible;
                imageMask.Visibility = Visibility.Visible;
                btnRemoveMask.Visibility = Visibility.Visible;
            }
            if (src == btnOpenImage || src == cbImageEdit)
            {
                lblSelectedImage.Visibility = Visibility.Visible;
                imageSelected.Visibility = Visibility.Visible;
                btnRemoveImage.Visibility = Visibility.Visible;
            }
        }
        private void imgVision_Initialized(object sender, EventArgs e)
        {

        }

        private async void btnVisionSendRequest_Click(object sender, RoutedEventArgs e)
        {

            Vision.InitChatVision(txtVisionRequest.Text);
            var responseString = await Vision.ResponseChat(OpenAPIKey);
            if (responseString == null)
            {
                txtVisionResponse.Text = "Server-response: \n" + GlobalhttpResponse + "\n\nError:\n" + responseString;
                this.IsEnabled = true;
                return;
            }
            else
            {
                //everything is ok. Get response
                txtVisionResponse.Text = responseString.Message.Role + "\n" + responseString.Message.Content;
            }

        }

        private void txtVisionRequest_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        private void cmbVisionImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void btnOpenVImage_Click(object sender, RoutedEventArgs e)
        {
            Vision.ImageFileName = Vision.GetImageFileName();
            var fileExtension = Path.GetExtension(Vision.ImageFileName);
            if (string.IsNullOrEmpty(Vision.ImageFileName))
            {
                MessageBox.Show("String cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            imgVision.Source = GetImageSource(Vision.ImageFileName);

            AddImageControls(sender);
        }
    }
}
