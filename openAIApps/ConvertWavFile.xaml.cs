using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using whisper.AudioTools;

namespace openAIApps
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ConvertWavFile : Window
    {
        AudioTools rec = new AudioTools();
        //initial directory for audio-files
        string savepath_snds = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "openapi\\snds");
        bool Selected_wavfile = false;
        bool Selected_file = false;
        public ConvertWavFile()
        {
            InitializeComponent();
            if (Selected_wavfile == true && Selected_file == true)
                btnConvertWav.IsEnabled = true;
        }
        /// <summary>
        /// use the AudioTools class to ssave filenames and such. We need it for the convert to mp3 -function
        /// </summary>
        /// <param name="initial_directory"></param>
        /// <returns></returns>
        public void OpenWavFile(string initial_directory)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Sound Files(*.wav;)|*.wav;";
            ofd.FilterIndex = 1;
            ofd.Multiselect = false;
            ofd.InitialDirectory = initial_directory;
            ofd.Title = "Select file To Convert";

            if (ofd.ShowDialog() == true)
            {
                FileInfo file = new FileInfo(ofd.FileName);
                if (file.Length == 0)
                {
                    MessageBox.Show("This file has zero length. Choose another", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                //return full path and filename
                rec.filename_full = ofd.FileName;
                rec.filename = ofd.SafeFileName;
                rec.SetMetaData(rec.InMetadata, rec.filename_full);
            }
        }
        private void btnOpenWavFile_Click(object sender, RoutedEventArgs e)
        {
            OpenWavFile(savepath_snds);
            if (string.IsNullOrEmpty(rec.filename_full))
            {
                MessageBox.Show("You must select a file", "No file was selected", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            else
            {
                lblSelectedFile.Content = rec.filename_full;
                txtbInFileInfo.Text = "\nDuration: " + rec.InMetadata.Duration + "\nFormat: " + rec.InMetadata.AudioData.Format + "\nSamplerate: " + rec.InMetadata.AudioData.SampleRate + "\nBitrate: " + rec.InMetadata.AudioData.BitRateKbs;
            }
            Selected_wavfile = true;
        }

        private void btnSaveMediaFile_Click(object sender, RoutedEventArgs e)
        {
            var extension = cmbFileExtension.Text;
            string temp = rec.SaveToFile(savepath_snds, extension);

            if (!string.IsNullOrEmpty(temp))
            {
                Selected_file = true;
                lblSaveAs.Content = temp;
            }
        }

        private void btnConvertWav_Click(object sender, RoutedEventArgs e)
        {
            if (Selected_wavfile == false || Selected_file == false)
            {
                string wav = null, mp3 = null;
                if (Selected_wavfile == false)
                {
                    wav = "Wav-file missing";
                }
                if (Selected_file == false)
                    mp3 = "mp3-file missing";
                MessageBox.Show(wav + "; " + mp3, "Missing files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            else
            {
                if (rec.ConvertWavFile(lblSelectedFile.Content.ToString(), lblSaveAs.Content.ToString()) == true)
                //   if(rec.ConvertWav(lblSelectedFile.Content.ToString(), lblSaveAsMP3.Content.ToString()) == true) 
                {
                    string file = lblSaveAs.Content.ToString();
                    FileInfo fi = new FileInfo(file);
                    txtbFileInfo.Text = "Success \nName: " + fi.Name + ", \nSize: " + fi.Length + " bytes" + "\nDuration: " + rec.OutMetadata.Duration + "\nFormat: " + rec.OutMetadata.AudioData.Format;
                }
            }

        }

        private void cmbFileExtension_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
