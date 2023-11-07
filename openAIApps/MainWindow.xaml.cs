using System;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using gpt35Turbo;
using whisper;
//speechsynthesis.cs
using TTS;




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
        const string savepath_logs = "D:\\Users\\frode\\Documents\\openapi\\logs\\";
        const string savepath_snds = "D:\\Users\\frode\\Documents\\openapi\\snds\\";
        //filename containing options to openapi
        const string options_file = "options.json";
        readonly string logfile = savepath_logs + "logfile" + ".txt";

        static requestGPT35 rxGPT35 = new requestGPT35();
        static responseGPT35Turbo responseGPT35 = new responseGPT35Turbo();
        public static HttpResponseMessage GlobalhttpResponse = new HttpResponseMessage();
        
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
        public MainWindow()
        {
            InitializeComponent();
            InitControls();
            Loaded += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            
        }
        public static requestGPT35 RxGPT35
        {
            get { return rxGPT35; }
        }
        public void InitControls()
        {
            foreach (var p in Dalle.optImages.optImages)
                cmNumberOfImages.Items.Add(p);
            cmNumberOfImages.SelectedItem = Dalle.optImages.noImages;

            foreach (var p in Dalle.optImages.optSize)
                cmSize.Items.Add(p);
            cmSize.SelectedItem = Dalle.optImages.csize;
        }
        private void menuHelp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void menuAbout_Click(object sender, RoutedEventArgs e)
        {
            About about = new About();
            about.ShowDialog();
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
            
            if (cbGPT35Chat.IsChecked == false)
                rxGPT35.InitRequest(txtRequest.Text);
            else
            {
                if (cmRole.SelectedIndex < 0)
                {
                    MessageBox.Show("You must select a role from the combobox", "Chat role-error", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.IsEnabled = true;
                    return;
                }
                else
                    rxGPT35.AddMessage(cmRole.Text, txtRequest.Text);
            }
            var jsonString = JsonSerializer.Serialize<requestGPT35>(rxGPT35);
            thisSession.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            thisSession.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", OpenAPIKey);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            try
            {
                var response = await thisSession.PostAsync(url_chat_completions, content).ConfigureAwait(false);
                var responseString = await response.Content.ReadAsStringAsync();



                //if there is an error we cannot deserialize to the class OpenAIResponse.
                //All member-variables are null. Therefore we can just do a check on one member
                responseGPT35 = JsonSerializer.Deserialize<responseGPT35Turbo>(responseString);

                this.Dispatcher.Invoke(() =>
                {
                    if (responseGPT35.choices == null)
                    {
                        txtResponse.Text = "Server-response:\n" + response + "\n\nError:" + responseString;
                    }
                    else
                    {
                        txtResponse.Text = responseGPT35.choices[0].message.content;
                        if(SpeechSynthesis.TTSUse)
                        { 
                            var res = SpeechSynthesis.TTSAsync(txtResponse.Text); //this is fun S)
                        }
                        //we have to do everythig here...remember that we have to have a counter so that new responses always come last
                        if (cbGPT35Chat.IsChecked == true)
                        {
                            //if chat mode, we take care of response. At this point the user has allready sent a request so we can add another
                            //this one was new for me: (rxGPT35.messages.Length - 1) = (^1) (index operators)
                            cmHistory.Items.Add(rxGPT35.messages[^1].role + ", " + rxGPT35.messages[^1].content);
                            cmHistory.Items.Add(responseGPT35.choices[0].message.role + ", " + responseGPT35.choices[0].message.content);
                            rxGPT35.AddMessage(responseGPT35.choices[0].message.role, responseGPT35.choices[0].message.content);
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
            rxGPT35.SaveToLogFile();
            rxGPT35.Dispose();
            cmHistory.Items.Clear();
            txtRequest.Clear();
            txtResponse.Clear();
        }

        private void cmRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            rxGPT35.SetRole(cmRole.SelectedItem.ToString());
        }

        private void txtRequest_GotFocus(object sender, RoutedEventArgs e)
        {
            txtRequest.SelectAll();
        }

        private void cmRole_Initialized(object sender, EventArgs e)
        {
            if (this.IsEnabled)
            {
                if(rxGPT35 != null)
                {
                    cmRole.ItemsSource = rxGPT35.gpt35Roles;
                }
            }
        }
        private void cbGPT35Chat_Checked(object sender, RoutedEventArgs e)
        {
            cmHistory.IsEnabled = true;
            btnClearHistory.IsEnabled = true;
            cmRole.IsEnabled = true;
            cmRole.SelectedIndex = 0;
        }
        private void cbGPT35Chat_Unchecked(object sender, RoutedEventArgs e)
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
            string temp = Dalle.GetImageFileName();
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
            string fullpath = Dalle.GetImageFileName();
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
            rxGPT35.SaveToLogFile();
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
            this.IsEnabled = false;
            
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
            if(rxGPT35 != null)
                rxGPT35.user = tbName.Text;
        }

        private void sldrFrequencyPenalty_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(rxGPT35 != null) 
            {
                string tempLabel = "Frequency Penalty: ";
                rxGPT35.frequency_penalty = sldrFrequencyPenalty.Value;
                lblFreqencyPenalty.Content = tempLabel + sldrFrequencyPenalty.Value.ToString("N1");
            }
        }

        private void sldrPresencePenalty_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(rxGPT35 != null) 
            {
                string tempLabel = "Presence Penalty: ";
                rxGPT35.presence_penalty = sldrPresencePenalty.Value;
                lblPresencePenalty.Content = tempLabel + sldrPresencePenalty.Value.ToString("N1");
            }
        }

        private void bStream_Click(object sender, RoutedEventArgs e)
        {
            if(rxGPT35 != null)
            { 
                if (bStream.IsChecked == true)
                    rxGPT35.stream = true;
                else
                    rxGPT35.stream = false;
            }
        }

        private void sldrN_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(rxGPT35 != null)
            {
                string tempLabel = "n: ";
                rxGPT35.n = (int)sldrN.Value;
                lblN.Content = tempLabel + sldrN.Value.ToString();
            }
        }

        private void sldrTopp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (rxGPT35 != null)
            {
                string tempLabel = "Top_p: ";
                rxGPT35.top_p = sldrTopp.Value;
                lblTopp.Content = tempLabel + sldrTopp.Value.ToString("N1");
            }
        }

        private void sldrTemperature_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (rxGPT35 != null)
            {
                string tempLabel = "Temperature: ";
                rxGPT35.temperature = sldrTemperature.Value;
                lblTemperature.Content = tempLabel+sldrTemperature.Value.ToString("N1");
            }
        }

        private void menuSpeechSynthesisTool_Click(object sender, RoutedEventArgs e)
        {
            SpeechSynthesisTool speechSynthesisTool = new SpeechSynthesisTool();
            speechSynthesisTool.ShowDialog();
        }

        private void cmbAvailableModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void cmbAvailableModels_Initialized(object sender, EventArgs e)
        {

        }
    }
}
