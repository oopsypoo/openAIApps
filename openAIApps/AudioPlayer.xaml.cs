using Microsoft.Win32;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using whisper.AudioTools;
using System.Media;

namespace openAIApps
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class AudioPlayer : Window
    {
        const string savepath_snds = "D:\\Users\\frode\\Documents\\openapi\\snds\\";
        AudioTools rec = new AudioTools();
        
    public AudioPlayer()
        {
            InitializeComponent();
            pbProgressPlay.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(pbProgressPlay_MouseDown), true);
            pbProgressPlay.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(pbProgressPlay_MouseUp), true);
        }
        public void OpenFile(string initial_directory)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            //ofd.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp; *.png)|*.jpg; *.jpeg; *.gif; *.bmp; *.png";
            ofd.Filter = "Sound Files(*.wav; *.mp3; *.aac; *.wma)|*.wav; *.mp3; *.aac; *.wma";
            //ofd.FilterIndex = 1;
            ofd.Multiselect = false;
            ofd.InitialDirectory = initial_directory;
            ofd.Title = "Select file To Play";

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
        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFile(savepath_snds);
            if(!string.IsNullOrEmpty(rec.filename_full)) 
            {
                lblCurrentFile.Content = rec.filename_full;
                btnPlayFile.IsEnabled = true;
                btnStopPlay.IsEnabled = true;
                txtbInPlayFileInfo.Text = "Duration: " + rec.InMetadata.Duration + "\nFormat: " + rec.InMetadata.AudioData.Format +
                    "\nChannelOutput: " + rec.InMetadata.AudioData.ChannelOutput + "\nSampleRate: " + rec.InMetadata.AudioData.SampleRate + "\nBitRateKbs: " + rec.InMetadata.AudioData.BitRateKbs;
                pbProgressPlay.Maximum = rec.InMetadata.Duration.TotalMilliseconds;
                
                //pbProgressPlay.Maximum = 100;
            }
        }

        private void btnPlayFile_Click(object sender, RoutedEventArgs e)
        {
            if(rec.filename_full == null)
            {
                MessageBox.Show("You must select a file to play", "Error", MessageBoxButton.OK, MessageBoxImage.Stop); 
                return;
            }
            rec.ProgressChanged += (sender, msIntoFile) =>
            {
                // Update progress bar control with progressPercent
                // progressBar is a placeholder for the actual name of your progress bar control
                pbProgressPlay.Value = (int)msIntoFile;
            };
            rec.PlayFile(rec.filename_full);
            btnPauseFile.IsEnabled = true;
        }

        private void btnStopPlay_Click(object sender, RoutedEventArgs e)
        {
            rec.StopPlaying();
            pbProgressPlay.Value = 0;
            btnPauseFile.Content = "Pause";
            btnPauseFile.IsEnabled = false;
        }
        

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            rec.StopPlaying();
            rec.Dispose();
        }


        private void btnPauseFile_Click(object sender, RoutedEventArgs e)
        {
            bool pause_state = false;
            rec.PausePlaying(ref pause_state);
            if(pause_state == true)
                btnPauseFile.Content = "Continue Playing";
            else
                btnPauseFile.Content = "Pause";
        }

        private void pbProgressPlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            rec.Pause();
        }


        private void pbProgressPlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            rec.Spool((int)pbProgressPlay.Value);
        }
    }
}
