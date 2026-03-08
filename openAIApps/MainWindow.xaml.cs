using Microsoft.EntityFrameworkCore;
using NAudio.Utils;
using openAIApps.Data;
using openAIApps.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
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
        public ICollectionView LogView { get; set; }
        // This is the master list from the DB
        private List<ChatSession> _allSessions { get; } = new();
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

        private async void LoadInitialLogs()
        {
            var sessions = await _historyService.GetAllSessionsAsync();

            _allSessions.Clear();
            foreach (var s in sessions)
                _allSessions.Add(s);

            // Not strictly necessary since we're adding to ObservableCollection,
            // but harmless if you want to re-evaluate filters after load:
            LogView.Refresh();
        }
        private bool FilterPredicate(object obj)
        {
            if (obj is not ChatSession session) return false;

            var typeFilter = (cbLogTypeFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            var query = txtLogSearch?.Text?.Trim() ?? string.Empty;

            bool matchesType = typeFilter == "All" ||
                               string.Equals(session.Endpoint.ToString(), typeFilter, StringComparison.OrdinalIgnoreCase);

            var title = session.Title ?? string.Empty;
            bool matchesText = string.IsNullOrEmpty(query) ||
                               title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

            return matchesType && matchesText;
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitResponsesControls(); // Move ALL combo population here
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            LoadInitialLogs(); // Load logs after everything is set up
        }
        public MainWindow()
        {
            // Set up the Logs tab with filtering
            LogView = CollectionViewSource.GetDefaultView(_allSessions);
            
            InitializeComponent();
            _context = new AppDbContext();
            _context.Database.EnsureCreated(); // Ensures SQLite file exists
            _historyService = new HistoryService(_context);
            // Pass history service to your existing logic classes if needed
            //_responsesClient.SetHistoryService(_historyService);
            LogView.Filter = FilterPredicate;
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
        private void UpdateSessions(IEnumerable<ChatSession> sessions)
        {
            using (LogView.DeferRefresh())
            {
                _allSessions.Clear();
                foreach (var s in sessions)
                    _allSessions.Add(s);
            }
            // If your Filter predicate reads UI controls (search/type), this ensures it re-evaluates
            LogView.Refresh();
        }
        // Call this when clicking the "Logs" tab
        private async void RefreshLogsTab()
        {
            if(_historyService == null) return; // Safety check
            var sessions = await _historyService.GetRecentSessionsAsync();

            // Update the collection that the view wraps
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => UpdateSessions(sessions));
            }
            else
            {
                UpdateSessions(sessions);
            }
        }
        
        private async void OnLogEntryDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Now that IsSynchronizedWithCurrentItem="True" is set, 
            // SelectedItem is rock-solid.
            var selectedSession = dgUnifiedLogs.CurrentItem as ChatSession;
            if (selectedSession == null)
                return;

            // Isolate context to prevent "cross-tab logging"
            _activeResponsesSessionId = null;
            _activeVideoSessionId = null;

            if (selectedSession.Endpoint == EndpointType.Video)
            {
                tabMain.SelectedIndex = 1; // Switch to Video Tab
                _activeVideoSessionId = selectedSession.Id;

                var history = await _historyService.GetFullSessionHistoryAsync(selectedSession.Id);

                var userMsg = history.FirstOrDefault(m => m.Role.ToLower() == "user");
                if (userMsg != null)
                {
                    txtVideoPrompt.Text = userMsg.Content;
                    cmbVideoModel.Text = userMsg.ModelUsed;
                    cmbVideoLength.Text = userMsg.VideoLength;
                    cmbVideoSize.Text = userMsg.VideoSize;
                    cbVideoRemix.IsChecked = userMsg.IsRemix;
                }

                var assistantMsg = history.LastOrDefault(m => m.Role.ToLower() == "assistant");
                if (assistantMsg != null && !string.IsNullOrEmpty(assistantMsg.RemoteId))
                {
                    var itemToSelect = _videoHistory.FirstOrDefault(v => v.Id == assistantMsg.RemoteId);
                    if (itemToSelect != null)
                    {
                        lstVideoFiles.SelectedItem = itemToSelect;
                        lstVideoFiles.ScrollIntoView(itemToSelect);
                    }
                }
            }
            else if (selectedSession.Endpoint == EndpointType.Responses)
            {
                tabMain.SelectedIndex = 2; // Switch to Responses Tab
                _activeResponsesSessionId = selectedSession.Id;
                await RefreshChatUI(selectedSession.Id);
            }
            
        }
        private async Task RefreshChatUI(int sessionId)
        {
            // 1. Fetch the full message history for this session from the DB
            var history = await _historyService.GetFullSessionHistoryAsync(sessionId);

            // 2. Bind the history to your ListBox
            lstResponsesTurns.ItemsSource = history;

            // 3. Auto-scroll to the bottom so the user sees the latest exchange
            if (lstResponsesTurns.Items.Count > 0)
            {
                var lastMsg = history.Last();
                lstResponsesTurns.ScrollIntoView(lastMsg);
            }

            // 4. Update the prompt textbox with the last user message to allow easy editing/resending
            var lastUserMsg = history.LastOrDefault(m => m.Role.ToLower() == "user");
            if (lastUserMsg != null)
            {
                txtResponsesPrompt.Text = lastUserMsg.Content;
            }
        }
        private void tabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshLogsTab();
            
        }
        // 1. Delete Session
        private async void OnDeleteSessionClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            // Prefer CommandParameter if set, otherwise fall back to DataContext
            var session = (button?.CommandParameter ?? button?.DataContext) as ChatSession;

            if (session == null) return;

            var confirm = MessageBox.Show($"Permanently delete '{session.Title}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                await _historyService.DeleteSessionAsync(session.Id);
                ApplyFilters(); // Refresh the grid
            }
        }


        
        private async void ApplyFilters()
        {
            // This "jolts" the UI to redraw based on the new filter rules
            if(LogView != null)
                LogView.Refresh();
        }
        
        private void TxtLogSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void OnLogFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void dgUnifiedLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        public void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine("Preview mouse hit button");
        }
    }
}









