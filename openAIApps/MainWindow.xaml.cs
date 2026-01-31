using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//speechsynthesis.cs
using static openAIApps.VideoClient;

namespace openAIApps
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml. Code for different tabs are in their respective files.
    /// MainWindow.Responses.xaml.cs, MainWindow.Video.cs, MainWindow.Whisper.cs
    /// menu-items are still here, with some menu-'actions'
    /// </summary>
    public partial class MainWindow : Window
    {
        const string url_openai_models = "https://api.openai.com/v1/models";
        /// <summary>
        /// Represents the OpenAI API key retrieved from the environment variable named "OPENAI_API_KEY".
        /// </summary>
        /// <remarks>This value is typically used to authenticate requests to the OpenAI API. Ensure that
        /// the environment variable is set before accessing this field to avoid authentication failures.</remarks>
        readonly string OpenAPIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        /// <summary>
        /// make these paths more general-purpose in the future
        /// </summary>
        //all logs are stored here
        private string appRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "openapi");
        private string savepath_logs;
        private string savepath_snds;
        private string logfile;
        public static HttpResponseMessage GlobalhttpResponse = new HttpResponseMessage();
        private VideoClient _videoClient;
        //private List<VideoListItem> _videoHistory = new();
        private ObservableCollection<VideoListItem> _videoHistory = new ObservableCollection<VideoListItem>();
        // Near other fields
        private string _responsesImagePath = string.Empty;

        private string _videoReferencePath = string.Empty;
        // near other fields
        private Responses _responsesClient;

        private readonly HttpClient _httpClient = new(); // Or inject as singleton

        private void EnsureSavePaths()
        {
            savepath_logs = Path.Combine(appRoot, "logs");
            savepath_snds = Path.Combine(appRoot, "snds");
            Directory.CreateDirectory(savepath_logs);
            Directory.CreateDirectory(savepath_snds);
            logfile = Path.Combine(savepath_logs, "logfile.txt");
        }



        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitResponsesControls(); // Move ALL combo population here
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
        public MainWindow()
        {
            InitializeComponent();
            InitControls();
            Loaded += MainWindow_Loaded;

        }

        public void InitControls()
        {
            EnsureSavePaths();
            _videoClient = new VideoClient(apiKey: OpenAPIKey);
            InitVideoList();
        }
        private void menuHelp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void menuAbout_Click(object sender, RoutedEventArgs e)
        {
            About about = new About();
            about.ShowDialog();
        }
        private void menuThisAssistant_Click(object sender, RoutedEventArgs e)
        {
            rassistant ra = new rassistant();
            ra.ShowDialog();
        }
        private void menuFile_Click(object sender, RoutedEventArgs e)
        {

        }

        private void menuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        public static string ExtractFileName(string path)
        {
            if (!String.IsNullOrEmpty(path))
            {
                int index = path.LastIndexOf('\\');
                if (index == -1)
                {
                    index = path.LastIndexOf('/');
                }
                return path.Substring(index + 1);
            }
            return null;
        }
        public static ImageSource GetImageSource(string filePath)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            return bitmap;
        }


        private void menuRecord_Click(object sender, RoutedEventArgs e)
        {
            RecordingTool rt = new RecordingTool();
            rt.Show();
            //rt.Owner = null;
        }

        private void menuConvert_Click(object sender, RoutedEventArgs e)
        {
            ConvertWavFile cvf = new ConvertWavFile();
            cvf.Show();
        }

        private void menuPlayFile_Click(object sender, RoutedEventArgs e)
        {
            AudioPlayer ap = new AudioPlayer();
            ap.Show();
        }


        private void menuSpeechSynthesisTool_Click(object sender, RoutedEventArgs e)
        {
            SpeechSynthesisTool speechSynthesisTool = new SpeechSynthesisTool();
            speechSynthesisTool.ShowDialog();
        }


        private async void menuAvailableModels_Click(object sender, RoutedEventArgs e)
        {
            // pass what the dialog needs: HttpClient and API key
            var availableModels = new AvailableModels(_httpClient, OpenAPIKey);

            try
            {
                // fetch models BEFORE showing dialog (or inside dialog, see below)
                var models = await availableModels.GetAvailableModelsAsync(_httpClient, OpenAPIKey, url_openai_models);
                availableModels.UpdateAvailableModels(models);

                // now show the populated dialog
                availableModels.ShowDialog();
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"HTTP error: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}");
            }
        }

    }
}


