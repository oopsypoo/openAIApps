using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using whisper.AudioTools;

namespace openAIApps
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    /// //initial directory for audio-files
    
    public partial class RecordingTool : Window
    {

        string savepath_snds = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "openapi\\snds");
        public static AudioTools AuxRecord = new AudioTools();
        public RecordingTool()
        {
            InitializeComponent();
        }
       
        /// <summary>
        /// Start recording to file(wav) for the moment
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAudioStartRecording_Click(object sender, RoutedEventArgs e)
        {
            string temp = AuxRecord.filename_full;
            if (!string.IsNullOrEmpty(temp))
            {
                btnAudioStopRecording.IsEnabled = true;
                btnAudioStartRecording.IsEnabled = false;
                AuxRecord.StartRecording(temp);
            }
            else
                MessageBox.Show("You must create a file to save to", "Whisper.AuxRecord.filename_full", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        /// <summary>
        /// stop the recording
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="e"></param>
        private void btnAudioStopRecording_Click(object obj, RoutedEventArgs e)
        {
            btnAudioStartRecording.IsEnabled = true;
            btnAudioStopRecording.IsEnabled = false;
            AuxRecord.StopRecording();

        }
        private void btnSaveToFile_Click(object sender, RoutedEventArgs e)
        {
            AuxRecord.SaveToFile(savepath_snds, "wav");
            lblNewAudioFile.Content = AuxRecord.filename_full;
        }
    }
}
