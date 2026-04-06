using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using openAIApps.Data;
using openAIApps.Native;
using openAIApps.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
/*
 * Code for Responses-tab-controls
*/

namespace openAIApps
{
    public partial class MainWindow
    {
        private List<string> _allModelsFromApi = new();
        private List<string> _activeModelsForResponses = new();
        private AvailableModels? _availableModelsWindow;
        private bool _isApplyingResponsesSettings;
        private bool _responsesWebViewInitialized;
        private bool _responsesViewerPageLoaded;

        private bool _bindingMarkdownThemeOptions;
        private bool _bindingPageThemeOptions;
        private List<MarkdownThemeOption> _markdownThemeOptions = new();
        private MarkdownThemeOption? _selectedMarkdownTheme;
        private List<PageThemeOption> _pageThemeOptions = new();
        private PageThemeOption? _selectedPageTheme;
        
        private string ApplyModelsToResponsesCombo(IEnumerable<string> models, string preferredModel = "gpt-4o")
        {
            var list = models
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _activeModelsForResponses = list;

            cmbResponsesModel.Items.Clear();

            foreach (string model in list)
            {
                cmbResponsesModel.Items.Add(new ComboBoxItem
                {
                    Content = model,
                    Tag = model
                });
            }

            if (list.Count == 0)
            {
                return string.Empty;
            }

            string selectedModel =
                list.FirstOrDefault(m => m.Equals(_responsesClient.CurrentModel, StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(m => m.Equals(preferredModel, StringComparison.OrdinalIgnoreCase))
                ?? list[0];

            int selectedIndex = list.FindIndex(m => m.Equals(selectedModel, StringComparison.OrdinalIgnoreCase));
            cmbResponsesModel.SelectedIndex = selectedIndex;

            return selectedModel;
        }
        /// <summary>
        /// Retrieves a predefined list of response model identifiers available for use within the application.
        /// </summary>
        /// <remarks>The returned model identifiers include both reasoning-enabled and non-reasoning
        /// variants to support a range of use cases. This method is intended for scenarios where a fixed set of
        /// supported models is required, such as populating selection controls or validating user input.</remarks>
        /// <returns>A list of strings containing the identifiers of supported response models, including variants from the GPT-5
        /// and GPT-4 families as well as dedicated reasoning models.</returns>
        private List<string> GetHardcodedResponseModels()
        {
            return new List<string>
            {
                // Frontier GPT-5 family (reasoning-enabled)
                "gpt-5.2",
                "gpt-5.2-pro",
                "gpt-5.1",
                "gpt-5-pro",
                "gpt-5-mini",
                // GPT-4.1 family (non-reasoning)
                "gpt-4.1",
                "gpt-4.1-mini",
                // GPT-4o series
                "gpt-4o",
                "gpt-4o-mini",
                // Dedicated reasoning models (o-series)
                "o3",
                "o3-pro",
                "o3-mini",
                "o4-mini",
                "computer-use-preview"
            };
        }
        private async Task LoadApiModelsCacheAsync()
        {
            try
            {
                _allModelsFromApi = await ModelApiService.GetAvailableModelsAsync(OpenAPIKey);
            }
            catch
            {
                // Ignore - fallback already exists
            }
        }
        private async Task InitResponsesControlsAsync()
        {
            _responsesClient = new Responses(OpenAPIKey);

            List<string> modelsToUse = AvailableModelsStorage.Load();

            if (modelsToUse.Count == 0)
            {
                try
                {
                    _allModelsFromApi = await ModelApiService.GetAvailableModelsAsync(OpenAPIKey);
                    modelsToUse = _allModelsFromApi;
                }
                catch
                {
                    modelsToUse = GetHardcodedResponseModels();
                }
            }
            else
            {
                // Optional: load API list in background so AvailableModels window can use full live list later
                _ = LoadApiModelsCacheAsync();
            }

            if (modelsToUse.Count == 0)
            {
                modelsToUse = GetHardcodedResponseModels();
            }

            ApplyModelsToResponsesCombo(modelsToUse, "gpt-4o");

            ResponsesState.SelectedModel = ApplyModelsToResponsesCombo(modelsToUse, "gpt-4o");
            ResponsesState.SelectedReasoning = "none";
            ResponsesState.UseTextTool = true;
            ResponsesState.UseWebSearch = false;
            ResponsesState.UseComputerUse = false;
            ResponsesState.UseImageGeneration = false;
            ResponsesState.SearchContextSize = "medium";
            ResponsesState.ImageGenQuality = "auto";
            ResponsesState.ImageGenSize = "auto";
            ResponsesState.ImageGenOutputFormat = "jpeg";
            ResponsesState.ImageGenOutputCompression = 85;
            ResponsesState.ImageGenBackground = "auto";
            ResponsesState.ImageGenInputFidelity = "high";
            //--------Developer function-tool-calls-----------
            ResponsesState.UseDeveloperTools = false;
            ResponsesState.DeveloperRepositoryRoot = string.Empty;
            ResponsesState.DeveloperScope = "repository";
            ResponsesState.DeveloperAllowReadOnlyOnly = true;
            ResponsesState.DeveloperRequireConfirmation = false;
            ResponsesState.DeveloperShowToolLogs = true;
            ResponsesState.DeveloperToolSearchProjectText = true;
            ResponsesState.DeveloperToolReadProjectFile = true;
            ResponsesState.DeveloperToolListProjectFiles = false;
            ResponsesState.DeveloperToolRunDiagnostics = false;
            ResponsesState.DeveloperAllowedExtensionsCsv = ResponsesPanelState.GetDefaultAllowedExtensionsCsv();
            ApplyResponsesStateToClient();
        }
        private ChatMessage GetSelectedResponseMessage()
        {
            return ResponsesState.SelectedTurn;
        }

        private string GetPrimaryAttachmentPath(ChatMessage message)
        {
            return message?.MediaFiles?
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.LocalPath))
                ?.LocalPath;
        }

        private static bool IsImageMediaFile(MediaFile media)
        {
            return media != null &&
                   !string.IsNullOrWhiteSpace(media.LocalPath) &&
                   File.Exists(media.LocalPath) &&
                   !string.IsNullOrWhiteSpace(media.MediaType) &&
                   media.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        private string GetPrimaryImagePath(ChatMessage message)
        {
            return message?.MediaFiles?
                .FirstOrDefault(IsImageMediaFile)?
                .LocalPath;
        }

        private List<MediaFile> GetImageMediaFiles(ChatMessage message)
        {
            return message?.MediaFiles?
                .Where(IsImageMediaFile)
                .ToList()
                ?? new List<MediaFile>();
        }

        private void ShowResponsesImagePreview(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                HideResponsesImagePreview();
                return;
            }

            imgResponsesPreview.Source = GetImageSource(path);
            _responsesPreviewImagePath = path;

            borderResponsesImage.Visibility = Visibility.Visible;
            colResponsesPrompt.Width = new GridLength(2, GridUnitType.Star);
            colResponsesImage.Width = new GridLength(1, GridUnitType.Star);
        }

