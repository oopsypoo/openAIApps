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
        const string savepath_logs = "D:\\Users\\frode\\Documents\\openapi\\logs\\";
        const string savepath_snds = "D:\\Users\\frode\\Documents\\openapi\\snds\\";
        //filename containing options to openapi
        const string options_file = "options.json";
        readonly string logfile = savepath_logs + "logfile" + ".txt";

        public static requestGPT rxGPT = new requestGPT();
        public static responseGPT responseGPT = new responseGPT();
        public static HttpResponseMessage GlobalhttpResponse = new HttpResponseMessage();
        private VideoClient _videoClient;
        private List<VideoListItem> _videoHistory = new();
        private string _videoReferencePath = string.Empty;

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
        public static requestGPT RxGPT
        {
            get { return rxGPT; }
        }
       
        public void InitControls()
        {//set standard/default values to image controls
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
    }
}



