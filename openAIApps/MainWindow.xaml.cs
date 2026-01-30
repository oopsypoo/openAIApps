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
        {//set standard/default values to image controls
            EnsureSavePaths();
            foreach (var p in Dalle.optImages.optImages)
                cmNumberOfImages.Items.Add(p);
            cmNumberOfImages.SelectedItem = Dalle.optImages.noImages;

            foreach (var p in Dalle.optImages.optSize)
                cmSize.Items.Add(p);
            cmSize.SelectedItem = Dalle.optImages.csize;

            foreach (var p in Dalle.optImages.optQuality)
                cmImageQuality.Items.Add(p);

            cmImageQuality.SelectedItem = Dalle.optImages.Quality;

            //Add the GPT-model-name to the tab. I've removed all references to GPT3.5Turbo

#if DALLE_VERSION3
            {
                cbImageVariations.IsEnabled = false;
                btnOpenImage.IsEnabled = false;
                cbImageEdit.IsEnabled = false;
                btnMaskImage.IsEnabled = false;
            }
            _videoClient = new VideoClient(apiKey: OpenAPIKey);
            InitVideoList();

#endif
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

        private void cmNumberOfImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dalle.optImages.noImages = (int)cmNumberOfImages.SelectedItem;
            Dalle.rxImagesEdit.n = Dalle.rxImages.n = Dalle.rxImagesVariation.n = Dalle.optImages.noImages;
        }

        private void cmSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dalle.optImages.csize = (string)cmSize.SelectedItem;

            Dalle.rxImagesEdit.size = Dalle.rxImages.size = Dalle.rxImagesVariation.size = Dalle.optImages.csize;
        }

        private async void btnDalleSendRequest_Click(object sender, RoutedEventArgs e)
        {
            txtDalleResponse.Text = "";
            this.IsEnabled = false;
            Dalle.rxImages.prompt = txtDalleRequest.Text;

            try
            {
                if (cbImageVariations.IsChecked == false && cbImageEdit.IsChecked == false)
                {
                    GlobalhttpResponse = await Dalle.rxImages.PostFile(OpenAPIKey);
                }
                else if (cbImageVariations.IsChecked == true && cbImageEdit.IsChecked == false)
                {
                    GlobalhttpResponse = await Dalle.rxImagesVariation.PostFile(OpenAPIKey);
                }
                else if (cbImageEdit.IsChecked == true)
                {
                    Dalle.rxImagesEdit.prompt = txtDalleRequest.Text;
                    GlobalhttpResponse = await Dalle.rxImagesEdit.PostFile(OpenAPIKey);
                }

                var responseString = await GlobalhttpResponse.Content.ReadAsStringAsync();

                Dalle.resource = JsonSerializer.Deserialize<Dalle.responseImage>(responseString);
                /// there's an error. just get out.

                if (Dalle.resource.data == null)
                {
                    txtDalleResponse.Text = "Server-response: \n" + GlobalhttpResponse + "\n\nError:\n" + responseString;
                    this.IsEnabled = true;
                    return;
                }
                else
                {
                    ///everything is ok. Draw/create the images
                    Dalle.resource.DrawImages();
                }
            }
            catch (Exception err)
            {
                this.Dispatcher.Invoke(() =>
                {
                    txtDalleResponse.Text = err.Message + "\nInnerexception: " + err.InnerException;
                    this.IsEnabled = true;
                    return;
                });
            }

            this.Dispatcher.Invoke(() =>
            {
                this.IsEnabled = true;
            });
        }

        private void cbImageVariations_Checked(object sender, RoutedEventArgs e)
        {
            btnOpenImage.IsEnabled = true;
            cbImageEdit.IsEnabled = false;
            //make image visible if something is selected
        }
        /// <summary>
        /// should happen when the variation-checkbox is unchecked. Make "things" hidden and reset image in rxImageVariations
        /// or, when edit is unchecked
        /// </summary>
        private void RemoveImageControls(object src)
        {
            lblSelectedImage.Visibility = Visibility.Hidden;
            imageSelected.Visibility = Visibility.Hidden;
            lblSelectedMask.Visibility = Visibility.Hidden;
            imageMask.Visibility = Visibility.Hidden;
            imageSelected.Visibility = Visibility.Hidden;
            btnRemoveMask.Visibility = Visibility.Hidden;
            btnRemoveImage.Visibility = Visibility.Hidden;
            imageSelected.Source = null;
            imageMask.Source = null;
            if (src == cbImageVariations)
                Dalle.rxImagesVariation.image = null;
            else if (src == cbImageEdit)
                Dalle.rxImagesEdit.image = null;

        }

        private void btnOpenImage_Click(object sender, RoutedEventArgs e)
        {
            string temp = Dalle.GetImageFileName(Dalle.GetSavepath_pics());
            if (string.IsNullOrEmpty(temp))
            {
                MessageBox.Show("String cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string filename = ExtractFileName(temp);

            if (cbImageVariations.IsChecked == true)
            {
                Dalle.rxImagesVariation.image = filename;
                imageSelected.Source = GetImageSource(temp);
            }
            else if (cbImageEdit.IsChecked == true)
            {
                Dalle.rxImagesEdit.image = filename;
                imageSelected.Source = GetImageSource(temp);
            }
            AddImageControls(sender);
        }

        private void cbImageVariations_Unchecked(object sender, RoutedEventArgs e)
        {
            btnOpenImage.IsEnabled = false;
            cbImageEdit.IsEnabled = true;
            RemoveImageControls(sender);
        }

        private void btnMaskImage_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = Dalle.GetImageFileName(Dalle.GetSavepath_pics());
            if (string.IsNullOrEmpty(fullpath))
            {
                MessageBox.Show("String cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Dalle.rxImagesEdit.mask = ExtractFileName(fullpath);
            imageMask.Source = GetImageSource(fullpath);
            AddImageControls(sender);
        }

        private void cbImageEdit_Unchecked(object sender, RoutedEventArgs e)
        {
            btnOpenImage.IsEnabled = false;
            btnMaskImage.IsEnabled = false;
            cbImageVariations.IsEnabled = true;
            //
            RemoveImageControls(sender);
        }

        private void cbImageEdit_Checked(object sender, RoutedEventArgs e)
        {
            btnOpenImage.IsEnabled = true;
            btnMaskImage.IsEnabled = true;
            cbImageVariations.IsEnabled = false;
            //

        }

        private void btnRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            imageSelected.Source = null;
            btnRemoveImage.Visibility = Visibility.Hidden;
            lblSelectedImage.Visibility = Visibility.Hidden;
            if (cbImageVariations.IsChecked == true)
            {
                Dalle.rxImagesVariation.image = null;
            }
            else
                Dalle.rxImagesEdit.image = null;
        }

        private void btnRemoveMask_Click(object sender, RoutedEventArgs e)
        {
            imageMask.Source = null;
            btnRemoveMask.Visibility = Visibility.Hidden;
            lblSelectedMask.Visibility = Visibility.Hidden;
            Dalle.rxImagesEdit.mask = null;
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

        private void cmbAvailableModels_Initialized(object sender, EventArgs e)
        {

        }

        //when aulity of an image is changed...value is changed
        private void cmImageQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dalle.optImages.Quality = (string)cmImageQuality.SelectedItem;

            Dalle.rxImages.quality = Dalle.optImages.Quality;
            //Dalle.rxImagesEdit.size = Dalle.rxImages.size = Dalle.rxImagesVariation.size = Dalle.optImages.csize;

        }

        private void menuCreateAssistant_Click(object sender, RoutedEventArgs e)
        {

        }


    }
}

















