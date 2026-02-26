using Microsoft.EntityFrameworkCore;
using openAIApps.Data;
using openAIApps.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;


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
        
        private AppSettings _settings;
        private string savepath_logs;
        private string savepath_snds;
        private string savepath_images;
        private string savepath_videos;
        
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
        private readonly AppDbContext _context;
        private readonly HistoryService _historyService;
        // This collection is the "Live" data for the current Responses tab
        public ObservableCollection<ChatMessage> CurrentChatMessages { get; } = new();

        // This collection is for the "Logs" tab search
        public ObservableCollection<ChatSession> HistoricSessions { get; } = new();

        // Inside MainWindow class
        private int? _activeResponsesSessionId;
        private int? _activeVideoSessionId;

        // A helper to ensure we always have a session before sending a request
        private async Task<int> EnsureSessionActiveAsync(EndpointType type, string firstPrompt)
        {
            if (type == EndpointType.Responses && _activeResponsesSessionId == null)
            {
                _activeResponsesSessionId = await _historyService.StartNewSessionAsync(ExtractTitle(firstPrompt), type);
            }
            else if (type == EndpointType.Video && _activeVideoSessionId == null)
            {
                // Use your existing tracking variable for videos
                _activeVideoSessionId = await _historyService.StartNewSessionAsync(ExtractTitle(firstPrompt), type);
            }

            // Return the appropriate ID based on the endpoint type
            return (type == EndpointType.Responses ? _activeResponsesSessionId : _activeVideoSessionId)!.Value;
        }

        private string ExtractTitle(string prompt) => prompt.Length > 30 ? $"{prompt[..30]}..." : prompt;
        private void EnsureSavePaths()
        {
            _settings ??= AppSettings.LoadSettings();  // Load once

            savepath_logs = Path.Combine(_settings.AppRoot, _settings.LogsFolder);
            savepath_snds = Path.Combine(_settings.AppRoot, _settings.SoundsFolder);
            savepath_images = Path.Combine(_settings.AppRoot, _settings.ImagesFolder);
            savepath_videos = Path.Combine(_settings.AppRoot, _settings.VideosFolder);
            
            Directory.CreateDirectory(savepath_logs);
            Directory.CreateDirectory(savepath_snds);
            Directory.CreateDirectory(savepath_images);
            Directory.CreateDirectory(savepath_videos);
        }



        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitResponsesControls(); // Move ALL combo population here
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
        public MainWindow()
        {
            InitializeComponent();
            _context = new AppDbContext();
            _context.Database.EnsureCreated(); // Ensures SQLite file exists
            _historyService = new HistoryService(_context);

            // Pass history service to your existing logic classes if needed
            //_responsesClient.SetHistoryService(_historyService);
            InitControls();
            Loaded += MainWindow_Loaded;

        }

        public void InitControls()
        {
            EnsureSavePaths();
            _videoClient = new VideoClient(apiKey: OpenAPIKey);
            // Bind the UI controls to these collections
            lstResponsesTurns.ItemsSource = CurrentChatMessages;
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

        private void menuSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow(_settings);
            bool? result = window.ShowDialog();
            if (result == true)  // Or: if (result.HasValue && result.Value)
            {
                EnsureSavePaths();  // Refresh paths only on save
            }
        }
        // Call this when clicking the "Logs" tab
        private async void RefreshLogsTab()
        {
            if(_historyService == null) return; // Safety check
            var sessions = await _historyService.GetRecentSessionsAsync();
            //HistoricSessions.Clear();
            //foreach (var s in sessions) HistoricSessions.Add(s);
            dgUnifiedLogs.ItemsSource = sessions; // Directly bind to the new list
        }
        // \openAIApps\MainWindow.xaml.cs
        private async void OnLogEntryDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 1. Get the data item from CurrentItem instead of SelectedItem
            // This is often more reliable than 'row.Item' which can be 'Disconnected'
            var selectedSession = dgUnifiedLogs.CurrentItem as ChatSession;

            // 2. If CurrentItem is null, try to fall back to the visual hit test
            if (selectedSession == null)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    while (dep != null && !(dep is DataGridRow))
                    {
                        dep = VisualTreeHelper.GetParent(dep);
                    }
                    if (dep is DataGridRow row && row.Item is ChatSession session && !row.Item.ToString().Contains("DisconnectedItem"))
                    {
                        selectedSession = session;
                    }
                }
            }

            // 3. Process the session if we found a valid one
            if (selectedSession != null)
            {
                _activeResponsesSessionId = selectedSession.Id;

                if (selectedSession.Endpoint == EndpointType.Responses)
                {
                    tabMain.SelectedIndex = 2;
                    txtResponsesResponse.Clear();

                    // Reload UI from SQLite
                    await RefreshCurrentChatUI(selectedSession.Id);

                    // Populate the large response box
                    var history = await _historyService.GetFullSessionHistoryAsync(selectedSession.Id);
                    var lastAssistant = history.LastOrDefault(m => m.Role.ToLower() == "assistant");
                    if (lastAssistant != null)
                    {
                        txtResponsesResponse.Text = lastAssistant.Content;
                    }

                    StatusText.Text = $"Loaded: {selectedSession.Title}";
                }
                else if (selectedSession.Endpoint == EndpointType.Video)
                {
                    tabMain.SelectedIndex = 1;
                    // 2. Load the prompt from the database
                    var history = await _historyService.GetFullSessionHistoryAsync(selectedSession.Id);
                    var lastUserMsg = history.LastOrDefault(m => m.Role.ToLower() == "user");

                    if (lastUserMsg != null)
                    {
                        // Rehydrate the UI with the saved settings
                        txtVideoPrompt.Text = lastUserMsg.Content;
                        cmbVideoModel.Text = lastUserMsg.ModelUsed;
                        cmbVideoLength.Text = lastUserMsg.VideoLength;
                        cmbVideoSize.Text = lastUserMsg.VideoSize;
                        cbVideoRemix.IsChecked = lastUserMsg.IsRemix;
                    }
                    // Implement similar logic for video sessions if needed
                    StatusText.Text = $"Loaded video session: {selectedSession.Title}";
                }
            }
        }

        private async void TxtLogSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = txtLogSearch.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                // Load the 50 most recent if search is empty
                dgUnifiedLogs.ItemsSource = await _historyService.GetRecentSessionsAsync();
                return;
            }

            // Modern C# Search Logic
            var results = await _context.Sessions
                .Where(s => s.Title.Contains(query) ||
                            s.Messages.Any(m => m.Content.Contains(query)))
                .OrderByDescending(s => s.LastUsedAt)
                .ToListAsync();

            dgUnifiedLogs.ItemsSource = results;
        }
        

        private void tabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshLogsTab();
        }
        // 1. Delete Session
        private async void OnDeleteSessionClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ChatSession session)
            {
                var result = MessageBox.Show($"Delete session '{session.Title}'?", "Confirm", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    await _historyService.DeleteSessionAsync(session.Id);
                    RefreshLogsTab(); // Refresh the list
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Delete clicked but DataContext was null or wrong type!");
            }
        }

        // 2. Filter Sessions
        private async void OnLogFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbLogTypeFilter.SelectedItem is ComboBoxItem item)
            {
                string filter = item.Content.ToString();
                // You'll need to update your HistoryService to accept a filter string
                var sessions = await _historyService.GetRecentSessionsAsync(filter);
                dgUnifiedLogs.ItemsSource = sessions;
            }
        }
    }
}


