using System.Windows;

using System.IO;


namespace openAIApps
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; }

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            Settings = settings;
            DataContext = settings;
        }

        private void BrowseAppRoot_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                var folder = Path.GetDirectoryName(dialog.FileName)!;
                if (Directory.Exists(folder))
                {
                    AppRootTextBox.Text = folder; // updates Settings.AppRoot via binding
                    UpdateSubPathsFromAppRoot();
                }
            }
        }

        private void BrowseLogs_Click(object sender, RoutedEventArgs e) => BrowseFolder(LogsTextBox);
        private void BrowseSounds_Click(object sender, RoutedEventArgs e) => BrowseFolder(SoundsTextBox);
        private void BrowseImages_Click(object sender, RoutedEventArgs e) => BrowseFolder(ImagesTextBox);
        private void BrowseVideos_Click(object sender, RoutedEventArgs e) => BrowseFolder(VideosTextBox);

        private void BrowseFolder(System.Windows.Controls.TextBox textBox)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                var folder = Path.GetDirectoryName(dialog.FileName)!;
                if (Directory.Exists(folder))
                    textBox.Text = folder; // updates bound Settings property
            }
        }

        private void UpdateSubPathsFromAppRoot()
        {
            var appRoot = AppRootTextBox.Text;
            if (string.IsNullOrEmpty(appRoot))
                return;

            // Auto-fill default subfolders when AppRoot changes
            LogsTextBox.Text = Path.Combine(appRoot, "logs");
            SoundsTextBox.Text = Path.Combine(appRoot, "snds");
            ImagesTextBox.Text = Path.Combine(appRoot, "images");
            VideosTextBox.Text = Path.Combine(appRoot, "videos");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.SaveSettings(Settings);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

}
