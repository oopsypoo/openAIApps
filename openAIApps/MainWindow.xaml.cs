using Microsoft.Win32;
using openAIApps.Data;
using openAIApps.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
        //public ObservableCollection<ChatSession> Sessions { get; } = new();
        public ObservableCollection<LogRowViewModel> LogRows { get; } = new();
        // Video tab source collection
        private VideoClient _videoClient;
        public ObservableCollection<VideoListItem> _videoHistory = new();
        public ObservableCollection<VideoListItem> VideoHistory => _videoHistory;
        public ObservableCollection<ChatMessage> CurrentVideoMessages { get; } = new();
        public ObservableCollection<ResponseAttachmentItem> PendingResponseAttachments { get; } = new();
        public ObservableCollection<DeveloperToolCallLogItem> DeveloperToolCallLogs { get; } = new();
        public VideoPanelState VideoState { get; } = new();

        /// <summary>
        /// Gets or sets the collection view that provides a filtered and sorted view of the log entries.
        /// </summary>
        public ICollectionView LogView { get; set; }
        public LogsPanelState LogsState { get; } = new();
        public ResponsesPanelState ResponsesState { get; } = new();

        private int? _activeResponsesSessionId;
        private int? _activeVideoSessionId;
        private System.Windows.Threading.DispatcherTimer _statusEllipsisTimer;
        private int _ellipsisCounter = 0;
        private AppStatus _appStatus;

        private void InitStatusAnimation()
        {
            _statusEllipsisTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _statusEllipsisTimer.Tick += (s, e) =>
            {
                _ellipsisCounter = (_ellipsisCounter + 1) % 4;
                if (ResponsesState.IsRequestInProgress)
                {
                    var baseText = StatusText.Text?.Split(new[] { '·', '.' }, StringSplitOptions.RemoveEmptyEntries)[0]?.Trim()
                                   ?? "Working";
                    _appStatus.Set(baseText + new string('.', _ellipsisCounter));
                }
            };

            ResponsesState.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ResponsesState.IsRequestInProgress))
                {
                    if (ResponsesState.IsRequestInProgress)
                    {
                        // start the animation
                        _ellipsisCounter = 0;
                        _statusEllipsisTimer.Start();
                    }
                    else
                    {
                        _statusEllipsisTimer.Stop();
                    }
                }
            };
        }


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
            var sessions = await _historyService.GetAllSessionsForLogsAsync();
            var rows = sessions.Select(BuildLogRow).ToList();
            ReplaceLogRows(rows);
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is not LogRowViewModel row)
                return false;

            bool matchesType =
                LogsState.TypeFilter == "All" ||
                string.Equals(row.Endpoint.ToString(), LogsState.TypeFilter, StringComparison.OrdinalIgnoreCase);

            string title = row.Title ?? string.Empty;
            bool matchesText =
                string.IsNullOrWhiteSpace(LogsState.SearchText) ||
                title.IndexOf(LogsState.SearchText, StringComparison.OrdinalIgnoreCase) >= 0;

            return matchesType && matchesText;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitStatusAnimation();
            await InitResponsesControlsAsync();
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            ApplyLogColumnVisibility();
            LoadInitialLogs();
            await EnsureResponsesWebViewInitializedAsync();
            await EnsureResponsesViewerPageLoadedAsync();
        }
        private void LogsState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LogsPanelState.SearchText) ||
                e.PropertyName == nameof(LogsPanelState.TypeFilter))
            {
                ApplyFilters();
                return;
            }

            if (e.PropertyName == nameof(LogsPanelState.ShowTurns) ||
                e.PropertyName == nameof(LogsPanelState.ShowMedia) ||
                e.PropertyName == nameof(LogsPanelState.ShowTools) ||
                e.PropertyName == nameof(LogsPanelState.ShowModel) ||
                e.PropertyName == nameof(LogsPanelState.ShowDev))
            {
                ApplyLogColumnVisibility();
            }
        }

        private void ResponsesState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
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
            _appStatus = new AppStatus(
                Dispatcher,
                text => StatusText.Text = text ?? string.Empty
            );
            AppDbContext.InitializeDatabase();
            _historyService = new HistoryService();
            _mediaStorageService = new MediaStorageService();
            _sessionCleanupService = new SessionCleanupService(_historyService, _mediaStorageService);

            LogView = CollectionViewSource.GetDefaultView(LogRows);
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
            // set it here to avoid whisper from trying to use it before it's set
            // whisper is collapsed, ubtil I fix it
            tabMain.SelectedItem = tpResponses;
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

        private void ReplaceLogRows(IEnumerable<LogRowViewModel> rows)
        {
            LogRows.Clear();

            if (rows != null)
            {
                foreach (var row in rows)
                {
                    LogRows.Add(row);
                }
            }

            LogView?.Refresh();
        }

        private static LogRowViewModel BuildLogRow(ChatSession session)
        {
            var messages = session.Messages ?? new List<ChatMessage>();

            int turns = messages.Count;

            var mediaFiles = messages
                .Where(m => m.MediaFiles != null)
                .SelectMany(m => m.MediaFiles)
                .ToList();

            string media = BuildMediaSummary(mediaFiles);
            string tools = BuildDistinctSummary(messages.Select(m => m.ActiveTools));
            string model = BuildDistinctSummary(messages.Select(m => m.ModelUsed));
            string dev = BuildDevSummary(messages);

            return new LogRowViewModel
            {
                SessionId = session.Id,
                Endpoint = session.Endpoint,
                Title = session.Title ?? string.Empty,
                CreatedAt = session.CreatedAt,
                LastUsedAt = session.LastUsedAt,
                Turns = turns,
                Media = media,
                Tools = tools,
                Dev = dev,
                Model = model,
                Session = session
            };
        }

        private static string BuildMediaSummary(IEnumerable<MediaFile> mediaFiles)
        {
            var files = mediaFiles?.ToList() ?? new List<MediaFile>();
            if (files.Count == 0)
                return "—";

            bool hasImage = files.Any(f =>
                !string.IsNullOrWhiteSpace(f.MediaType) &&
                f.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));

            bool hasVideo = files.Any(f =>
                !string.IsNullOrWhiteSpace(f.MediaType) &&
                f.MediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase));

            string label = hasImage && hasVideo
                ? "Mixed"
                : hasImage
                    ? "Image"
                    : hasVideo
                        ? "Video"
                        : "Media";

            return $"{label} ({files.Count})";
        }

        private static string BuildDistinctSummary(IEnumerable<string> values)
        {
            var items = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .SelectMany(v => v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .ToList();

            return items.Count == 0 ? "—" : string.Join(", ", items);
        }

        private void ApplyLogColumnVisibility()
        {
            if (colLogTurns != null)
                colLogTurns.Visibility = LogsState.ShowTurns ? Visibility.Visible : Visibility.Collapsed;

            if (colLogMedia != null)
                colLogMedia.Visibility = LogsState.ShowMedia ? Visibility.Visible : Visibility.Collapsed;

            if (colLogTools != null)
                colLogTools.Visibility = LogsState.ShowTools ? Visibility.Visible : Visibility.Collapsed;

            if (colLogModel != null)
                colLogModel.Visibility = LogsState.ShowModel ? Visibility.Visible : Visibility.Collapsed;
            if (colLogDev != null)
                colLogDev.Visibility = LogsState.ShowDev ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void RefreshLogsTab()
        {
            if (_historyService == null)
                return;

            var sessions = await _historyService.GetAllSessionsForLogsAsync();
            var rows = sessions.Select(BuildLogRow).ToList();

            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => ReplaceLogRows(rows));
            }
            else
            {
                ReplaceLogRows(rows);
            }
        }

        private async void OnLogEntryDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
                return;

            if (FindVisualParent<Button>(source) != null)
                return;

            var row = FindVisualParent<DataGridRow>(source);
            if (row?.Item is not LogRowViewModel selectedRow || selectedRow.Session == null)
                return;

            var selectedSession = selectedRow.Session;

            e.Handled = true;
            await OpenSessionFromLogsAsync(selectedSession);
            _appStatus.Set($"Opened session '{selectedSession.Title}'");
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
        private async void ClearDeletedSessionFromUi(ChatSession session)
        {
            if (session == null)
                return;

            if (session.Endpoint == EndpointType.Responses &&
                _activeResponsesSessionId == session.Id)
            {
                _activeResponsesSessionId = null;
                await ResetResponsesUi(clearPrompt: true);
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
            var row = (button?.CommandParameter ?? button?.DataContext) as LogRowViewModel;
            var session = row?.Session;

            if (session == null || row == null)
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
            // remove from UI
            LogRows.Remove(row);
            LogView?.Refresh();

            if (ReferenceEquals(LogsState.SelectedLogRow, row))
                LogsState.SelectedLogRow = null;

            _appStatus.Set($"Deleted session '{session.Title}'");
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
                _appStatus.Set("Loading EndpointType.Responses");
                await Dispatcher.InvokeAsync(() =>
                {
                    tabMain.SelectedItem = tpResponses;
                    tabMain.UpdateLayout();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                _appStatus.Set("Loading EndpointType.Responses finisdhed");
                _activeResponsesSessionId = selectedSession.Id;
                await LoadResponsesSessionAsync(selectedSession.Id);
            }
        }
        private async void OnExportLogMarkdownClick(object sender, RoutedEventArgs e)
        {
            await ExportSelectedLogAsync("md");
        }

        private async void OnExportLogTextClick(object sender, RoutedEventArgs e)
        {
            await ExportSelectedLogAsync("txt");
        }
        private async void OnExportLogHtmlClick(object sender, RoutedEventArgs e)
        {
            await ExportSelectedLogAsync("html");
        }
        private async Task ExportSelectedLogAsync(string format)
        {
            var selectedRow = LogsState.SelectedLogRow;
            if (selectedRow?.Session == null)
            {
                MessageBox.Show(
                    "Select a log row first.",
                    "Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var session = selectedRow.Session;
            var history = await _historyService.GetFullSessionHistoryAsync(session.Id);

            string extension =
                string.Equals(format, "txt", StringComparison.OrdinalIgnoreCase) ? "txt" :
                string.Equals(format, "html", StringComparison.OrdinalIgnoreCase) ? "html" :
                "md";

            var dialog = new SaveFileDialog
            {
                Title = "Export session",
                Filter = extension switch
                {
                    "html" => "HTML files (*.html)|*.html|Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    "txt" => "Text files (*.txt)|*.txt|Markdown files (*.md)|*.md|HTML files (*.html)|*.html|All files (*.*)|*.*",
                    _ => "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|HTML files (*.html)|*.html|All files (*.*)|*.*"
                },
                DefaultExt = "." + extension,
                FileName = BuildSafeExportFileName(session, extension)
            };

            if (dialog.ShowDialog(this) != true)
                return;

            string content = extension switch
            {
                "txt" => BuildSessionExportText(session, history),
                "html" => await BuildSessionExportHtmlAsync(session, history),
                _ => BuildSessionExportMarkdown(session, history)
            };

            await File.WriteAllTextAsync(dialog.FileName, content, Encoding.UTF8);
            _appStatus.Set($"Exported '{session.Title}' to {dialog.FileName}");
        }

        private static string BuildSafeExportFileName(ChatSession session, string extension)
        {
            string title = string.IsNullOrWhiteSpace(session.Title)
                ? $"session-{session.Id}"
                : session.Title;

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(c, '_');
            }

            if (title.Length > 80)
                title = title[..80];

            return $"{title}.{extension}";
        }

        private static string BuildSessionExportMarkdown(ChatSession session, IReadOnlyList<ChatMessage> history)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Session Export");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine($"- Session ID: {session.Id}");
            sb.AppendLine($"- Title: {EscapeMarkdownInline(session.Title)}");
            sb.AppendLine($"- Endpoint: {session.Endpoint}");
            sb.AppendLine($"- Created: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- Last Used: {session.LastUsedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- Messages: {history.Count}");

            var mediaFiles = history
                .Where(m => m.MediaFiles != null)
                .SelectMany(m => m.MediaFiles)
                .ToList();

            sb.AppendLine($"- Media: {BuildMediaSummary(mediaFiles)}");
            sb.AppendLine($"- Tools: {EscapeMarkdownInline(BuildDistinctSummary(history.Select(m => m.ActiveTools)))}");
            sb.AppendLine($"- Model: {EscapeMarkdownInline(BuildDistinctSummary(history.Select(m => m.ModelUsed)))}");
            sb.AppendLine($"- Developer Tools: {(SessionUsesDeveloperTools(history) ? "Yes" : "No")}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Messages");
            sb.AppendLine();

            foreach (var message in history)
            {
                sb.AppendLine($"### [{message.Timestamp:yyyy-MM-dd HH:mm:ss}] {EscapeMarkdownInline(message.Role)}");
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(message.Content))
                {
                    sb.AppendLine(message.Content);
                    sb.AppendLine();
                }

                sb.AppendLine("#### Metadata");
                sb.AppendLine();

                AppendMarkdownBullet(sb, "Model", message.ModelUsed);
                AppendMarkdownBullet(sb, "Tools", message.ActiveTools);
                sb.AppendLine($"- Developer Tools: {(HasDeveloperTools(message) ? "Yes" : "No")}");
                AppendMarkdownBullet(sb, "Reasoning", message.ReasoningLevel);
                AppendMarkdownBullet(sb, "Search Context Size", message.SearchContextSize);
                AppendMarkdownBullet(sb, "Image Size", message.ImageSize);
                AppendMarkdownBullet(sb, "Image Quality", message.ImageQuality);
                AppendMarkdownBullet(sb, "Video Length", message.VideoLength);
                AppendMarkdownBullet(sb, "Video Size", message.VideoSize);
                AppendMarkdownBullet(sb, "Remote ID", message.RemoteId);
                AppendMarkdownBullet(sb, "Source Remote ID", message.SourceRemoteId);

                sb.AppendLine($"- Remix: {(message.IsRemix ? "true" : "false")}");
                sb.AppendLine();

                if (message.MediaFiles != null && message.MediaFiles.Count > 0)
                {
                    sb.AppendLine("#### Media");
                    sb.AppendLine();

                    foreach (var media in message.MediaFiles)
                    {
                        sb.AppendLine($"- File: {EscapeMarkdownInline(media.FileName)}");
                        AppendMarkdownIndentedBullet(sb, "Media Type", media.MediaType);
                        AppendMarkdownIndentedBullet(sb, "Path", $"`{media.LocalPath}`");
                        AppendMarkdownIndentedBullet(sb, "Is Image", media.IsImage ? "true" : "false");
                    }

                    sb.AppendLine();
                }

                sb.AppendLine("#### Debug");
                sb.AppendLine();

                AppendMarkdownCodeBlockIfAny(sb, "RawJson", "json", message.RawJson);
                AppendMarkdownCodeBlockIfAny(sb, "DeveloperToolSettingsJson", "json", message.DeveloperToolSettingsJson);
                AppendMarkdownCodeBlockIfAny(sb, "ImageToolSettingsJson", "json", message.ImageToolSettingsJson);
                AppendMarkdownCodeBlockIfAny(sb, "ToolCallLogJson", "json", message.ToolCallLogJson);

                sb.AppendLine("---");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string BuildSessionExportText(ChatSession session, IReadOnlyList<ChatMessage> history)
        {
            var sb = new StringBuilder();

            sb.AppendLine("SESSION EXPORT");
            sb.AppendLine("==============");
            sb.AppendLine($"Session ID: {session.Id}");
            sb.AppendLine($"Title: {session.Title}");
            sb.AppendLine($"Endpoint: {session.Endpoint}");
            sb.AppendLine($"Created: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Last Used: {session.LastUsedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Messages: {history.Count}");
            sb.AppendLine($"Media: {BuildMediaSummary(history.Where(m => m.MediaFiles != null).SelectMany(m => m.MediaFiles))}");
            sb.AppendLine($"Tools: {BuildDistinctSummary(history.Select(m => m.ActiveTools))}");
            sb.AppendLine($"Model: {BuildDistinctSummary(history.Select(m => m.ModelUsed))}");
            sb.AppendLine($"Developer Tools: {(SessionUsesDeveloperTools(history) ? "Yes" : "No")}");
            sb.AppendLine();
            sb.AppendLine("MESSAGES");
            sb.AppendLine("--------");

            foreach (var message in history)
            {
                sb.AppendLine($"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}] {message.Role}");
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(message.Content))
                {
                    sb.AppendLine(message.Content);
                    sb.AppendLine();
                }

                sb.AppendLine("Metadata:");
                AppendTextLineIfAny(sb, "  Model", message.ModelUsed);
                AppendTextLineIfAny(sb, "  Tools", message.ActiveTools);
                sb.AppendLine($"  Developer Tools: {(HasDeveloperTools(message) ? "true" : "false")}");
                AppendTextLineIfAny(sb, "  Reasoning", message.ReasoningLevel);
                AppendTextLineIfAny(sb, "  Search Context Size", message.SearchContextSize);
                AppendTextLineIfAny(sb, "  Image Size", message.ImageSize);
                AppendTextLineIfAny(sb, "  Image Quality", message.ImageQuality);
                AppendTextLineIfAny(sb, "  Video Length", message.VideoLength);
                AppendTextLineIfAny(sb, "  Video Size", message.VideoSize);
                AppendTextLineIfAny(sb, "  Remote ID", message.RemoteId);
                AppendTextLineIfAny(sb, "  Source Remote ID", message.SourceRemoteId);
                sb.AppendLine($"  Remix: {(message.IsRemix ? "true" : "false")}");
                sb.AppendLine();

                if (message.MediaFiles != null && message.MediaFiles.Count > 0)
                {
                    sb.AppendLine("Media:");
                    foreach (var media in message.MediaFiles)
                    {
                        sb.AppendLine($"  - File: {media.FileName}");
                        AppendTextLineIfAny(sb, "    Media Type", media.MediaType);
                        AppendTextLineIfAny(sb, "    Path", media.LocalPath);
                        sb.AppendLine($"    Is Image: {(media.IsImage ? "true" : "false")}");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("Debug:");
                AppendTextBlockIfAny(sb, "RawJson", message.RawJson);
                AppendTextBlockIfAny(sb, "DeveloperToolSettingsJson", message.DeveloperToolSettingsJson);
                AppendTextBlockIfAny(sb, "ImageToolSettingsJson", message.ImageToolSettingsJson);
                AppendTextBlockIfAny(sb, "ToolCallLogJson", message.ToolCallLogJson);

                sb.AppendLine("------------------------------------------------------------");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        private static void AppendMarkdownBullet(StringBuilder sb, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                sb.AppendLine($"- {label}: {EscapeMarkdownInline(value)}");
        }

        private static void AppendMarkdownIndentedBullet(StringBuilder sb, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                sb.AppendLine($"  - {label}: {value}");
        }

        private static void AppendMarkdownCodeBlockIfAny(StringBuilder sb, string label, string language, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            sb.AppendLine($"##### {label}");
            sb.AppendLine();
            sb.AppendLine($"```{language}");
            sb.AppendLine(value);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        private async Task<string> BuildSessionExportHtmlAsync(ChatSession session, IReadOnlyList<ChatMessage> history)
        {
            string markdown = BuildSessionExportMarkdown(session, history);

            string bodyHtml = ConvertMarkdownToHtml(markdown);
            string css = await TryLoadMarkdownViewerCssAsync();

            return $"""
                    <!DOCTYPE html>
                    <html lang="en">
                    <head>
                        <meta charset="utf-8" />
                        <title>{System.Net.WebUtility.HtmlEncode(session.Title ?? $"Session {session.Id}")}</title>
                        <style>
                    {css}
                        </style>
                    </head>
                    <body>
                        <main class="markdown-body">
                    {bodyHtml}
                        </main>
                    </body>
                    </html>
                    """;
        }
        private async Task<string> TryLoadMarkdownViewerCssAsync()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string cssPath = Path.Combine(baseDir, "Assets", "MarkdownViewer", "markdown.css");

                if (File.Exists(cssPath))
                    return await File.ReadAllTextAsync(cssPath);

                return """
                        body {
                            font-family: Segoe UI, Arial, sans-serif;
                            margin: 24px;
                            background: #ffffff;
                            color: #222222;
                        }
                        .markdown-body {
                            max-width: 1100px;
                            margin: 0 auto;
                        }
                        pre {
                            white-space: pre-wrap;
                            word-wrap: break-word;
                            background: #f6f8fa;
                            padding: 12px;
                            border-radius: 6px;
                        }
                        code {
                            font-family: Consolas, monospace;
                        }
                        """;
            }
            catch
            {
                return """
                        body {
                            font-family: Segoe UI, Arial, sans-serif;
                            margin: 24px;
                        }
                        pre {
                            white-space: pre-wrap;
                        }
                        """;
            }
        }
        private static string ConvertMarkdownToHtml(string markdown)
        {
            // TODO:
            // Replace this with the same markdown pipeline used by your MarkdownViewer assets/page,
            // for example Markdig if already referenced in the project.

            string encoded = System.Net.WebUtility.HtmlEncode(markdown);
            return $"<pre>{encoded}</pre>";
        }
        private static void AppendTextLineIfAny(StringBuilder sb, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                sb.AppendLine($"{label}: {value}");
        }

        private static void AppendTextBlockIfAny(StringBuilder sb, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            sb.AppendLine($"{label}:");
            sb.AppendLine(value);
            sb.AppendLine();
        }

        private static string EscapeMarkdownInline(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("`", "\\`")
                .Replace("*", "\\*")
                .Replace("_", "\\_");
        }
        private static bool HasDeveloperTools(ChatMessage message)
        {
            return IsDeveloperToolsEnabled(message.DeveloperToolSettingsJson);
        }

        private static bool SessionUsesDeveloperTools(IEnumerable<ChatMessage> history)
        {
            return history.Any(m => IsDeveloperToolsEnabled(m.DeveloperToolSettingsJson));
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
        private static string BuildDevSummary(IEnumerable<ChatMessage> messages)
        {
            return messages.Any(m => IsDeveloperToolsEnabled(m.DeveloperToolSettingsJson))
                ? "Yes"
                : "No";
        }
        // This method checks if the developer tools were enabled for a given message by
        // inspecting the DeveloperToolSettingsJson property.
        private static bool IsDeveloperToolsEnabled(string? developerToolSettingsJson)
        {// If the JSON is null or whitespace, we can assume developer tools were not enabled.
            if (string.IsNullOrWhiteSpace(developerToolSettingsJson))
                return false;

            try
            {// Attempt to deserialize the JSON into a DeveloperToolSettingsSnapshot object.
                var snapshot = JsonSerializer.Deserialize<DeveloperToolSettingsSnapshot>(developerToolSettingsJson);
                return snapshot?.Enabled == true;
            }
            catch
            {// If deserialization fails for any reason, we will assume developer tools were not enabled.
                return false;
            }
        }
    }
}