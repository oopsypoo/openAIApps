using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//speechsynthesis.cs
using whisper;
using static openAIApps.VideoClient;

namespace openAIApps
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// My former app OAiGPT3 was kind of messy and alot has changed since from openai.com
    /// Seems like alot of the models are allready "out-of-date" and focus is going to be GPT3.5
    /// So I'm going to rearrange and start adding apps in "tabs" instead. Main focus will be on model
    /// gpt3.5-turbo and DALL-E
    /// </summary>
    public partial class MainWindow : Window
    {
        const string url_openai_models = "https://api.openai.com/v1/models";

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
        

        private void EnsureSavePaths()
        {
            savepath_logs = Path.Combine(appRoot, "logs");
            savepath_snds = Path.Combine(appRoot, "snds");
            Directory.CreateDirectory(savepath_logs);
            Directory.CreateDirectory(savepath_snds);
            logfile = Path.Combine(savepath_logs, "logfile.txt");
        }

        /// <summary>
        /// not sure if this is the best solution. Using namespace and reorganizing data is the thing. But this will work.
        /// </summary>
        //static Dalle rxDalle = new Dalle();

        //oes not do much except to save all written entries to a log file. See in the beginning of
        //private async void btn_oai_rx_send_clickAsync(object sender, RoutedEventArgs e)
        //{
        public void SaveToLogFile(string txtRequest_Text)
        {
            string logEntry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": " + txtRequest_Text;

            using (StreamWriter sw = File.AppendText(logfile))
            {
                sw.WriteLine(logEntry);
            }
        }
        // In InitControls(), after creating _responsesClient:


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

        /// 
        /// available models...hmmm..do later next two functions are meant to fill and handle ComboBox cmbAvailableModels
        private void cmbAvailableModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}

