        private void HideResponsesImagePreview()
        {
            imgResponsesPreview.Source = null;
            _responsesPreviewImagePath = string.Empty;

            borderResponsesImage.Visibility = Visibility.Collapsed;
            colResponsesPrompt.Width = new GridLength(1, GridUnitType.Star);
            colResponsesImage.Width = new GridLength(0);
        }
        private void ReplaceResponsePreviewImages(IEnumerable<MediaFile> images)
        {
            ResponsePreviewImages.Clear();

            if (images == null)
                return;

            foreach (var image in images)
            {
                ResponsePreviewImages.Add(image);
            }
        }

        private void ClearResponsePreviewImages()
        {
            ResponsePreviewImages.Clear();

            if (lstResponsesImages != null)
                lstResponsesImages.SelectedItem = null;

            if (txtResponsesPreviewInfo != null)
                txtResponsesPreviewInfo.Text = string.Empty;
        }

        private void UpdateResponsesPreviewInfo()
        {
            if (txtResponsesPreviewInfo == null)
                return;

            if (ResponsePreviewImages.Count == 0)
            {
                txtResponsesPreviewInfo.Text = string.Empty;
                return;
            }

            if (lstResponsesImages?.SelectedItem is MediaFile selected)
            {
                int index = ResponsePreviewImages.IndexOf(selected);
                txtResponsesPreviewInfo.Text = index >= 0
                    ? $"{index + 1} / {ResponsePreviewImages.Count}"
                    : string.Empty;
                return;
            }

            txtResponsesPreviewInfo.Text = $"1 / {ResponsePreviewImages.Count}";
        }

        private void ShowResponsesImageGallery(ChatMessage message, string preferredPath = null)
        {
            var images = GetImageMediaFiles(message);
            ReplaceResponsePreviewImages(images);

            if (images.Count == 0)
            {
                ClearResponsePreviewImages();
                HideResponsesImagePreview();
                return;
            }

            MediaFile selected =
                !string.IsNullOrWhiteSpace(preferredPath)
                    ? images.FirstOrDefault(m =>
                        string.Equals(m.LocalPath, preferredPath, StringComparison.OrdinalIgnoreCase))
                    : null;

            selected ??= images[0];

            if (lstResponsesImages != null)
                lstResponsesImages.SelectedItem = selected;

            ShowResponsesImagePreview(selected.LocalPath);
            UpdateResponsesPreviewInfo();
        }

        private void ReplaceCurrentChatMessages(IEnumerable<ChatMessage> history)
        {
            CurrentChatMessages.Clear();

            foreach (var message in history)
            {
                CurrentChatMessages.Add(message);
            }
        }

