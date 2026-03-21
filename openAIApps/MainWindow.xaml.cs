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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        readonly string OpenAPIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        private AppSettings _settings;
        private string savepath_logs;
        private string savepath_snds;
        private string savepath_images;
        private string savepath_videos;

        public event Action<List<string>>? ModelsApplied;
        
        public static HttpResponseMessage GlobalhttpResponse = new HttpResponseMessage();

        
        private string _responsesImagePath = string.Empty;
        private string _videoReferencePath = string.Empty;
        private string _responsesPreviewImagePath = string.Empty;
        public ObservableCollection<MediaFile> ResponsePreviewImages { get; } = new();
        
        private Responses _responsesClient;

        private readonly HistoryService _historyService;
        private readonly MediaStorageService _mediaStorageService;
        private readonly SessionCleanupService _sessionCleanupService;
        // Responses tab source collection
        public ObservableCollection<ChatMessage> CurrentChatMessages { get; } = new();

        // Logs tab source collection
        public ObservableCollection<ChatSession> Sessions { get; } = new();
        // Video tab source collection
        private VideoClient _videoClient;
        public ObservableCollection<VideoListItem> _videoHistory = new();
        public ObservableCollection<VideoListItem> VideoHistory => _videoHistory;
        public ObservableCollection<ChatMessage> CurrentVideoMessages { get; } = new();
        public ObservableCollection<ResponseAttachmentItem> PendingResponseAttachments { get; } = new();
        
        public VideoPanelState VideoState { get; } = new();

        /// <summary>
        /// Gets or sets the collection view that provides a filtered and sorted view of the log entries.
        /// </summary>
        public ICollectionView LogView { get; set; }
        public LogsPanelState LogsState { get; } = new();
        public ResponsesPanelState ResponsesState { get; } = new();

        private int? _activeResponsesSessionId;
        private int? _activeVideoSessionId;

        

        private async Task<int> EnsureSessionActiveAsync(EndpointType type, string firstPrompt)
        {
            if (type == EndpointType.Responses && _activeResponsesSessionId == null)
            {
                _activeResponsesSessionId = await _historyService.StartNewSessionAsync(ExtractTitle(firstPrompt), type);
            }
            else if (type == EndpointType.Video && _activeVideoSessionId == null)
            {
                _activeVideoSessionId = await _historyService.StartNewSessionAsync(ExtractTitle(firstPrompt), type);
            }

            return (type == EndpointType.Responses ? _activeResponsesSessionId : _activeVideoSessionId)!.Value;
        }

        private string ExtractTitle(string prompt)
        {
            prompt = prompt ?? string.Empty;
            prompt = prompt.Trim();

            if (string.IsNullOrWhiteSpace(prompt))
                return "Image prompt";

            return prompt.Length > 60
                ? $"{prompt[..60]}..."
                : prompt;
        }

        private void EnsureSavePaths()
        {
            _settings ??= AppSettings.LoadSettings();

            savepath_logs = Path.Combine(_settings.AppRoot, _settings.LogsFolder);
            savepath_snds = Path.Combine(_settings.AppRoot, _settings.SoundsFolder);
            savepath_images = Path.Combine(_settings.AppRoot, _settings.ImagesFolder);
            savepath_videos = Path.Combine(_settings.AppRoot, _settings.VideosFolder);

            Directory.CreateDirectory(savepath_logs);
            Directory.CreateDirectory(savepath_snds);
            Directory.CreateDirectory(savepath_images);
            Directory.CreateDirectory(savepath_videos);

            _mediaStorageService?.SetImagesFolder(savepath_images);
        }
        
        private async void LoadInitialLogs()
        {
            var sessions = await _historyService.GetAllSessionsAsync();
            ReplaceSessions(sessions);
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is not ChatSession session)
                return false;

            bool matchesType =
                LogsState.TypeFilter == "All" ||
                string.Equals(session.Endpoint.ToString(), LogsState.TypeFilter, StringComparison.OrdinalIgnoreCase);

            string title = session.Title ?? string.Empty;
            bool matchesText =
                string.IsNullOrWhiteSpace(LogsState.SearchText) ||
                title.IndexOf(LogsState.SearchText, StringComparison.OrdinalIgnoreCase) >= 0;

            return matchesType && matchesText;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitResponsesControlsAsync();
            UpdateResponsesResponseDocument(ResponsesState.ResponseText);
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            LoadInitialLogs();
        }
        private void LogsState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LogsPanelState.SearchText) ||
                e.PropertyName == nameof(LogsPanelState.TypeFilter))
            {
                ApplyFilters();
            }
        }

        private void ResponsesState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ResponsesPanelState.ResponseText))
            {
                UpdateResponsesResponseDocument(ResponsesState.ResponseText);
                return;
            }

            if (_responsesClient == null || _isApplyingResponsesSettings)
                return;

            switch (e.PropertyName)
            {
                case nameof(ResponsesPanelState.SelectedModel):
                case nameof(ResponsesPanelState.SelectedReasoning):
                case nameof(ResponsesPanelState.SearchContextSize):
                case nameof(ResponsesPanelState.ImageGenQuality):
                case nameof(ResponsesPanelState.ImageGenSize):
                case nameof(ResponsesPanelState.ImageGenOutputFormat):
                case nameof(ResponsesPanelState.ImageGenOutputCompression):
                case nameof(ResponsesPanelState.ImageGenBackground):
                case nameof(ResponsesPanelState.ImageGenInputFidelity):
                case nameof(ResponsesPanelState.UseTextTool):
                case nameof(ResponsesPanelState.UseWebSearch):
                case nameof(ResponsesPanelState.UseComputerUse):
                case nameof(ResponsesPanelState.UseImageGeneration):
                    ValidateResponsesState();
                    NormalizeResponsesToolsState();
                    ValidateImageGenerationSettings();
                    ApplyResponsesStateToClient();
                    break;
            }
        }
        public MainWindow()
        {
            InitializeComponent();

            AppDbContext.InitializeDatabase();
            _historyService = new HistoryService();
            _mediaStorageService = new MediaStorageService();
            _sessionCleanupService = new SessionCleanupService(_historyService, _mediaStorageService);

            LogView = CollectionViewSource.GetDefaultView(Sessions);
            LogView.Filter = FilterPredicate;

            LogsState.PropertyChanged += LogsState_PropertyChanged;
            ResponsesState.PropertyChanged += ResponsesState_PropertyChanged;

            InitControls();
            Loaded += MainWindow_Loaded;
        }

        public void InitControls()
        {
            EnsureSavePaths();
            _videoClient = new VideoClient(apiKey: OpenAPIKey);
            InitVideoState();
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
            if (_availableModelsWindow != null && _availableModelsWindow.IsLoaded)
            {
                _availableModelsWindow.Activate();
                return;
            }

            if (_allModelsFromApi.Count == 0)
            {
                try
                {
                    _allModelsFromApi = await ModelApiService.GetAvailableModelsAsync(OpenAPIKey);
                }
                catch
                {
                    _allModelsFromApi = GetHardcodedResponseModels();
                }
            }

            _availableModelsWindow = new AvailableModels(_allModelsFromApi, _activeModelsForResponses);
            _availableModelsWindow.Owner = this;
            _availableModelsWindow.ModelsApplied += AvailableModelsWindow_ModelsApplied;
            _availableModelsWindow.Closed += AvailableModelsWindow_Closed;
            _availableModelsWindow.Show();
        }

        private void AvailableModelsWindow_ModelsApplied(List<string> models)
        {
            _activeModelsForResponses = models.ToList();
            ApplyModelsToResponsesCombo(_activeModelsForResponses, "gpt-4o");
        }

        private void AvailableModelsWindow_Closed(object? sender, EventArgs e)
        {
            if (_availableModelsWindow != null)
            {
                _availableModelsWindow.ModelsApplied -= AvailableModelsWindow_ModelsApplied;
                _availableModelsWindow.Closed -= AvailableModelsWindow_Closed;
                _availableModelsWindow = null;
            }
        }

        private void menuSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow(_settings);
            bool? result = window.ShowDialog();
            if (result == true)
            {
                EnsureSavePaths();
            }
        }

        private void ReplaceSessions(IEnumerable<ChatSession> sessions)
        {
            Sessions.Clear();

            if (sessions != null)
            {
                foreach (var session in sessions)
                {
                    Sessions.Add(session);
                }
            }

            LogView?.Refresh();
        }

        private async void RefreshLogsTab()
        {
            if (_historyService == null)
                return;

            var sessions = await _historyService.GetAllSessionsAsync();

            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => ReplaceSessions(sessions));
            }
            else
            {
                ReplaceSessions(sessions);
            }
        }

        private async void OnLogEntryDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
                return;

            // Ignore double-clicks on the delete button
            if (FindVisualParent<Button>(source) != null)
                return;

            var row = FindVisualParent<DataGridRow>(source);
            if (row?.Item is not ChatSession selectedSession)
                return;

            e.Handled = true;
            await OpenSessionFromLogsAsync(selectedSession);
        }

        private void tabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, tabMain))
                return;

            if (tabMain.SelectedItem == tabLogs)
            {
                RefreshLogsTab();
            }
        }
        private void ClearDeletedSessionFromUi(ChatSession session)
        {
            if (session == null)
                return;

            if (session.Endpoint == EndpointType.Responses &&
                _activeResponsesSessionId == session.Id)
            {
                _activeResponsesSessionId = null;
                ResetResponsesUi(clearPrompt: true);
            }

            if (session.Endpoint == EndpointType.Video &&
                _activeVideoSessionId == session.Id)
            {
                ResetVideoUI();
            }
        }
        private async void OnDeleteSessionClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var session = (button?.CommandParameter ?? button?.DataContext) as ChatSession;

            if (session == null)
                return;

            var confirm = MessageBox.Show(
                $"Permanently delete '{session.Title}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            await _sessionCleanupService.DeleteSessionAsync(session.Id);
            ClearDeletedSessionFromUi(session);
            RefreshLogsTab();
        }

        private void ApplyFilters()
        {
            LogView?.Refresh();
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }
        private async Task OpenSessionFromLogsAsync(ChatSession selectedSession)
        {
            if (selectedSession == null)
                return;

            _activeResponsesSessionId = null;
            _activeVideoSessionId = null;

            if (selectedSession.Endpoint == EndpointType.Video)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    tabMain.SelectedItem = tpVideo;
                    tabMain.UpdateLayout();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                await LoadVideoSessionAsync(selectedSession.Id);
            }
            else if (selectedSession.Endpoint == EndpointType.Responses)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    tabMain.SelectedItem = tpResponses;
                    tabMain.UpdateLayout();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                _activeResponsesSessionId = selectedSession.Id;
                await LoadResponsesSessionAsync(selectedSession.Id);
            }
        }
        private void SelectAllTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb && !string.IsNullOrEmpty(tb.Text))
            {
                tb.SelectAll();
            }
        }

        private void SelectAllTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox tb)
                return;

            if (!tb.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                tb.Focus();
            }
        }
    }
}