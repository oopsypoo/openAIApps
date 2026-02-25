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

        /* private void BrowseAppRoot_Click(object sender, RoutedEventArgs e)
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
         }*/
        private void BrowseAppRoot_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Application Root Directory",
                InitialDirectory = Directory.Exists(AppRootTextBox.Text) ? AppRootTextBox.Text : string.Empty
            };

            if (dialog.ShowDialog() == true)
            {
                string selectedPath = dialog.FolderName;

                if (HasWritePermission(selectedPath))
                {
                    AppRootTextBox.Text = selectedPath;
                    UpdateSubPathsFromAppRoot();
                }
                else
                {
                    MessageBox.Show("You don't have permission to write to this folder. Please choose a different location (like your Documents folder).",
                                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private bool HasWritePermission(string folderPath)
        {
            try
            {
                // Generate a random temp file name
                string tempFilePath = Path.Combine(folderPath, Path.GetRandomFileName());

                // Try to create and immediately delete it
                using (FileStream fs = File.Create(tempFilePath)) { }
                File.Delete(tempFilePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void BrowseLogs_Click(object sender, RoutedEventArgs e) => BrowseFolder(LogsTextBox);
        private void BrowseSounds_Click(object sender, RoutedEventArgs e) => BrowseFolder(SoundsTextBox);
        private void BrowseImages_Click(object sender, RoutedEventArgs e) => BrowseFolder(ImagesTextBox);
        private void BrowseVideos_Click(object sender, RoutedEventArgs e) => BrowseFolder(VideosTextBox);
        /// <summary>
        /// Opens a folder browser dialog to select a folder for the given TextBox. It checks if the application has write permission to the selected folder before updating the TextBox.
        /// </summary>
        /// <param name="textBox"></param>
        private void BrowseFolder(System.Windows.Controls.TextBox textBox)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select the save location",
                InitialDirectory = Directory.Exists(textBox.Text) ? textBox.Text : string.Empty
            };

            if (dialog.ShowDialog() == true)
            {
                string folder = dialog.FolderName;

                if (HasWritePermission(folder))
                {
                    textBox.Text = folder;
                }
                else
                {
                    MessageBox.Show("The application does not have permission to write to this folder. Please select a different location.",
                                    "Permission Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void UpdateSubPathsFromAppRoot()
        {
            var appRoot = AppRootTextBox.Text;

            // If the user clears the Root, we shouldn't try to build subpaths
            if (string.IsNullOrWhiteSpace(appRoot))
                return;

            // We just update the UI text boxes. 
            // Your EnsureSavePaths() will handle the actual directory creation later.
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
