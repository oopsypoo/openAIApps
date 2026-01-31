using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using whisper;

namespace openAIApps
{
    public partial class MainWindow
    {
        private void cmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (cmbLanguage.SelectedIndex)
            {
                case 0:
                    Whisper.WOptions.STT_language = "en";
                    break;
                case 1:
                    Whisper.WOptions.STT_language = "nb";
                    break;
                case 2:
                    Whisper.WOptions.STT_language = "tl";
                    break;
            }
            Whisper.RxWhisper.language = Whisper.WOptions.STT_language;
        }

        private void cmbSpeachToText_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Whisper.WOptions.STT_Type = Whisper.WhisperEndpoints[cmbSpeachToText.SelectedIndex];
        }

        private async void btnWhisperSendRequest_Click(object sender, RoutedEventArgs e)
        {
            txtWhisperResponse.Text = string.Empty;
            IsEnabled = false;

            try
            {
                GlobalhttpResponse = await Whisper.RxWhisper.PostFile(OpenAPIKey);
                var responseString = await GlobalhttpResponse.Content.ReadAsStringAsync();

                // Try parse into ResponseWhisper; if it fails or text is null, show raw body
                Whisper.ResWhisper = null;
                try
                {
                    Whisper.ResWhisper =
                        JsonSerializer.Deserialize<Whisper.ResponseWhisper>(responseString);
                }
                catch
                {
                    // ignore, will fall back to raw body
                }

                if (!GlobalhttpResponse.IsSuccessStatusCode || Whisper.ResWhisper?.text == null)
                {
                    txtWhisperResponse.Text =
                        "Server-response:\n" + GlobalhttpResponse +
                        "\n\nBody:\n" + responseString;
                }
                else
                {
                    txtWhisperResponse.Text = Whisper.ResWhisper.text;
                }
            }
            catch (Exception err)
            {
                txtWhisperResponse.Text =
                    err.Message + "\nInnerexception: " + err.InnerException;
            }
            finally
            {
                IsEnabled = true;
            }
        }

        /// <summary>
        /// Opens an audio file for transcription or translation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAudioOpenFile_Click(object sender, RoutedEventArgs e)
        {
            Whisper.full_audiofilename = Whisper.WOptions.STT_audiofile = Whisper.GetAudioFileName(savepath_snds);
            lblSelectedAudioFile.Content = Whisper.full_audiofilename;
        }
    }
}
