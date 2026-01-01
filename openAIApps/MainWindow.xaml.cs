using gpt;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//speechsynthesis.cs
using TTS;
using whisper;
using static openAIApps.Responses;
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
        /// <summary>
        /// new url for gpt-3.5-turbo, which also uses a new structure
        /// we'll call this class GptTurbo. You add a new messages-struct that says something about the role
        /// the role can be 1 of 4(for the moment)
        /// system
        /// user
        /// assistant
        /// </summary>
        /// 
        const string url_chat_completions = "https://api.openai.com/v1/chat/completions";

        readonly string OpenAPIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        /// <summary>
        /// make these paths more general-purpose in the future
        /// </summary>
        //all logs are stored here
        private string appRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "openapi");
        private string savepath_logs;
        private string savepath_snds;
        //const string savepath_logs = "C:\\Users\\wwsac\\Documents\\openapi\\logs\\";
        //const string savepath_snds = "C:\\Users\\wwsac\\Documents\\openapi\\snds\\";
        //filename containing options to openapi
        const string options_file = "options.json";
        //readonly string logfile = savepath_logs + "logfile" + ".txt";
        private string logfile;
        public static requestGPT rxGPT = new requestGPT();
        public static responseGPT responseGPT = new responseGPT();
        public static HttpResponseMessage GlobalhttpResponse = new HttpResponseMessage();
        private VideoClient _videoClient;
        private List<VideoListItem> _videoHistory = new();
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
        //private async void btn_oai_rx_send_ClickAsync(object sender, RoutedEventArgs e)
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
        private void InitResponsesControls()
        {
            _responsesClient = new Responses(OpenAPIKey);

            // Frontier GPT-5 family (reasoning-enabled)
            cmbResponsesModel.Items.Clear();
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5.2", Tag = "gpt-5.2" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5.2-pro", Tag = "gpt-5.2-pro" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5.1", Tag = "gpt-5.1" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5-pro", Tag = "gpt-5-pro" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5-mini", Tag = "gpt-5-mini" });

            // GPT-4.1 family (non-reasoning)
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-4.1", Tag = "gpt-4.1" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-4.1-mini", Tag = "gpt-4.1-mini" });

            // GPT-4o series
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-4o", Tag = "gpt-4o", IsSelected = true });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-4o-mini", Tag = "gpt-4o-mini" });

            // Dedicated reasoning models (o-series)
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "o3", Tag = "o3" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "o3-pro", Tag = "o3-pro" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "o3-mini", Tag = "o3-mini" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "o4-mini", Tag = "o4-mini" });

            // Specialized / preview
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "computer-use-preview", Tag = "computer-use-preview" });

            cmbResponsesModel.SelectedIndex = 6; // gpt-4o (safe default)
            _responsesClient.CurrentModel = "gpt-4o";

            // Tools (unchanged)
            _responsesClient.ActiveTools.Clear();
            _responsesClient.ActiveTools.Add("text");       // conceptual default
            cbToolText.IsChecked = true;
            cbToolWebSearch.IsChecked = false;
            cbToolComputerUse.IsChecked = false;
            _responsesClient.WebSearchContextSize = "medium";
            cmbSearchContextSize.SelectedIndex = 1; // medium
            cmbSearchContextSize.IsEnabled = false;



            // Reasoning levels (per docs)
            cmbReasoning.Items.Clear();
            cmbReasoning.Items.Add(new ComboBoxItem { Content = "none", Tag = "none" });
            cmbReasoning.Items.Add(new ComboBoxItem { Content = "minimal", Tag = "minimal" });
            cmbReasoning.Items.Add(new ComboBoxItem { Content = "low", Tag = "low" });
            cmbReasoning.Items.Add(new ComboBoxItem { Content = "medium", Tag = "medium" });
            cmbReasoning.Items.Add(new ComboBoxItem { Content = "high", Tag = "high" });
            cmbReasoning.Items.Add(new ComboBoxItem { Content = "xhigh", Tag = "xhigh" });
            cmbReasoning.SelectedIndex = 0;
            _responsesClient.CurrentReasoning = "none";
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
        public static requestGPT RxGPT
        {
            get { return rxGPT; }
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
            tpGPT.Header = rxGPT.model;
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
            if(!String.IsNullOrEmpty(path))
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
        private async void btnSendRequest_Click(object sender, RoutedEventArgs e)
        {
            HttpClient thisSession = new HttpClient();
            // disable this control/window while the http request is done so that the user cannot press other
            // controls in this window. One  request at a time. Actually I thougt it would be just the button that would be disabled,
            //but the whole window becomes disabled...???
            this.IsEnabled = false;
            SaveToLogFile(txtRequest.Text);
            
            if (cbGPTChat.IsChecked == false)
                rxGPT.InitRequest(txtRequest.Text);
            else
            {
                if (cmRole.SelectedIndex < 0)
                {
                    MessageBox.Show("You must select a role from the combobox", "Chat role-error", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.IsEnabled = true;
                    return;
                }
                else
                    rxGPT.AddMessage(cmRole.Text, txtRequest.Text);
            }
            var jsonString = JsonSerializer.Serialize<requestGPT>(rxGPT);
            thisSession.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            thisSession.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", OpenAPIKey);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            try
            {
                var response = await thisSession.PostAsync(url_chat_completions, content).ConfigureAwait(false);
                var responseString = await response.Content.ReadAsStringAsync();



                //if there is an error we cannot deserialize to the class OpenAIResponse.
                //All member-variables are null. Therefore we can just do a check on one member
                responseGPT = JsonSerializer.Deserialize<responseGPT>(responseString);

                this.Dispatcher.Invoke(() =>
                {
                    if (responseGPT.choices == null)
                    {
                        txtResponse.Text = "Server-response:\n" + response + "\n\nError:" + responseString;
                    }
                    else
                    {
                        txtResponse.Text = responseGPT.choices[0].message.content;
                        if(SpeechSynthesis.TTSUse)
                        { 
                            var res = SpeechSynthesis.TTSAsync(txtResponse.Text); //this is fun S)
                        }
                        //we have to do everythig here...remember that we have to have a counter so that new responses always come last
                        if (cbGPTChat.IsChecked == true)
                        {
                            //if chat mode, we take care of response. At this point the user has allready sent a request so we can add another
                            //this one was new for me: (rxGPT.messages.Length - 1) = (^1) (index operators)
                            cmHistory.Items.Add(rxGPT.messages[^1].role + ", " + rxGPT.messages[^1].content);
                            cmHistory.Items.Add(responseGPT.choices[0].message.role + ", " + responseGPT.choices[0].message.content);
                            rxGPT.AddMessage(responseGPT.choices[0].message.role, responseGPT.choices[0].message.content);
                        }
                    }
                });
            }
            catch (Exception err)///not sure if this helps, but for now it seems so. Catch any error and print it.
            {
                this.Dispatcher.Invoke(() =>
                {
                    txtResponse.Text = err.Message + "\nInnerException: " + err.InnerException;
                });
            }
            this.Dispatcher.Invoke(() =>
            {
                this.IsEnabled = true;
            });
        }
            
        


        

        private void btnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            rxGPT.SaveToLogFile();
            rxGPT.Dispose();
            cmHistory.Items.Clear();
            txtRequest.Clear();
            txtResponse.Clear();
        }

        private void cmRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            rxGPT.SetRole(cmRole.SelectedItem.ToString());
        }

        private void txtRequest_GotFocus(object sender, RoutedEventArgs e)
        {
            txtRequest.SelectAll();
        }

        private void cmRole_Initialized(object sender, EventArgs e)
        {
            if (this.IsEnabled)
            {
                if(rxGPT != null)
                {
                    cmRole.ItemsSource = rxGPT.gpt_roles;
                }
            }
        }
        private void cbGPTChat_Checked(object sender, RoutedEventArgs e)
        {
            cmHistory.IsEnabled = true;
            btnClearHistory.IsEnabled = true;
            cmRole.IsEnabled = true;
            cmRole.SelectedIndex = 0;
        }
        private void cbGPTChat_Unchecked(object sender, RoutedEventArgs e)
        {
            cmHistory.IsEnabled=false;
            btnClearHistory.IsEnabled=false;
            cmRole.IsEnabled=false;
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
                else if(cbImageEdit.IsChecked == true)
                {
                    Dalle.rxImagesEdit.prompt = txtDalleRequest.Text;
                    GlobalhttpResponse = await Dalle.rxImagesEdit.PostFile(OpenAPIKey);
                }
                
                var responseString = await GlobalhttpResponse.Content.ReadAsStringAsync();

                Dalle.resource = JsonSerializer.Deserialize<Dalle.responseImage>(responseString);
                /// there's an error. just get out.
               
                if(Dalle.resource.data == null)
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
            catch(Exception err)
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
            else if(src == cbImageEdit)
                Dalle.rxImagesEdit.image = null;
            
        }
        private void AddImageControls(object src)
        {
            if(src == btnMaskImage || src == cbImageEdit)
            { 
                lblSelectedMask.Visibility = Visibility.Visible;
                imageMask.Visibility = Visibility.Visible;
                btnRemoveMask.Visibility = Visibility.Visible;
            }
            if(src == btnOpenImage || src == cbImageEdit)
            { 
                lblSelectedImage.Visibility = Visibility.Visible;
                imageSelected.Visibility = Visibility.Visible;
                btnRemoveImage.Visibility = Visibility.Visible;
            }
        }
        private void btnOpenImage_Click(object sender, RoutedEventArgs e)
        {
            string temp = Dalle.GetImageFileName(Dalle.GetSavepath_pics());
            if (string.IsNullOrEmpty(temp))
            { 
                MessageBox.Show("String cannot be empty", "Error", MessageBoxButton.OK,MessageBoxImage.Error);
                return;
            }
            string filename = ExtractFileName(temp);
            
            if (cbImageVariations.IsChecked == true)
            { 
                Dalle.rxImagesVariation.image = filename;
                imageSelected.Source = GetImageSource(temp);
            }
            else if(cbImageEdit.IsChecked == true)
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
            AddImageControls (sender);
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

        private void menuSave_Click(object sender, RoutedEventArgs e)
        {
            rxGPT.SaveToLogFile();
        }

        private void btnRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            imageSelected.Source = null;
            btnRemoveImage.Visibility= Visibility.Hidden;
            lblSelectedImage.Visibility= Visibility.Hidden;
            if(cbImageVariations.IsChecked == true) 
            { 
                Dalle.rxImagesVariation.image = null; 
            }
            else
                Dalle.rxImagesEdit.image = null;
        }

        private void btnRemoveMask_Click(object sender, RoutedEventArgs e)
        {
            imageMask.Source = null;
            btnRemoveMask.Visibility= Visibility.Hidden;
            lblSelectedMask.Visibility= Visibility.Hidden;
            Dalle.rxImagesEdit.mask = null;
        }
        
        

        private void cmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch(cmbLanguage.SelectedIndex)
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
            txtWhisperResponse.Text = "";
            this.IsEnabled = true;
            
            try
            {
                GlobalhttpResponse = await Whisper.RxWhisper.PostFile(OpenAPIKey);
                
                var responseString = await GlobalhttpResponse.Content.ReadAsStringAsync();

                Whisper.ResWhisper = JsonSerializer.Deserialize<Whisper.ResponseWhisper>(responseString);
                /// there's an error. just get out.

                if (Whisper.ResWhisper.text == null)
                {
                    txtDalleResponse.Text = "Server-response: \n" + GlobalhttpResponse + "\n\nError:\n" + responseString;
                    this.IsEnabled = true;
                    return;
                }
                else
                {
                    ///everything is ok. Draw/create the images
                    txtWhisperResponse.Text = Whisper.ResWhisper.text;
                    
                    //var res = SpeechSynthesis.TTSAsync(txtWhisperResponse.Text);
                    
                }
            }
            catch (Exception err)
            {
                this.Dispatcher.Invoke(() =>
                {
                    txtWhisperResponse.Text = err.Message + "\nInnerexception: " + err.InnerException;
                    this.IsEnabled = true;
                    return;
                });
            }

            this.Dispatcher.Invoke(() =>
            {
                this.IsEnabled = true;
            });
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

        private void tbName_LostFocus(object sender, RoutedEventArgs e)
        {
            if(rxGPT != null)
                rxGPT.user = tbName.Text;
        }

        private void sldrFrequencyPenalty_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(rxGPT != null) 
            {
                string tempLabel = "Frequency Penalty: ";
                rxGPT.frequency_penalty = sldrFrequencyPenalty.Value;
                lblFreqencyPenalty.Content = tempLabel + sldrFrequencyPenalty.Value.ToString("N1");
            }
        }

        private void sldrPresencePenalty_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(rxGPT != null) 
            {
                string tempLabel = "Presence Penalty: ";
                rxGPT.presence_penalty = sldrPresencePenalty.Value;
                lblPresencePenalty.Content = tempLabel + sldrPresencePenalty.Value.ToString("N1");
            }
        }

        private void bStream_Click(object sender, RoutedEventArgs e)
        {
            if(rxGPT != null)
            { 
                if (bStream.IsChecked == true)
                    rxGPT.stream = true;
                else
                    rxGPT.stream = false;
            }
        }

        private void sldrN_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(rxGPT != null)
            {
                string tempLabel = "n: ";
                rxGPT.n = (int)sldrN.Value;
                lblN.Content = tempLabel + sldrN.Value.ToString();
            }
        }

        private void sldrTopp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (rxGPT != null)
            {
                string tempLabel = "Top_p: ";
                rxGPT.top_p = sldrTopp.Value;
                lblTopp.Content = tempLabel + sldrTopp.Value.ToString("N1");
            }
        }

        private void sldrTemperature_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (rxGPT != null)
            {
                string tempLabel = "Temperature: ";
                rxGPT.temperature = sldrTemperature.Value;
                lblTemperature.Content = tempLabel+sldrTemperature.Value.ToString("N1");
            }
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

        private void imgVision_Initialized(object sender, EventArgs e)
        {

        }

        private async void btnVisionSendRequest_Click(object sender, RoutedEventArgs e)
        {

            Vision.InitChatVision(txtVisionRequest.Text);
            var responseString = await Vision.ResponseChat(OpenAPIKey);
            if (responseString == null)
            {
                txtVisionResponse.Text = "Server-response: \n" + GlobalhttpResponse + "\n\nError:\n" + responseString;
                this.IsEnabled = true;
                return;
            }
            else
            {
                //everything is ok. Get response
                txtVisionResponse.Text = responseString.Message.Role + "\n" + responseString.Message.Content;
            }
            /*
               Vision.rxVision.messages[0].content[0].text = txtVisionRequest.Text;
               try
               {
                   GlobalhttpResponse = await Vision.rxVision.PostFile(OpenAPIKey);


                   var response = await GlobalhttpResponse.Content.ReadAsStringAsync();

                   //var resource = JsonSerializer.Deserialize(responseString);
                   /// there's an error. just get out.

                   if (response == null)
                   {
                       txtVisionResponse.Text = "Server-response: \n" + GlobalhttpResponse + "\n\nError:\n" + response;
                       this.IsEnabled = true;
                       return;
                   }
                   else
                   {
                       //everything is ok. Get response
                       txtVisionResponse.Text = response;
                   }
               }
               catch (Exception err)
               {
                   this.Dispatcher.Invoke(() =>
                   {
                       txtVisionResponse.Text = err.Message + "\nInnerexception: " + err.InnerException;
                       this.IsEnabled = true;
                       return;
                   });
               }

               this.Dispatcher.Invoke(() =>
               {
                   this.IsEnabled = true;
               });
          */
        }

        private void txtVisionRequest_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        private void cmbVisionImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void btnOpenVImage_Click(object sender, RoutedEventArgs e)
        {
            Vision.ImageFileName = Vision.GetImageFileName();
            var fileExtension = Path.GetExtension(Vision.ImageFileName);
            if (string.IsNullOrEmpty(Vision.ImageFileName))
            {
                MessageBox.Show("String cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            //string filename = ExtractFileName(temp);
            /*byte[] imageBytes = File.ReadAllBytes(Vision.ImageFileName);
            string base64Image = Convert.ToBase64String(imageBytes);
            Vision.rxVision.messages[0].content[0].image_url.url = $"data:image/{fileExtension};base64,{base64Image}";
            */

            imgVision.Source = GetImageSource(Vision.ImageFileName);
            
            AddImageControls(sender);
        }

        private void cmbModelChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private void cmbVideoLengthChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private void cmbVideoSizeChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private async void btnVideoSendRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Build request from your UI controls
                var request = new VideoClient.RequestVideo
                {
                    Prompt = txtVideoPrompt.Text,
                    Model = cmbVideoModel.Text,
                    Seconds = cmbVideoLength.Text,
                    Size = cmbVideoSize.Text
                };

                // Create the video job
                var response = await _videoClient.CreateVideoAsync(request);

                // Show raw JSON in your response text box
                txtVideoResponse.Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });

                // --- Handle invalid response ---
                if (response == null || string.IsNullOrEmpty(response.Id))
                {
                    MessageBox.Show("Video creation request failed. No video ID returned.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // --- Add to your listbox immediately ---
                lstVideoFiles.Items.Add(new VideoClient.VideoListItem
                {
                    Id = response.Id,
                    Status = response.Status,
                    Progress = response.Progress,
                    Model = response.Model
                });

                // --- Create and show progress window ---
                var progressWindow = new ProgressWindow("Creating video...");
                progressWindow.Owner = this;

                var cts = new CancellationTokenSource();
                progressWindow.Canceled += (s, _) => cts.Cancel();

                progressWindow.Show();

                // --- Track progress using the client helper ---
                var progress = new Progress<double>(value =>
                {
                    progressWindow.UpdateProgress(value);
                });

                await _videoClient.MonitorVideoProgressAsync(response.Id, progress, cts.Token);

                progressWindow.Close();

                // --- Check final status ---
                var finalStatus = await _videoClient.GetVideoStatusAsync(response.Id);

                if (finalStatus != null)
                {
                    txtVideoResponse.Text = JsonSerializer.Serialize(finalStatus, new JsonSerializerOptions { WriteIndented = true });

                    // Update listbox item if found
                    var existing = lstVideoFiles.Items.OfType<VideoClient.VideoListItem>().FirstOrDefault(v => v.Id == finalStatus.Id);
                    if (existing != null)
                    {
                        existing.Status = finalStatus.Status;
                        existing.Progress = finalStatus.Progress;
                        lstVideoFiles.Items.Refresh();
                    }

                    if (finalStatus.Status == "completed")
                    {
                        MessageBox.Show($"✅ Video {finalStatus.Id} created successfully!", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (finalStatus.Status == "failed")
                    {
                        MessageBox.Show($"❌ Video {finalStatus.Id} failed to generate.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        MessageBox.Show($"⚠️ Video {finalStatus.Id} has status: {finalStatus.Status}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Could not retrieve final video status.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}", "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void btnOpenVReference_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a reference image",
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                _videoReferencePath = dlg.FileName;

                // Display preview in the Image control
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_videoReferencePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                imgVideo.Source = bitmap;
            }
        }
        private async void InitVideoList()
        {
            try
            {
                var listResponse = await _videoClient.GetAllVideosAsync();
                if (listResponse?.Data != null)
                {
                    _videoHistory = listResponse.Data;  // Populate our local list
                    lstVideoFiles.ItemsSource = _videoHistory;
                    lstVideoFiles.DisplayMemberPath = "Id";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load video list:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnDownloadVideo_Click(object sender, RoutedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video to download.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string videoId = selectedVideo.Id;

            // Create progress window
            var progressWindow = new ProgressWindow("Downloading video...");
            progressWindow.Owner = this;
            progressWindow.Show();

            var progress = new Progress<double>(value =>
            {
                progressWindow.UpdateProgress(value);
            });

            bool success = await _videoClient.DownloadVideoAsync(videoId, progress);

            progressWindow.Close();

            if (success)
                MessageBox.Show($"Video {videoId} downloaded successfully.", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show($"Failed to download video {videoId}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void btnDeleteVideo_Click(object sender, RoutedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete video {selectedVideo.Id}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                await _videoClient.DeleteVideoAsync(selectedVideo.Id);

                // Remove it from the UI list
                var videos = lstVideoFiles.ItemsSource as List<VideoListItem>;
                if (videos != null)
                {
                    videos.Remove(selectedVideo);
                    lstVideoFiles.ItemsSource = null;
                    lstVideoFiles.ItemsSource = videos;
                }

                MessageBox.Show("Video deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete video:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnGetStatus_Click(object sender, RoutedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var status = await _videoClient.GetVideoStatusAsync(selectedVideo.Id);

                // Show JSON string in your response TextBox (or format nicely)
                txtVideoResponse.Text = JsonSerializer.Serialize(status, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Optional: update your local _videoHistory so progress/status is current
                selectedVideo.Status = status.Status;
                selectedVideo.Progress = status.Progress;

                // Refresh ListBox to reflect any changes
                lstVideoFiles.ItemsSource = null;
                lstVideoFiles.ItemsSource = _videoHistory;
                lstVideoFiles.DisplayMemberPath = "Id";
            }
            catch (Exception ex)
            {
                txtVideoResponse.Text = $"Error fetching video status:\n{ex.Message}";
            }
        }

        private void btmRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            // Clear the reference file path
            _videoReferencePath = string.Empty;
            _videoClient.ReferenceFilePath = string.Empty; // if your VideoClient uses this

            // Reset the preview image
            imgVideo.Source = new BitmapImage(new Uri("/no_pic.png", UriKind.Relative));
        }
        private void btnPlayVideo_Click(object sender, RoutedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video first.", "No Selection",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string videosDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            string localFilePath = Path.Combine(videosDir, $"{selectedVideo.Id}.mp4");

            if (!File.Exists(localFilePath))
            {
                MessageBox.Show("Video not found locally. Please download it first.", "Missing File",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Open preview window
            var previewWindow = new VideoPreviewWindow(localFilePath);
            previewWindow.ShowDialog();
        }


        private void cbTool_Checked(object sender, RoutedEventArgs e)
        {
            if (_responsesClient == null || sender is not CheckBox cb)
                return;

            string key = cb.Name switch
            {
                "cbToolText" => "text",
                "cbToolWebSearch" => "web_search",
                "cbToolComputerUse" => "computer_use",
                _ => null
            };
            
            if (key == null)
                return;

            if (cb.IsChecked == true)
            {
                if (key == "text")
                {
                    // If "text" is checked, clear all other tools
                    _responsesClient.ActiveTools.Clear();
                    _responsesClient.ActiveTools.Add("text");
                    cbToolWebSearch.IsChecked = false;
                    cbToolComputerUse.IsChecked = false;
                }
                else
                {
                    // Turn off "text" if any real tools are enabled
                    _responsesClient.ActiveTools.Remove("text");
                    cbToolText.IsChecked = false;
                    _responsesClient.ActiveTools.Add(key);
                }
            }
            else
            {
                _responsesClient.ActiveTools.Remove(key);

                // If no tools left, fall back to "text"
                if (_responsesClient.ActiveTools.Count == 0)
                {
                    _responsesClient.ActiveTools.Add("text");
                    cbToolText.IsChecked = true;
                }
            }
            cmbSearchContextSize.IsEnabled = cbToolWebSearch.IsChecked == true;
        }

        // Your existing btnResponsesSendRequest_Click stays the same, just simpler:
        private async void btnResponsesSendRequest_Click(object sender, RoutedEventArgs e)
        {
            if (_responsesClient == null)
            {
                MessageBox.Show("Responses client not initialized.", "Error");
                return;
            }
            string prompt = txtResponsesPrompt.Text;
            this.IsEnabled = false;
            txtResponsesResponse.Text = string.Empty;

            try
            {
                string result = await _responsesClient.GetResponseAsync(txtResponsesPrompt.Text);
                // Set user text on the last turn
                _responsesClient.SetLastUserText(prompt);

                // Refresh the ListBox binding
                lstResponsesTurns.ItemsSource = null;
                lstResponsesTurns.ItemsSource = _responsesClient.ConversationLog;

                // Select the newest turn
                if (_responsesClient.ConversationLog.Count > 0)
                    lstResponsesTurns.SelectedIndex = _responsesClient.ConversationLog.Count - 1;
                txtResponsesResponse.Text = result;
            }
            catch (Exception ex)
            {
                txtResponsesResponse.Text = $"Error: {ex.Message}";
            }
            finally
            {
                this.IsEnabled = true;
            }
        }
        private void cmbResponsesModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbResponsesModel.SelectedItem is ComboBoxItem selected)
            {
                string model = selected.Tag.ToString();
                _responsesClient.CurrentModel = model;

                // Model-specific reasoning warnings
                string reasoning = _responsesClient.CurrentReasoning;
                bool supportsReasoning = model.StartsWith("gpt-5") ||
                                        model.StartsWith("o") ||
                                        model == "gpt-5-pro";

                if (!supportsReasoning && reasoning != "none")
                {
                    MessageBox.Show(
                        $"⚠️ '{model}' may ignore reasoning.effort='{reasoning}'. " +
                        "Use GPT-5/o-series models for reasoning controls.",
                        "Model Compatibility",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Auto-reset to safe default
                    cmbReasoning.SelectedIndex = 0;
                    _responsesClient.CurrentReasoning = "none";
                }
                else if (model == "gpt-5-pro" && reasoning != "high")
                {
                    MessageBox.Show(
                        "gpt-5-pro only supports 'high' reasoning. Auto-setting.",
                        "Model Note",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    cmbReasoning.SelectedIndex = 4; // high
                    _responsesClient.CurrentReasoning = "high";
                }
            }
        }
        private void cmbReasoning_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_responsesClient == null)
                return;

            if (sender is ComboBox cmb && cmb.SelectedItem is ComboBoxItem selectedReasoning && selectedReasoning.Tag != null)
            {
                _responsesClient.CurrentReasoning = selectedReasoning.Tag.ToString();
            }
            // If nothing selected (during init), just keep current value
        }

        
        private void btnResponsesNewChat_Click(object sender, RoutedEventArgs e)
        {
            _responsesClient?.ClearConversation();
            _responsesClient?.ConversationLog.Clear();

            lstResponsesTurns.ItemsSource = null;
            txtResponsesPrompt.Clear();
            txtResponsesResponse.Clear();
        }

        private async void btnResponsesDeleteChat_Click(object sender, RoutedEventArgs e)
        {
            if (_responsesClient == null || !_responsesClient.ConversationActive)
            {
                MessageBox.Show("No active conversation to delete.", "Info");
                return;
            }

            var confirm = MessageBox.Show(
                "Delete the current conversation on server and clear history?",
                "Delete Conversation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                bool ok = await _responsesClient.DeleteConversationAsync(_responsesClient.LastResponseId);
                if (ok)
                    MessageBox.Show("Conversation deleted.", "Deleted");

                _responsesClient.ClearConversation();
                _responsesClient.ConversationLog.Clear();
                lstResponsesTurns.ItemsSource = null;
                txtResponsesPrompt.Clear();
                txtResponsesResponse.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting conversation: {ex.Message}", "Error");
            }
        }

        
        private void lstResponsesTurns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstResponsesTurns.SelectedItem is ResponsesTurn turn)
            {
                txtResponsesPrompt.Text = turn.UserText ?? "";
                txtResponsesResponse.Text = turn.AssistantText ?? "";
            }
        }
        private void cmbSearchContextSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_responsesClient == null)
                return;

            if (cmbSearchContextSize.SelectedItem is ComboBoxItem selected &&
                selected.Tag is string tag)
            {
                _responsesClient.WebSearchContextSize = tag; // "low", "medium", or "high"
            }
        }

    }
}





