using System;
using System.Windows;

namespace openAIApps
{
    public partial class VideoPreviewWindow : Window
    {
        public VideoPreviewWindow(string videoPath)
        {
            InitializeComponent();
            mediaPlayer.Source = new Uri(videoPath);
            mediaPlayer.Play();
        }

        private void mediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Position = TimeSpan.Zero;
            mediaPlayer.Play(); // loop playback
        }
    }
}