        private async Task LoadResponsesSessionAsync(int sessionId, bool restoreLastUserPrompt = true)
        {
            if (sessionId <= 0)
                return;

            PendingResponseAttachments.Clear();
            _appStatus.Set("Update attachment panel...");
            UpdatePendingAttachmentsPanel();
            _appStatus.Set("Loading session history...");
            var history = await _historyService.GetFullSessionHistoryAsync(sessionId);

            ReplaceCurrentChatMessages(history);
            _appStatus.Set($"Rendering {history.Count} messages...");
            ApplyResponsesSettingsFromHistory(history);
            ClearResponsePreviewImages();
            ClearDeveloperToolCallLogs();
            if (CurrentChatMessages.Count > 0)
            {
                var lastMessage = CurrentChatMessages.Last();
                ResponsesState.SelectedTurn = lastMessage;
                lstResponsesTurns.ScrollIntoView(lastMessage);
            }
            else
            {
                ResponsesState.SelectedTurn = null;
            }

            if (restoreLastUserPrompt)
            {
                var lastUserMessage = history.LastOrDefault(m =>
                    string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

                if (lastUserMessage != null)
                {
                    ResponsesState.PromptText = lastUserMessage.Content;
                }
            }

            if (ResponsesState.SelectedTurn is ChatMessage selected)
            {
                if (string.Equals(selected.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    await RenderResponsesMarkdownAsync(selected.Content);
                    ApplyDeveloperToolCallLogJson(selected.ToolCallLogJson);
                }
                else
                {
                    await RenderResponsesMarkdownAsync(string.Empty);
                    ClearDeveloperToolCallLogs();
                }
                _appStatus.Set("Showing Image gallery ");
                ShowResponsesImageGallery(selected);
            }
            else
            {
                await RenderResponsesMarkdownAsync(string.Empty);
                ClearResponsePreviewImages();
                HideResponsesImagePreview();
                DeveloperToolCallLogs.Clear();
                ClearDeveloperToolCallLogs();
            }
        }

        private async Task ResetResponsesUi(bool clearPrompt = true)
        {
            CurrentChatMessages.Clear();
            ResponsesState.SelectedTurn = null;
            
            if (clearPrompt)
                ResponsesState.PromptText = string.Empty;

            _responsesImagePath = string.Empty;
            _responsesPreviewImagePath = string.Empty;
            ClearResponsePreviewImages();
            ClearDeveloperToolCallLogs();
            HideResponsesImagePreview();
            ResetDeveloperToolsState();
            await RenderResponsesMarkdownAsync(string.Empty);
            if (_responsesClient != null)
                _responsesClient.ClearConversation();
        }
        private void ResetDeveloperToolsState()
        {
            ResponsesState.UseDeveloperTools = false;
            ResponsesState.DeveloperRepositoryRoot = string.Empty;
            ResponsesState.DeveloperScope = "repository";
            ResponsesState.DeveloperAllowReadOnlyOnly = true;
            ResponsesState.DeveloperRequireConfirmation = false;
            ResponsesState.DeveloperShowToolLogs = true;
            ResponsesState.DeveloperToolSearchProjectText = true;
            ResponsesState.DeveloperToolReadProjectFile = true;
            ResponsesState.DeveloperToolListProjectFiles = false;
            ResponsesState.DeveloperToolRunDiagnostics = false;
            ResponsesState.DeveloperAllowedExtensionsCsv =
                ResponsesPanelState.GetDefaultAllowedExtensionsCsv();
        }
        private void UpdatePendingAttachmentsPanel()
        {
            if (borderResponsesAttachments != null)
            {
                borderResponsesAttachments.Visibility =
                    PendingResponseAttachments.Count > 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }
        }

        private void AddPendingResponseAttachments(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
                return;

            ResponseAttachmentItem lastAddedImage = null;

            foreach (string filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    continue;

                bool alreadyExists = PendingResponseAttachments.Any(a =>
                    string.Equals(a.LocalPath, filePath, StringComparison.OrdinalIgnoreCase));

                if (alreadyExists)
                    continue;

                var item = new ResponseAttachmentItem
                {
                    LocalPath = filePath,
                    MediaType = FileInputHelper.GetMimeType(filePath)
                };

                PendingResponseAttachments.Add(item);

                if (item.IsImage)
                    lastAddedImage = item;
            }

            UpdatePendingAttachmentsPanel();

            if (lastAddedImage != null)
            {
                ShowResponsesImagePreview(lastAddedImage.LocalPath);
            }
            else if (PendingResponseAttachments.Count == 0)
            {
                HideResponsesImagePreview();
            }
        }
        private void ClearPendingResponseAttachments()
        {
            PendingResponseAttachments.Clear();
            UpdatePendingAttachmentsPanel();

            _responsesImagePath = string.Empty;

            if (ResponsesState.SelectedTurn != null)
            {
                string selectedTurnImage = GetPrimaryImagePath(ResponsesState.SelectedTurn);
                if (!string.IsNullOrWhiteSpace(selectedTurnImage) && File.Exists(selectedTurnImage))
                    ShowResponsesImagePreview(selectedTurnImage);
                else
                    HideResponsesImagePreview();
            }
            else
            {
                HideResponsesImagePreview();
            }
        }

        private void OpenLocalFile(string path, string caption)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not open {caption}:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private string GetCurrentPreviewImagePath()
        {
            if (lstResponsesImages?.SelectedItem is MediaFile selected &&
                !string.IsNullOrWhiteSpace(selected.LocalPath) &&
                File.Exists(selected.LocalPath))
            {
                return selected.LocalPath;
            }

            if (!string.IsNullOrWhiteSpace(_responsesPreviewImagePath) &&
                File.Exists(_responsesPreviewImagePath))
            {
                return _responsesPreviewImagePath;
            }

            if (!string.IsNullOrWhiteSpace(_responsesImagePath) &&
                File.Exists(_responsesImagePath))
            {
                return _responsesImagePath;
            }

            return null;
        }

        private void ShowFirstAssistantImageOfSelectedTurn()
        {
            var message = GetSelectedResponseMessage();

            if (message == null ||
                !string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                ClearResponsePreviewImages();
                HideResponsesImagePreview();
                return;
            }

            ShowResponsesImageGallery(message);
        }

        private async void btnResponsesSendRequestClick(object sender, RoutedEventArgs e)
        {
            string userPrompt = ResponsesState.PromptText ?? string.Empty;
            bool hasAttachedFiles = PendingResponseAttachments.Count > 0;

            if (string.IsNullOrWhiteSpace(userPrompt) && !hasAttachedFiles)
                return;

            ResponsesState.IsRequestInProgress = true;
            using (_appStatus.Operation("Preparing request..."))
            {
                try
                {
                    string model = ResponsesState.SelectedModel;
                    string reasoning = ResponsesState.SelectedReasoning;
                    string imgSize = ResponsesState.ImageGenSize;
                    string imgQual = ResponsesState.ImageGenQuality;
                    string searchSize = ResponsesState.SearchContextSize;
                    string imageToolSettingsJson = BuildImageToolSettingsJson();
                    string developerToolSettingsJson = BuildDeveloperToolSettingsJson();

                    string toolsCsv = string.Join(",",
                        _responsesClient.ActiveTools
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase));

                    string titleSeed = !string.IsNullOrWhiteSpace(userPrompt)
                                    ? userPrompt
                                    : (PendingResponseAttachments.FirstOrDefault()?.FileName ?? "[attachment prompt]");
                    _appStatus.Set("Ensuring session...");
                    int sid = await EnsureSessionActiveAsync(EndpointType.Responses, titleSeed);
                    _appStatus.Set("Add user-message to DB");
                    int userMsgId = await _historyService.AddMessageAsync(
                        sid,
                        "user",
                        userPrompt,
                        model: model,
                        reasoning: reasoning,
                        tools: toolsCsv,
                        imgSize: imgSize,
                        imgQual: imgQual,
                        searchSize: searchSize,
                        imageToolSettingsJson: imageToolSettingsJson,
                        developerToolSettingsJson: developerToolSettingsJson);

                    if (hasAttachedFiles)
                    {
                        int i = 0;
                        var list = PendingResponseAttachments.ToList();
                        _appStatus.Set($"Attaching {list.Count} file(s)...");
                        foreach (var attachment in list)
                        {
                            i++;
                            _appStatus.Set($"Attaching file {i}/{list.Count}: {attachment.FileName}");
                            string storedPath = _mediaStorageService.ImportUserFile(attachment.LocalPath);

                            if (!string.IsNullOrWhiteSpace(storedPath))
                            {
                                await _historyService.LinkMediaAsync(userMsgId, storedPath, attachment.MediaType);
                            }
                        }
                    }
                    _appStatus.Set("Getting context for API");
                    var context = await _historyService.GetContextForApiAsync(sid);
                    var developerToolsOptions = BuildDeveloperToolsOptionsFromState();
                    // We are sending full DB history, so start a fresh response chain.
                    ClearDeveloperToolCallLogs();
                    _responsesClient.ClearConversation();
                    _appStatus.Set("GetChatCompletionWLocal tools.");
                    var progress = new Progress<string>(msg => _appStatus.Set(msg));
                    var result = await _responsesClient.GetChatCompletionWithLocalToolsAsync(
                        context,
                        developerToolsOptions,
                        confirmLocalCallAsync: ConfirmDeveloperToolCallAsync,
                        onToolCallLoggedAsync: LogDeveloperToolCallAsync,
                        progress: progress);
                    string toolCallLogJson = BuildDeveloperToolCallLogJson();
                    if (result != null)
                    {
                        _appStatus.Set("Add assistant message to DB");
                        int assistantMsgId = await _historyService.AddMessageAsync(
                            sid,
                            "assistant",
                            result.AssistantText,
                            result.RawJson,
                            model: model,
                            reasoning: reasoning,
                            tools: toolsCsv,
                            imgSize: imgSize,
                            imgQual: imgQual,
                            searchSize: searchSize,
                            developerToolSettingsJson: developerToolSettingsJson,
                            toolCallLogJson: toolCallLogJson);


                        if (result.ImagePayloads?.Count > 0)
                        {
                            _appStatus.Set($"Saving {result.ImagePayloads.Count} images...");
                            var paths = _mediaStorageService.SaveAssistantImages(
                                result.ImagePayloads,
                                result.ImageOutputFormat);

                            foreach (var path in paths)
                            {
                                await _historyService.LinkMediaAsync(
                                    assistantMsgId,
                                    path,
                                    ImageInputHelper.GetMimeType(path));
                            }
                        }
                        _appStatus.Set("Refreshing chat UI...");
                        ClearPendingResponseAttachments();

                        await RefreshCurrentChatUI(sid);
                    }
                    else
                    {
                        _appStatus.Set("No response");
                    }
                }
                catch (Exception ex)
                {
                    _appStatus.Set("Error: " + ex.Message);
                    MessageBox.Show($"Execution Error: {ex.Message}");
                }
                finally
                {
                    ResponsesState.IsRequestInProgress = false;
                }
            }
        }


        // New helper to keep UI in sync with DB
        private async Task RefreshCurrentChatUI(int sessionId)
        {
            using (_appStatus.Operation("Refreshing Current chat UI..."))
            {
                await LoadResponsesSessionAsync(sessionId, restoreLastUserPrompt: false);
            }
        }

        private async void btnResponsesNewChat_Click(object sender, RoutedEventArgs e)
        {
            _activeResponsesSessionId = null;
            await ResetResponsesUi(clearPrompt: true);

            _appStatus.Set("New session started. History will be saved once you send a message.");
        }

        private async void btnResponsesDeleteChat_Click(object sender, RoutedEventArgs e)
        {
            if (_activeResponsesSessionId == null)
                return;

            if (ResponsesState.SelectedTurn is not ChatMessage selectedTurn)
                return;

            var confirm = MessageBox.Show(
                "Delete the currently selected turn?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            _appStatus.Set("Deleting current turn...");

            bool sessionDeleted = await _sessionCleanupService.DeleteTurnAsync(selectedTurn.Id);

            if (sessionDeleted)
            {
                _activeResponsesSessionId = null;
                await ResetResponsesUi(clearPrompt: true);
            }
            else
            {
                await RefreshCurrentChatUI(_activeResponsesSessionId.Value);
            }

            RefreshLogsTab();
        }

        private async void lstResponsesTurns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_responsesClient == null || _isApplyingResponsesSettings)
                return;

            if (lstResponsesTurns.SelectedItem is not ChatMessage selectedMsg)
                return;

            ResponsesState.SelectedTurn = selectedMsg;

            if (string.Equals(selectedMsg.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                ResponsesState.PromptText = selectedMsg.Content;
                await RenderResponsesMarkdownAsync(string.Empty);
                ShowResponsesImageGallery(selectedMsg);
                ClearDeveloperToolCallLogs();
            }
            else if (string.Equals(selectedMsg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                await RenderResponsesMarkdownAsync(selectedMsg.Content);
                ShowResponsesImageGallery(selectedMsg);
                ApplyDeveloperToolCallLogJson(selectedMsg.ToolCallLogJson);
            }
            else
            {
                ClearResponsePreviewImages();
                HideResponsesImagePreview();
                DeveloperToolCallLogs.Clear();
            }
        }

        private void lstResponsesImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstResponsesImages.SelectedItem is not MediaFile selectedImage)
                return;

            if (string.IsNullOrWhiteSpace(selectedImage.LocalPath) || !File.Exists(selectedImage.LocalPath))
                return;

            ShowResponsesImagePreview(selectedImage.LocalPath);
            UpdateResponsesPreviewInfo();
        }

        private void btnResponsesAttachImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select an image for this message",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*",
                CheckFileExists = true,
                InitialDirectory = savepath_images
            };

            if (dlg.ShowDialog() == true)
            {
                _responsesImagePath = dlg.FileName;
                ShowResponsesImagePreview(_responsesImagePath);
            }
        }
        private void btnResponsesRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            _responsesImagePath = string.Empty;
            HideResponsesImagePreview();
        }
        //This is probably unneccessary. Check it out first. Is it really functional?
        //Does it make sense do double-click inside the chat-history.
        //Double-clicking on the preview-image is logical...here maybe not so.
        //REMOVE or NOT? test first
        private void lstResponsesTurns_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstResponsesTurns.SelectedItem is not ChatMessage message)
                return;

            string path = GetPrimaryAttachmentPath(message);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open image:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void imgResponsesPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2)
                return;

            var path = GetCurrentPreviewImagePath();
            if (path == null) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open image:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void MenuItemImageOpen_Click(object sender, RoutedEventArgs e)
        {
            var path = GetCurrentPreviewImagePath();
            if (path == null) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open image:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// Handles the Click event of the 'Open With' menu item for an image, displaying the system 'Open with' dialog
        /// for the currently selected image file.
        /// </summary>
        /// <remarks>If no image is selected or the file does not exist, an informational message is shown
        /// and the dialog is not displayed. If an error occurs while attempting to show the 'Open with' dialog, an
        /// error message is displayed to the user.</remarks>
        /// <param name="sender">The source of the event, typically the menu item that was clicked.</param>
        /// <param name="e">The event data associated with the click event.</param>
        private void MenuItemImageOpenWith_Click(object sender, RoutedEventArgs e)
        {
            var path = GetCurrentPreviewImagePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show("No image selected or file not found.", "Open with",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                ShellLauncher.ShowOpenWithDialog(Window.GetWindow(this), path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not show Open with dialog:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static bool HasResponsesSettings(ChatMessage message)
        {
            if (message == null)
                return false;

            return
                !string.IsNullOrWhiteSpace(message.ModelUsed) ||
                !string.IsNullOrWhiteSpace(message.ReasoningLevel) ||
                !string.IsNullOrWhiteSpace(message.ActiveTools) ||
                !string.IsNullOrWhiteSpace(message.SearchContextSize) ||
                !string.IsNullOrWhiteSpace(message.ImageSize) ||
                !string.IsNullOrWhiteSpace(message.ImageQuality) ||
                !string.IsNullOrWhiteSpace(message.ImageToolSettingsJson) ||
                !string.IsNullOrWhiteSpace(message.DeveloperToolSettingsJson);
        }


        //see use-case in ApplyResponsesSettingsFromHistory. It explains why it still is not used
        /// <summary>
        /// Ensures that the specified model is present in the responses model selection list. If the model does not
        /// already exist, it is added to the list.
        /// </summary>
        /// <remarks>This method does not add duplicate entries. The check is case-insensitive and
        /// considers both the content and tag of each item. If the model is not already tracked, it is also added to
        /// the active models collection.</remarks>
        /// <param name="model">The name of the model to ensure exists in the responses model selection list. Cannot be null, empty, or
        /// whitespace.</param>
        private void EnsureResponsesModelExists(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return;

            bool exists = cmbResponsesModel.Items
                .OfType<ComboBoxItem>()
                .Any(i =>
                    string.Equals(i.Tag?.ToString(), model, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(i.Content?.ToString(), model, StringComparison.OrdinalIgnoreCase));

            if (exists)
                return;

            cmbResponsesModel.Items.Insert(0, new ComboBoxItem
            {
                Content = model,
                Tag = model
            });

            if (!_activeModelsForResponses.Contains(model, StringComparer.OrdinalIgnoreCase))
                _activeModelsForResponses.Insert(0, model);
        }
        //This is a point: If the setting(ModelUsed) does not exist we can add it.
        //But this will be a 'problem' when reading from the model-list that is
        //created. This value will then be removed again. Maybe it should be saved by using
        //AvailableModelsStorage (new_list). Think about it before doing any changes.
        //EnsureResponsesModelExists(settingsMessage.ModelUsed);
        private void ApplyResponsesSettingsFromHistory(IReadOnlyList<ChatMessage> history)
        {
            if (history == null || history.Count == 0)
                return;

            var settingsMessage = history.LastOrDefault(HasResponsesSettings);
            if (settingsMessage == null)
                return;

            _isApplyingResponsesSettings = true;
            try
            {
                ResponsesState.SelectedModel = settingsMessage.ModelUsed;
                ResponsesState.SelectedReasoning = string.IsNullOrWhiteSpace(settingsMessage.ReasoningLevel)
                    ? "none"
                    : settingsMessage.ReasoningLevel;

                ResponsesState.SearchContextSize = string.IsNullOrWhiteSpace(settingsMessage.SearchContextSize)
                    ? "medium"
                    : settingsMessage.SearchContextSize;

                ResponsesState.ImageGenQuality = string.IsNullOrWhiteSpace(settingsMessage.ImageQuality)
                    ? "auto"
                    : settingsMessage.ImageQuality;

                ResponsesState.ImageGenSize = string.IsNullOrWhiteSpace(settingsMessage.ImageSize)
                    ? "auto"
                    : settingsMessage.ImageSize;

                ResponsesState.ImageGenOutputFormat = "jpeg";
                ResponsesState.ImageGenBackground = "auto";
                ResponsesState.ImageGenInputFidelity = "high";
                ResponsesState.ImageGenOutputCompression = 85;
                ApplyResponsesToolsToState(settingsMessage.ActiveTools);
                ApplyDeveloperToolSettingsFromJson(settingsMessage.DeveloperToolSettingsJson);
            }
            finally
            {
                _isApplyingResponsesSettings = false;
            }

            NormalizeResponsesToolsState();
            ApplyResponsesStateToClient();
            //ApplyDeveloperToolSettingsFromJson(settingsMessage.DeveloperToolSettingsJson);
        }
        private void NormalizeResponsesToolsState()
        {
            if (_isApplyingResponsesSettings)
                return;

            _isApplyingResponsesSettings = true;
            try
            {
                if (ResponsesState.UseComputerUse)
                {
                    ResponsesState.UseWebSearch = false;
                    ResponsesState.UseImageGeneration = false;
                }

                bool anyNonText =
                    ResponsesState.UseWebSearch ||
                    ResponsesState.UseComputerUse ||
                    ResponsesState.UseImageGeneration;

                if (ResponsesState.UseTextTool && anyNonText)
                {
                    ResponsesState.UseTextTool = false;
                }

                if (!ResponsesState.UseTextTool && !anyNonText)
                {
                    ResponsesState.UseTextTool = true;
                }

                if (ResponsesState.UseTextTool)
                {
                    ResponsesState.UseWebSearch = false;
                    ResponsesState.UseComputerUse = false;
                    ResponsesState.UseImageGeneration = false;
                }
            }
            finally
            {
                _isApplyingResponsesSettings = false;
            }
        }
        private void ApplyResponsesStateToClient()
        {
            if (_responsesClient == null)
                return;

            _responsesClient.CurrentModel = ResponsesState.SelectedModel;
            _responsesClient.CurrentReasoning = ResponsesState.SelectedReasoning;
            _responsesClient.WebSearchContextSize = ResponsesState.SearchContextSize;
            _responsesClient.ImageGenQuality = ResponsesState.ImageGenQuality;
            _responsesClient.ImageGenSize = ResponsesState.ImageGenSize;
            _responsesClient.ImageGenOutputFormat = ResponsesState.ImageGenOutputFormat;
            _responsesClient.ImageGenOutputCompression = ResponsesState.ImageGenOutputCompression;
            _responsesClient.ImageGenBackground = ResponsesState.ImageGenBackground;
            _responsesClient.ImageGenInputFidelity = ResponsesState.ImageGenInputFidelity;

            _responsesClient.ActiveTools.Clear();

            if (ResponsesState.UseTextTool)
                _responsesClient.ActiveTools.Add(ResponseToolKeys.Text);

            if (ResponsesState.UseWebSearch)
                _responsesClient.ActiveTools.Add(ResponseToolKeys.WebSearch);

            if (ResponsesState.UseComputerUse)
                _responsesClient.ActiveTools.Add(ResponseToolKeys.ComputerUsePreview);

            if (ResponsesState.UseImageGeneration)
                _responsesClient.ActiveTools.Add(ResponseToolKeys.ImageGeneration);

            if (_responsesClient.ActiveTools.Count == 0)
                _responsesClient.ActiveTools.Add(ResponseToolKeys.Text);
        }

        private void ValidateResponsesState()
        {
            string model = ResponsesState.SelectedModel;
            string reasoning = ResponsesState.SelectedReasoning;

            bool supportsReasoning = model.StartsWith("gpt-5") ||
                                     model.StartsWith("o") ||
                                     model == "gpt-5-pro";

            if (!supportsReasoning && reasoning != "none")
            {
                ResponsesState.SelectedReasoning = "none";
                _appStatus.Set($"Model '{model}' does not support reasoning settings.");
            }
            else if (model == "gpt-5-pro" && reasoning != "high")
            {
                ResponsesState.SelectedReasoning = "high";
                _appStatus.Set("gpt-5-pro requires high reasoning.");
            }
        }
        private sealed class ImageToolSettingsSnapshot
        {
            [JsonPropertyName("quality")]
            public string Quality { get; set; } = "auto";

            [JsonPropertyName("size")]
            public string Size { get; set; } = "auto";

            [JsonPropertyName("output_format")]
            public string OutputFormat { get; set; } = "jpeg";

            [JsonPropertyName("output_compression")]
            public int? OutputCompression { get; set; }

            [JsonPropertyName("background")]
            public string Background { get; set; } = "auto";

            [JsonPropertyName("input_fidelity")]
            public string InputFidelity { get; set; } = "high";
        }

        private sealed class DeveloperToolSettingsSnapshot
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; }

            [JsonPropertyName("repository_root")]
            public string RepositoryRoot { get; set; } = string.Empty;

            [JsonPropertyName("scope")]
            public string Scope { get; set; } = "repository";

            [JsonPropertyName("search_project_text")]
            public bool SearchProjectText { get; set; } = true;

            [JsonPropertyName("read_project_file")]
            public bool ReadProjectFile { get; set; } = true;

            [JsonPropertyName("list_project_files")]
            public bool ListProjectFiles { get; set; }

            [JsonPropertyName("run_diagnostics")]
            public bool RunDiagnostics { get; set; }

            [JsonPropertyName("read_only_only")]
            public bool ReadOnlyOnly { get; set; } = true;

            [JsonPropertyName("require_confirmation")]
            public bool RequireConfirmation { get; set; }

            [JsonPropertyName("show_tool_logs")]
            public bool ShowToolLogs { get; set; } = true;

            [JsonPropertyName("allowed_extensions_csv")]
            public string AllowedExtensionsCsv { get; set; } =
                ResponsesPanelState.GetDefaultAllowedExtensionsCsv();
            
        }
        private string BuildImageToolSettingsJson()
        {
            if (!ResponsesState.UseImageGeneration)
                return string.Empty;

            var snapshot = new ImageToolSettingsSnapshot
            {
                Quality = string.IsNullOrWhiteSpace(ResponsesState.ImageGenQuality)
                    ? "auto"
                    : ResponsesState.ImageGenQuality,

                Size = string.IsNullOrWhiteSpace(ResponsesState.ImageGenSize)
                    ? "auto"
                    : ResponsesState.ImageGenSize,

                OutputFormat = string.IsNullOrWhiteSpace(ResponsesState.ImageGenOutputFormat)
                    ? "jpeg"
                    : ResponsesState.ImageGenOutputFormat,

                OutputCompression = ResponsesState.IsOutputCompressionEnabled
                    ? ResponsesState.ImageGenOutputCompression
                    : null,

                Background = string.IsNullOrWhiteSpace(ResponsesState.ImageGenBackground)
                    ? "auto"
                    : ResponsesState.ImageGenBackground,

                InputFidelity = string.IsNullOrWhiteSpace(ResponsesState.ImageGenInputFidelity)
                    ? "high"
                    : ResponsesState.ImageGenInputFidelity
            };

            return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        private string BuildDeveloperToolSettingsJson()
        {
            var snapshot = new DeveloperToolSettingsSnapshot
            {
                Enabled = ResponsesState.UseDeveloperTools,
                RepositoryRoot = ResponsesState.DeveloperRepositoryRoot ?? string.Empty,
                Scope = string.IsNullOrWhiteSpace(ResponsesState.DeveloperScope)
                    ? "repository"
                    : ResponsesState.DeveloperScope,

                SearchProjectText = ResponsesState.DeveloperToolSearchProjectText,
                ReadProjectFile = ResponsesState.DeveloperToolReadProjectFile,
                ListProjectFiles = ResponsesState.DeveloperToolListProjectFiles,
                RunDiagnostics = ResponsesState.DeveloperToolRunDiagnostics,

                ReadOnlyOnly = ResponsesState.DeveloperAllowReadOnlyOnly,
                RequireConfirmation = ResponsesState.DeveloperRequireConfirmation,
                ShowToolLogs = ResponsesState.DeveloperShowToolLogs,

                AllowedExtensionsCsv = string.IsNullOrWhiteSpace(ResponsesState.DeveloperAllowedExtensionsCsv)
                    ? ResponsesPanelState.GetDefaultAllowedExtensionsCsv()
                    : ResponsesState.DeveloperAllowedExtensionsCsv
            };

            return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
        private void ApplyImageToolSettingsFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var snapshot = JsonSerializer.Deserialize<ImageToolSettingsSnapshot>(json);
                if (snapshot == null)
                    return;

                ResponsesState.ImageGenQuality =
                    string.IsNullOrWhiteSpace(snapshot.Quality) ? "auto" : snapshot.Quality;

                ResponsesState.ImageGenSize =
                    string.IsNullOrWhiteSpace(snapshot.Size) ? "auto" : snapshot.Size;

                ResponsesState.ImageGenOutputFormat =
                    string.IsNullOrWhiteSpace(snapshot.OutputFormat) ? "jpeg" : snapshot.OutputFormat;

                ResponsesState.ImageGenBackground =
                    string.IsNullOrWhiteSpace(snapshot.Background) ? "auto" : snapshot.Background;

                ResponsesState.ImageGenInputFidelity =
                    string.IsNullOrWhiteSpace(snapshot.InputFidelity) ? "high" : snapshot.InputFidelity;

                if (snapshot.OutputCompression.HasValue)
                    ResponsesState.ImageGenOutputCompression = snapshot.OutputCompression.Value;
            }
            catch
            {
                // Keep tolerant; old rows may not have valid JSON
            }
        }
        private void ApplyDeveloperToolSettingsFromJson(string json)
        {
            // Always reset first so opening a session without developer-tool settings
            // does not inherit stale UI state from the previously opened session.
            ResetDeveloperToolsState();

            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var snapshot = JsonSerializer.Deserialize<DeveloperToolSettingsSnapshot>(json);
                if (snapshot == null)
                    return;

                ResponsesState.UseDeveloperTools = snapshot.Enabled;
                ResponsesState.DeveloperRepositoryRoot = snapshot.RepositoryRoot ?? string.Empty;
                ResponsesState.DeveloperScope = string.IsNullOrWhiteSpace(snapshot.Scope)
                    ? "repository"
                    : snapshot.Scope;

                ResponsesState.DeveloperToolSearchProjectText = snapshot.SearchProjectText;
                ResponsesState.DeveloperToolReadProjectFile = snapshot.ReadProjectFile;
                ResponsesState.DeveloperToolListProjectFiles = snapshot.ListProjectFiles;
                ResponsesState.DeveloperToolRunDiagnostics = snapshot.RunDiagnostics;

                ResponsesState.DeveloperAllowReadOnlyOnly = snapshot.ReadOnlyOnly;
                ResponsesState.DeveloperRequireConfirmation = snapshot.RequireConfirmation;
                ResponsesState.DeveloperShowToolLogs = snapshot.ShowToolLogs;
                ResponsesState.DeveloperAllowedExtensionsCsv =
                    string.IsNullOrWhiteSpace(snapshot.AllowedExtensionsCsv)
                        ? ResponsesPanelState.GetDefaultAllowedExtensionsCsv()
                        : snapshot.AllowedExtensionsCsv;
            }
            catch
            {
                // Keep tolerant for older rows or malformed JSON
            }
        }
        private void ApplyResponsesToolsToState(string toolsCsv)
        {
            var tools = (toolsCsv ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (tools.Count == 0)
                tools.Add(ResponseToolKeys.Text);

            if (tools.Count > 1 && tools.Contains(ResponseToolKeys.Text))
                tools.Remove(ResponseToolKeys.Text);

            ResponsesState.UseTextTool = tools.Contains(ResponseToolKeys.Text);
            ResponsesState.UseWebSearch = tools.Contains(ResponseToolKeys.WebSearch);
            ResponsesState.UseComputerUse = tools.Contains(ResponseToolKeys.ComputerUsePreview);
            ResponsesState.UseImageGeneration = tools.Contains(ResponseToolKeys.ImageGeneration);
        }
        private void ValidateImageGenerationSettings()
        {
            if (ResponsesState.ImageGenOutputCompression < 0)
                ResponsesState.ImageGenOutputCompression = 0;

            if (ResponsesState.ImageGenOutputCompression > 100)
                ResponsesState.ImageGenOutputCompression = 100;

            if (!ResponsesState.UseImageGeneration)
                return;

            bool compressionAllowed =
                string.Equals(ResponsesState.ImageGenOutputFormat, "jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ResponsesState.ImageGenOutputFormat, "webp", StringComparison.OrdinalIgnoreCase);

            bool transparentAllowed =
                string.Equals(ResponsesState.ImageGenOutputFormat, "png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ResponsesState.ImageGenOutputFormat, "webp", StringComparison.OrdinalIgnoreCase);

            if (!compressionAllowed)
            {
                // Keep a default value stored, but it won't be sent.
                if (ResponsesState.ImageGenOutputCompression < 0 || ResponsesState.ImageGenOutputCompression > 100)
                    ResponsesState.ImageGenOutputCompression = 85;
            }

            if (!transparentAllowed &&
                string.Equals(ResponsesState.ImageGenBackground, "transparent", StringComparison.OrdinalIgnoreCase))
            {
                ResponsesState.ImageGenBackground = "auto";
                _appStatus.Set($"Transparent background is not supported with {ResponsesState.ImageGenOutputFormat}.");
            }

            if (string.IsNullOrWhiteSpace(ResponsesState.ImageGenOutputFormat))
                ResponsesState.ImageGenOutputFormat = "jpeg";

            if (string.IsNullOrWhiteSpace(ResponsesState.ImageGenBackground))
                ResponsesState.ImageGenBackground = "auto";

            if (string.IsNullOrWhiteSpace(ResponsesState.ImageGenInputFidelity))
                ResponsesState.ImageGenInputFidelity = "high";
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                T descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }

        private void btnResponsesAttachFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select files for this message",
                Multiselect = true,
                CheckFileExists = true,
                InitialDirectory = savepath_images,
                Filter =
                    "Common Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.pdf;*.txt;*.md;*.csv;*.json;*.xml;*.cs;*.docx;*.xlsx|" +
                    "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|" +
                    "Document Files|*.pdf;*.txt;*.md;*.csv;*.json;*.xml;*.docx;*.xlsx;*.cs;*.css;*.js;*.html|" +
                    "All Files|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                AddPendingResponseAttachments(dlg.FileNames);
            }
        }

        private void btnResponsesClearAttachments_Click(object sender, RoutedEventArgs e)
        {
            ClearPendingResponseAttachments();
        }

        private void btnResponsesRemovePendingAttachment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ResponseAttachmentItem item)
                return;

            PendingResponseAttachments.Remove(item);
            UpdatePendingAttachmentsPanel();

            if (string.Equals(_responsesImagePath, item.LocalPath, StringComparison.OrdinalIgnoreCase))
            {
                var nextImage = PendingResponseAttachments.FirstOrDefault(a => a.IsImage && File.Exists(a.LocalPath));
                if (nextImage != null)
                    ShowResponsesImagePreview(nextImage.LocalPath);
                else
                    HideResponsesImagePreview();
            }
            else if (PendingResponseAttachments.Count == 0)
            {
                HideResponsesImagePreview();
            }
        }

        private void btnResponsesOpenPendingAttachment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ResponseAttachmentItem item)
                return;

            OpenLocalFile(item.LocalPath, "attachment");
        }

        private void lstResponsesPendingAttachments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstResponsesPendingAttachments.SelectedItem is not ResponseAttachmentItem item)
                return;

            if (item.IsImage && File.Exists(item.LocalPath))
                ShowResponsesImagePreview(item.LocalPath);
        }
        private void btnDeveloperBrowseRoot_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select repository root"
            };

            if (dlg.ShowDialog() == true)
            {
                ResponsesState.DeveloperRepositoryRoot = dlg.FolderName ?? string.Empty;
            }
        }
        private DeveloperToolsOptions BuildDeveloperToolsOptionsFromState()
        {
            var extensions = (ResponsesState.DeveloperAllowedExtensionsCsv ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            return new DeveloperToolsOptions
            {
                Enabled = ResponsesState.UseDeveloperTools,
                RepositoryRoot = ResponsesState.DeveloperRepositoryRoot ?? string.Empty,
                ScopeMode = ResponsesState.DeveloperScope ?? "repository",

                ReadOnlyOnly = ResponsesState.DeveloperAllowReadOnlyOnly,
                RequireConfirmation = ResponsesState.DeveloperRequireConfirmation,
                ShowToolLogs = ResponsesState.DeveloperShowToolLogs,

                SearchProjectTextEnabled = ResponsesState.DeveloperToolSearchProjectText,
                ReadProjectFileEnabled = ResponsesState.DeveloperToolReadProjectFile,
                ListProjectFilesEnabled = ResponsesState.DeveloperToolListProjectFiles,
                RunDiagnosticsEnabled = ResponsesState.DeveloperToolRunDiagnostics,

                AllowedExtensions = extensions,
                MaxReadLines = 300,
                MaxSearchResults = 100,
                MaxFileBytes = 512 * 1024
            };
        }
        private Task<bool> ConfirmDeveloperToolCallAsync(string toolName, string argumentsJson)
        {
            if (!ResponsesState.DeveloperRequireConfirmation)
                return Task.FromResult(true);

            var result = MessageBox.Show(
                $"Allow local tool call?\n\nTool: {toolName}\n\nArguments:\n{argumentsJson}",
                "Confirm local developer tool call",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return Task.FromResult(result == MessageBoxResult.Yes);
        }

        private Task LogDeveloperToolCallAsync(string toolName, string argumentsJson, string resultJson)
        {
            if (!ResponsesState.DeveloperShowToolLogs)
                return Task.CompletedTask;

            Dispatcher.Invoke(() =>
            {
                _appStatus.Set($"Local tool used: {toolName}");
            });

            Debug.WriteLine($"[LOCAL TOOL] {toolName}");
            Debug.WriteLine(argumentsJson);
            Debug.WriteLine(resultJson);

            AddDeveloperToolCallLog(toolName, argumentsJson, resultJson);

            return Task.CompletedTask;
        }
        private void AddDeveloperToolCallLog(string toolName, string argumentsJson, string resultJson)
        {
            Dispatcher.Invoke(() =>
            {
                DeveloperToolCallLogs.Add(new DeveloperToolCallLogItem
                {
                    Timestamp = DateTime.Now,
                    ToolName = toolName ?? string.Empty,
                    ArgumentsJson = argumentsJson ?? string.Empty,
                    ResultJson = resultJson ?? string.Empty
                });
            });
        }

        private void ClearDeveloperToolCallLogs()
        {
            DeveloperToolCallLogs.Clear();
        }
        private string BuildDeveloperToolCallLogJson()
        {
            if (DeveloperToolCallLogs.Count == 0)
                return string.Empty;

            return JsonSerializer.Serialize(DeveloperToolCallLogs);
        }
        private void ApplyDeveloperToolCallLogJson(string json)
        {
            DeveloperToolCallLogs.Clear();

            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var items = JsonSerializer.Deserialize<List<DeveloperToolCallLogItem>>(json);
                if (items == null)
                    return;

                foreach (var item in items)
                {
                    DeveloperToolCallLogs.Add(item);
                }
            }
            catch
            {
                // tolerate older rows / malformed JSON
            }
        }
        /// <summary>
        /// Represents a selectable Markdown theme option with associated display and file information.
        /// </summary>
        /// <remarks>This class is typically used to provide theme choices in a Markdown rendering or
        /// editing context. Instances are immutable and intended for use as value objects in UI or configuration
        /// scenarios.</remarks>
        private sealed class MarkdownThemeOption
        {
            public string FileName { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
            public string RelativeHref { get; init; } = string.Empty;

            public override string ToString() => DisplayName;
        }
        private sealed class PageThemeOption
        {
            public string FileName { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
            public string RelativeHref { get; init; } = string.Empty;

            public override string ToString() => DisplayName;
        }
        private string GetMarkdownViewerAssetsOutputPath()
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets",
                "MarkdownViewer");
        }

        private List<MarkdownThemeOption> LoadMarkdownThemeOptions()
        {
            string stylesPath = System.IO.Path.Combine(GetMarkdownViewerAssetsOutputPath(), "styles");

            if (!Directory.Exists(stylesPath))
                return new List<MarkdownThemeOption>();

            var files = Directory.GetFiles(stylesPath, "*.min.css", SearchOption.TopDirectoryOnly);

            return files
                .Select(path =>
                {
                    string fileName = Path.GetFileName(path);

                    return new MarkdownThemeOption
                    {
                        FileName = fileName,
                        DisplayName = FormatMarkdownThemeDisplayName(fileName),
                        RelativeHref = "styles/" + fileName
                    };
                })
                .OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private string FormatMarkdownThemeDisplayName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);

            if (name.EndsWith(".min", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);

            name = name.Replace("-", " ");

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        }

        private MarkdownThemeOption? GetDefaultMarkdownThemeOption(List<MarkdownThemeOption> options)
        {
            var github = options.FirstOrDefault(t =>
                t.FileName.Equals("github.min.css", StringComparison.OrdinalIgnoreCase));

            return github ?? options.FirstOrDefault();
        }
        /// Markdown pipeline for rendering response text (used for copy as markdown)
        private readonly MarkdownPipeline _responsesMarkdownPipeline =
                                        new MarkdownPipelineBuilder()
                                            .UseAdvancedExtensions()
                                            .Build();

        private async Task EnsureResponsesWebViewInitializedAsync()
        {
            if (_responsesWebViewInitialized)
                return;

            await wvResponsesResponse.EnsureCoreWebView2Async();

            string assetsPath = GetMarkdownViewerAssetsOutputPath();

            wvResponsesResponse.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "markdownviewer.local",
                assetsPath,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

            wvResponsesResponse.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            wvResponsesResponse.CoreWebView2.Settings.AreDevToolsEnabled = true;
            wvResponsesResponse.CoreWebView2.Settings.IsStatusBarEnabled = false;
            wvResponsesResponse.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            wvResponsesResponse.CoreWebView2.Settings.IsZoomControlEnabled = true;
            wvResponsesResponse.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = true;
            
            wvResponsesResponse.CoreWebView2.NavigationStarting += ResponsesWebView_NavigationStarting;
            wvResponsesResponse.CoreWebView2.WebMessageReceived += ResponsesWebView_WebMessageReceived;

            _responsesWebViewInitialized = true;

            _markdownThemeOptions = LoadMarkdownThemeOptions();
            BindMarkdownThemeOptions();

            _pageThemeOptions = LoadPageThemeOptions();
            BindPageThemeOptions();

            await EnsureResponsesViewerPageLoadedAsync();
        }
        private string ConvertMarkdownToHtmlBody(string markdown)
        {
            markdown ??= string.Empty;
            return Markdig.Markdown.ToHtml(markdown, _responsesMarkdownPipeline);
        }

        private void BindMarkdownThemeOptions()
        {
            _settings ??= AppSettings.LoadSettings();

            _bindingMarkdownThemeOptions = true;
            try
            {
                cmbResponsesMarkdownTheme.ItemsSource = _markdownThemeOptions;

                string savedTheme = _settings.ResponsesMarkdownTheme;

                _selectedMarkdownTheme =
                    _markdownThemeOptions.FirstOrDefault(t =>
                        t.FileName.Equals(savedTheme, StringComparison.OrdinalIgnoreCase))
                    ?? GetDefaultMarkdownThemeOption(_markdownThemeOptions);

                if (_selectedMarkdownTheme != null)
                    cmbResponsesMarkdownTheme.SelectedItem = _selectedMarkdownTheme;
            }
            finally
            {
                _bindingMarkdownThemeOptions = false;
            }
        }
        private async Task EnsureResponsesViewerPageLoadedAsync()
        {
            if (_responsesViewerPageLoaded)
                return;

            var tcs = new TaskCompletionSource<bool>();

            void Handler(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
            {
                wvResponsesResponse.NavigationCompleted -= Handler;
                tcs.TrySetResult(true);
            }

            wvResponsesResponse.NavigationCompleted += Handler;
            wvResponsesResponse.CoreWebView2.Navigate("https://markdownviewer.local/template.html");

            await tcs.Task;
            _responsesViewerPageLoaded = true;
        }
        private void ResponsesWebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Uri))
                return;

            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out Uri uri))
                return;

            if (uri.Host.Equals("markdownviewer.local", StringComparison.OrdinalIgnoreCase))
                return;

            bool isExternalLink =
                uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase);

            if (!isExternalLink)
                return;

            e.Cancel = true;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }
        private void ResponsesWebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();
                if (string.IsNullOrWhiteSpace(message))
                    return;

                const string prefix = "copy-code:";
                if (!message.StartsWith(prefix, StringComparison.Ordinal))
                    return;

                string code = message.Substring(prefix.Length).TrimEnd();

                if (string.IsNullOrEmpty(code))
                    return;

                Clipboard.SetText(code);
            }
            catch
            {
                // Optional: log or show status message
            }
        }

        private async Task RenderResponsesMarkdownAsync(string markdown)
        {
            await EnsureResponsesWebViewInitializedAsync();
            await EnsureResponsesViewerPageLoadedAsync();

            string htmlBody = ConvertMarkdownToHtmlBody(markdown);
            string jsArgument = System.Text.Json.JsonSerializer.Serialize(htmlBody);

            await wvResponsesResponse.ExecuteScriptAsync(
                $"window.markdownViewer.setContent({jsArgument});");

            await ApplySelectedPageThemeAsync();
            await ApplySelectedMarkdownThemeAsync();
        }
        private async Task ApplySelectedPageThemeAsync()
        {
            if (_selectedPageTheme == null)
                return;

            string hrefArgument = JsonSerializer.Serialize(_selectedPageTheme.RelativeHref);

            await wvResponsesResponse.ExecuteScriptAsync(
                $"window.markdownViewer.setPageTheme({hrefArgument});");
        }
        private async Task ApplySelectedMarkdownThemeAsync()
        {
            if (_selectedMarkdownTheme == null)
                return;

            string hrefArgument = System.Text.Json.JsonSerializer.Serialize(_selectedMarkdownTheme.RelativeHref);

            await wvResponsesResponse.ExecuteScriptAsync(
                $"window.markdownViewer.setHighlightTheme({hrefArgument});");

            /*await wvResponsesResponse.ExecuteScriptAsync(
                "window.markdownViewer.refreshHighlighting();");*/
        }
        private async void cmbResponsesMarkdownTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_bindingMarkdownThemeOptions)
                return;

            if (cmbResponsesMarkdownTheme.SelectedItem is not MarkdownThemeOption selected)
                return;

            _selectedMarkdownTheme = selected;

            _settings ??= AppSettings.LoadSettings();
            _settings.ResponsesMarkdownTheme = selected.FileName;
            AppSettings.SaveSettings(_settings);

            if (!_responsesViewerPageLoaded)
                return;

            await ApplySelectedMarkdownThemeAsync();
        }
        private async void cmbResponsesPageTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_bindingPageThemeOptions)
                return;

            if (cmbResponsesPageTheme.SelectedItem is not PageThemeOption selected)
                return;

            _selectedPageTheme = selected;

            _settings ??= AppSettings.LoadSettings();
            _settings.ResponsesPageTheme = selected.FileName;
            AppSettings.SaveSettings(_settings);

            if (!_responsesViewerPageLoaded)
                return;

            await ApplySelectedPageThemeAsync();
        }
        private List<PageThemeOption> LoadPageThemeOptions()
        {
            string themesPath = Path.Combine(GetMarkdownViewerAssetsOutputPath(), "page-themes");

            if (!Directory.Exists(themesPath))
                return new List<PageThemeOption>();

            var files = Directory.GetFiles(themesPath, "*.css", SearchOption.TopDirectoryOnly);

            return files
                .Select(path =>
                {
                    string fileName = Path.GetFileName(path);

                    return new PageThemeOption
                    {
                        FileName = fileName,
                        DisplayName = FormatThemeDisplayName(fileName),
                        RelativeHref = "page-themes/" + fileName
                    };
                })
                .OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private string FormatThemeDisplayName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);

            if (name.EndsWith(".min", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);

            name = name.Replace("-", " ");
            name = name.Replace("_", " ");

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        }

        private PageThemeOption? GetDefaultPageThemeOption(List<PageThemeOption> options)
        {
            var preferred = options.FirstOrDefault(t =>
                t.FileName.Equals("github-light-page.css", StringComparison.OrdinalIgnoreCase));

            return preferred ?? options.FirstOrDefault();
        }
        private void BindPageThemeOptions()
        {
            _settings ??= AppSettings.LoadSettings();

            _bindingPageThemeOptions = true;
            try
            {
                cmbResponsesPageTheme.ItemsSource = _pageThemeOptions;

                string savedTheme = _settings.ResponsesPageTheme;

                _selectedPageTheme =
                    _pageThemeOptions.FirstOrDefault(t =>
                        t.FileName.Equals(savedTheme, StringComparison.OrdinalIgnoreCase))
                    ?? GetDefaultPageThemeOption(_pageThemeOptions);

                if (_selectedPageTheme != null)
                    cmbResponsesPageTheme.SelectedItem = _selectedPageTheme;
            }
            finally
            {
                _bindingPageThemeOptions = false;
            }
        }
    }
}
