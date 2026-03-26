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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
            ResponsesState.DeveloperAllowedExtensionsCsv = ".cs,.xaml,.csproj,.sln,.json,.xml,.md,.config,.props,.targets";
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
            UpdatePendingAttachmentsPanel();
            var history = await _historyService.GetFullSessionHistoryAsync(sessionId);

            ReplaceCurrentChatMessages(history);
            ApplyResponsesSettingsFromHistory(history);
            ClearResponsePreviewImages();
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
                    ResponsesState.ResponseText = selected.Content;
                }
                else
                {
                    ResponsesState.ResponseText = string.Empty;
                }

                ShowResponsesImageGallery(selected);
            }
            else
            {
                ResponsesState.ResponseText = string.Empty;
                ClearResponsePreviewImages();
                HideResponsesImagePreview();
            }
        }

        private void ResetResponsesUi(bool clearPrompt = true)
        {
            CurrentChatMessages.Clear();
            ResponsesState.SelectedTurn = null;
            ResponsesState.ResponseText = string.Empty;

            if (clearPrompt)
                ResponsesState.PromptText = string.Empty;

            _responsesImagePath = string.Empty;
            _responsesPreviewImagePath = string.Empty;
            ClearResponsePreviewImages();
            HideResponsesImagePreview();
            ResetDeveloperToolsState();

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
                ".cs,.xaml,.csproj,.sln,.json,.xml,.md,.config,.props,.targets";
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
                MessageBox.Show($"Could not open {caption}:\n{ex.Message}",
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

            this.IsEnabled = false;

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
                int sid = await EnsureSessionActiveAsync(EndpointType.Responses, titleSeed);

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
                    foreach (var attachment in PendingResponseAttachments.ToList())
                    {
                        string storedPath = _mediaStorageService.ImportUserFile(attachment.LocalPath);

                        if (!string.IsNullOrWhiteSpace(storedPath))
                        {
                            await _historyService.LinkMediaAsync(
                                userMsgId,
                                storedPath,
                                attachment.MediaType);
                        }
                    }
                }

                var context = await _historyService.GetContextForApiAsync(sid);
                var developerToolsOptions = BuildDeveloperToolsOptionsFromState();
                // We are sending full DB history, so start a fresh response chain.
                _responsesClient.ClearConversation();
                var result = await _responsesClient.GetChatCompletionWithLocalToolsAsync(
                    context,
                    developerToolsOptions,
                    confirmLocalCallAsync: ConfirmDeveloperToolCallAsync,
                    onToolCallLoggedAsync: LogDeveloperToolCallAsync);

                if (result != null)
                {
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
                        developerToolSettingsJson: developerToolSettingsJson);

                    if (result.ImagePayloads?.Count > 0)
                    {
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

                    ClearPendingResponseAttachments();

                    await RefreshCurrentChatUI(sid);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Execution Error: {ex.Message}");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }


        // New helper to keep UI in sync with DB
        private async Task RefreshCurrentChatUI(int sessionId)
        {
            await LoadResponsesSessionAsync(sessionId);
        }

        private void btnResponsesNewChat_Click(object sender, RoutedEventArgs e)
        {
            _activeResponsesSessionId = null;
            ResetResponsesUi(clearPrompt: true);

            StatusText.Text = "New session started. History will be saved once you send a message.";
        }

        private async void btnResponsesDeleteChat_Click(object sender, RoutedEventArgs e)
        {
            if (_activeResponsesSessionId == null)
                return;

            var confirm = MessageBox.Show(
                "Delete this conversation from history?",
                "Confirm",
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes)
                return;

            StatusText.Text = "Deleting session...: " + _activeResponsesSessionId.Value;

            await _sessionCleanupService.DeleteSessionAsync(_activeResponsesSessionId.Value);

            _activeResponsesSessionId = null;
            ResetResponsesUi(clearPrompt: true);
            RefreshLogsTab();
        }

        private void lstResponsesTurns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_responsesClient == null || _isApplyingResponsesSettings)
                return;

            if (lstResponsesTurns.SelectedItem is not ChatMessage selectedMsg)
                return;

            ResponsesState.SelectedTurn = selectedMsg;

            if (string.Equals(selectedMsg.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                ResponsesState.PromptText = selectedMsg.Content;
                ResponsesState.ResponseText = string.Empty;
                ShowResponsesImageGallery(selectedMsg);
            }
            else if (string.Equals(selectedMsg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                ResponsesState.ResponseText = selectedMsg.Content;
                ShowResponsesImageGallery(selectedMsg);
            }
            else
            {
                ClearResponsePreviewImages();
                HideResponsesImagePreview();
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
            ApplyDeveloperToolSettingsFromJson(settingsMessage.DeveloperToolSettingsJson);
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
                StatusText.Text = $"Model '{model}' does not support reasoning settings.";
            }
            else if (model == "gpt-5-pro" && reasoning != "high")
            {
                ResponsesState.SelectedReasoning = "high";
                StatusText.Text = "gpt-5-pro requires high reasoning.";
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
                ".cs,.xaml,.csproj,.sln,.json,.xml,.md,.config,.props,.targets";
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
                    ? ".cs,.xaml,.csproj,.sln,.json,.xml,.md,.config,.props,.targets"
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
                        ? ".cs,.xaml,.csproj,.sln,.json,.xml,.md,.config,.props,.targets"
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
                StatusText.Text = $"Transparent background is not supported with {ResponsesState.ImageGenOutputFormat}.";
            }

            if (string.IsNullOrWhiteSpace(ResponsesState.ImageGenOutputFormat))
                ResponsesState.ImageGenOutputFormat = "jpeg";

            if (string.IsNullOrWhiteSpace(ResponsesState.ImageGenBackground))
                ResponsesState.ImageGenBackground = "auto";

            if (string.IsNullOrWhiteSpace(ResponsesState.ImageGenInputFidelity))
                ResponsesState.ImageGenInputFidelity = "high";
        }
        private void UpdateResponsesResponseDocument(string text)
        {
            if (rtbResponsesResponse == null)
                return;

            rtbResponsesResponse.Document = BuildResponseDocument(text ?? string.Empty);
        }

        private FlowDocument BuildResponseDocument(string text)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(10),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                TextAlignment = TextAlignment.Left
            };

            if (string.IsNullOrWhiteSpace(text))
                return doc;

            var parts = SplitIntoCodeAndTextBlocks(text);

            foreach (var part in parts)
            {
                if (part.IsCode)
                {
                    if (!string.IsNullOrWhiteSpace(part.Language))
                    {
                        doc.Blocks.Add(new Paragraph(new Run(part.Language))
                        {
                            Margin = new Thickness(0, 8, 0, 2),
                            FontSize = 11,
                            Foreground = Brushes.DimGray,
                            FontStyle = FontStyles.Italic
                        });
                    }

                    var para = new Paragraph
                    {
                        Margin = new Thickness(0, 2, 0, 10),
                        Padding = new Thickness(10),
                        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 210)),
                        BorderThickness = new Thickness(1),
                        FontFamily = new FontFamily("Consolas")
                    };

                    para.Inlines.Add(new Run(part.Content));
                    doc.Blocks.Add(para);
                }
                else
                {
                    AddMarkdownBlocks(doc, part.Content);
                }
            }

            return doc;
        }
        private sealed class ResponseDocumentPart
        {
            public bool IsCode { get; set; }
            public string Content { get; set; } = string.Empty;
            public string Language { get; set; } = string.Empty;
        }

        private List<ResponseDocumentPart> SplitIntoCodeAndTextBlocks(string text)
        {
            var result = new List<ResponseDocumentPart>();

            if (string.IsNullOrEmpty(text))
                return result;

            string pattern = @"```(?<lang>[\w#+-]*)\s*\r?\n(?<code>[\s\S]*?)```";
            var matches = Regex.Matches(text, pattern);

            int currentIndex = 0;

            foreach (Match match in matches)
            {
                if (match.Index > currentIndex)
                {
                    string plain = text.Substring(currentIndex, match.Index - currentIndex);
                    if (!string.IsNullOrWhiteSpace(plain))
                    {
                        result.Add(new ResponseDocumentPart
                        {
                            IsCode = false,
                            Content = plain.Trim()
                        });
                    }
                }

                result.Add(new ResponseDocumentPart
                {
                    IsCode = true,
                    Language = match.Groups["lang"].Value?.Trim() ?? string.Empty,
                    Content = match.Groups["code"].Value.TrimEnd()
                });

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < text.Length)
            {
                string tail = text.Substring(currentIndex);
                if (!string.IsNullOrWhiteSpace(tail))
                {
                    result.Add(new ResponseDocumentPart
                    {
                        IsCode = false,
                        Content = tail.Trim()
                    });
                }
            }

            if (result.Count == 0)
            {
                result.Add(new ResponseDocumentPart
                {
                    IsCode = false,
                    Content = text
                });
            }

            return result;
        }
        private void AddMarkdownBlocks(FlowDocument doc, string text)
        {
            string[] lines = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n');

            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    i++;
                    continue;
                }

                Match headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
                if (headingMatch.Success)
                {
                    int level = headingMatch.Groups[1].Value.Length;
                    string headingText = headingMatch.Groups[2].Value.Trim();
                    doc.Blocks.Add(CreateHeadingParagraph(headingText, level));
                    i++;
                    continue;
                }

                if (Regex.IsMatch(line, @"^\s*>\s+"))
                {
                    var quoteLines = new List<string>();

                    while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s*>\s+"))
                    {
                        quoteLines.Add(Regex.Replace(lines[i], @"^\s*>\s+", "").Trim());
                        i++;
                    }

                    doc.Blocks.Add(CreateBlockQuoteParagraph(string.Join(" ", quoteLines)));
                    continue;
                }

                if (Regex.IsMatch(line, @"^\s*[-*]\s+"))
                {
                    var items = new List<string>();

                    while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s*[-*]\s+"))
                    {
                        items.Add(Regex.Replace(lines[i], @"^\s*[-*]\s+", "").Trim());
                        i++;
                    }

                    doc.Blocks.Add(CreateMarkdownList(items, ordered: false));
                    continue;
                }

                if (Regex.IsMatch(line, @"^\s*\d+\.\s+"))
                {
                    var items = new List<string>();

                    while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s*\d+\.\s+"))
                    {
                        items.Add(Regex.Replace(lines[i], @"^\s*\d+\.\s+", "").Trim());
                        i++;
                    }

                    doc.Blocks.Add(CreateMarkdownList(items, ordered: true));
                    continue;
                }

                var paragraphLines = new List<string>();

                while (i < lines.Length &&
                       !string.IsNullOrWhiteSpace(lines[i]) &&
                       !Regex.IsMatch(lines[i], @"^(#{1,6})\s+") &&
                       !Regex.IsMatch(lines[i], @"^\s*>\s+") &&
                       !Regex.IsMatch(lines[i], @"^\s*[-*]\s+") &&
                       !Regex.IsMatch(lines[i], @"^\s*\d+\.\s+"))
                {
                    paragraphLines.Add(lines[i].Trim());
                    i++;
                }

                string paragraphText = string.Join(" ", paragraphLines).Trim();
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    doc.Blocks.Add(CreateNormalParagraph(paragraphText));
                }
            }
        }

        private Paragraph CreateHeadingParagraph(string text, int level)
        {
            double size = level switch
            {
                1 => 24,
                2 => 20,
                3 => 17,
                4 => 15,
                _ => 14
            };

            var para = new Paragraph
            {
                Margin = new Thickness(0, 10, 0, 6),
                FontSize = size,
                FontWeight = FontWeights.Bold
            };

            AddInlineMarkdown(para, text);
            return para;
        }

        private Paragraph CreateNormalParagraph(string text)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 8)
            };

            AddInlineMarkdown(para, text);
            return para;
        }

        private Paragraph CreateBlockQuoteParagraph(string text)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(8, 4, 0, 8),
                Padding = new Thickness(8, 4, 4, 4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(3, 0, 0, 0),
                Foreground = Brushes.DimGray,
                FontStyle = FontStyles.Italic
            };

            AddInlineMarkdown(para, text);
            return para;
        }

        private System.Windows.Documents.List CreateMarkdownList(IEnumerable<string> items, bool ordered)
        {
            var list = new System.Windows.Documents.List
            {
                Margin = new Thickness(20, 0, 0, 8),
                MarkerStyle = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc
            };

            foreach (string item in items)
            {
                var para = new Paragraph
                {
                    Margin = new Thickness(0)
                };

                AddInlineMarkdown(para, item);

                list.ListItems.Add(new ListItem(para));
            }

            return list;
        }

        private void AddInlineMarkdown(Paragraph paragraph, string text)
        {
            if (paragraph == null)
                return;

            text ??= string.Empty;

            string pattern = @"(\*\*[^*]+\*\*|`[^`]+`|\[(?<label>[^\]]+)\]\((?<url>https?://[^)]+)\))";
            var matches = Regex.Matches(text, pattern);

            int currentIndex = 0;

            foreach (Match match in matches)
            {
                if (match.Index > currentIndex)
                {
                    string plain = text.Substring(currentIndex, match.Index - currentIndex);
                    paragraph.Inlines.Add(new Run(plain));
                }

                string token = match.Value;

                if (token.StartsWith("**") && token.EndsWith("**"))
                {
                    string boldText = token.Substring(2, token.Length - 4);
                    paragraph.Inlines.Add(new Bold(new Run(boldText)));
                }
                else if (token.StartsWith("`") && token.EndsWith("`"))
                {
                    string codeText = token.Substring(1, token.Length - 2);
                    var run = new Run(codeText)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                        Foreground = Brushes.DarkSlateBlue
                    };
                    paragraph.Inlines.Add(run);
                }
                else if (match.Groups["label"].Success && match.Groups["url"].Success)
                {
                    string label = match.Groups["label"].Value;
                    string url = match.Groups["url"].Value;

                    var link = new Hyperlink(new Run(label))
                    {
                        NavigateUri = new Uri(url),
                        ToolTip = "Ctrl+Click to open link"
                    };

                    link.Click += (_, __) =>
                    {
                        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                        {
                            StatusText.Text = "Hold Ctrl while clicking to open links.";
                            return;
                        }

                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                            // ignore
                        }
                    };

                    paragraph.Inlines.Add(link);
                }
                else
                {
                    paragraph.Inlines.Add(new Run(token));
                }

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < text.Length)
            {
                string tail = text.Substring(currentIndex);
                paragraph.Inlines.Add(new Run(tail));
            }
        }
        private IEnumerable<string> SplitPlainTextIntoParagraphs(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p));
        }
        private void ResponsesRichTextContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            string selectedText = new TextRange(
                rtbResponsesResponse.Selection.Start,
                rtbResponsesResponse.Selection.End).Text;

            miResponsesCopySelection.IsEnabled = !string.IsNullOrWhiteSpace(selectedText);
            miResponsesCopyAll.IsEnabled = rtbResponsesResponse.Document != null;
            miResponsesCopyMarkdown.IsEnabled = !string.IsNullOrWhiteSpace(ResponsesState.ResponseText);

            Paragraph currentParagraph = GetCurrentParagraphFromRichTextBox(rtbResponsesResponse);
            bool hasCurrentBlock = currentParagraph != null;
            bool isCodeBlock = hasCurrentBlock && IsCodeParagraph(currentParagraph);

            miResponsesCopyCurrentBlock.IsEnabled = hasCurrentBlock;
            miResponsesCopyCurrentCodeBlock.IsEnabled = isCodeBlock;
        }

        private void miResponsesCopySelection_Click(object sender, RoutedEventArgs e)
        {
            string selectedText = new TextRange(
                rtbResponsesResponse.Selection.Start,
                rtbResponsesResponse.Selection.End).Text?.Trim();

            if (!string.IsNullOrWhiteSpace(selectedText))
                Clipboard.SetText(selectedText);
        }

        private void miResponsesCopyAll_Click(object sender, RoutedEventArgs e)
        {
            string allText = new TextRange(
                rtbResponsesResponse.Document.ContentStart,
                rtbResponsesResponse.Document.ContentEnd).Text?.Trim();

            if (!string.IsNullOrWhiteSpace(allText))
                Clipboard.SetText(allText);
        }

        private void miResponsesCopyCurrentBlock_Click(object sender, RoutedEventArgs e)
        {
            Paragraph paragraph = GetCurrentParagraphFromRichTextBox(rtbResponsesResponse);
            if (paragraph == null)
                return;

            string text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
                Clipboard.SetText(text);
        }

        private void miResponsesCopyCurrentCodeBlock_Click(object sender, RoutedEventArgs e)
        {
            Paragraph paragraph = GetCurrentParagraphFromRichTextBox(rtbResponsesResponse);
            if (paragraph == null || !IsCodeParagraph(paragraph))
                return;

            string text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
                Clipboard.SetText(text);
        }
        private Paragraph GetCurrentParagraphFromRichTextBox(RichTextBox richTextBox)
        {
            if (richTextBox?.CaretPosition == null)
                return null;

            DependencyObject current = richTextBox.CaretPosition.Parent;

            while (current != null)
            {
                if (current is Paragraph paragraph)
                    return paragraph;

                current = LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private bool IsCodeParagraph(Paragraph paragraph)
        {
            if (paragraph == null)
                return false;

            string fontFamily = paragraph.FontFamily?.Source ?? string.Empty;

            bool looksLikeCodeFont =
                fontFamily.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fontFamily.IndexOf("Courier", StringComparison.OrdinalIgnoreCase) >= 0;

            bool hasCodeBackground = paragraph.Background != null;

            return looksLikeCodeFont || hasCodeBackground;
        }
        private void miResponsesCopyMarkdown_Click(object sender, RoutedEventArgs e)
        {
            string markdown = ResponsesState.ResponseText?.Trim();
            if (!string.IsNullOrWhiteSpace(markdown))
                Clipboard.SetText(markdown);
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
        private ScrollViewer GetResponsesRichTextScrollViewer()
        {
            return FindVisualChild<ScrollViewer>(rtbResponsesResponse);
        }
        private void rtbResponsesResponse_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (rtbResponsesResponse == null)
                return;

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A)
            {
                rtbResponsesResponse.SelectAll();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
            {
                string selectedText = new TextRange(
                    rtbResponsesResponse.Selection.Start,
                    rtbResponsesResponse.Selection.End).Text?.Trim();

                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    Clipboard.SetText(selectedText);
                }
                else
                {
                    string allText = new TextRange(
                        rtbResponsesResponse.Document.ContentStart,
                        rtbResponsesResponse.Document.ContentEnd).Text?.Trim();

                    if (!string.IsNullOrWhiteSpace(allText))
                        Clipboard.SetText(allText);
                }

                e.Handled = true;
                return;
            }

            ScrollViewer scrollViewer = GetResponsesRichTextScrollViewer();
            if (scrollViewer == null)
                return;

            switch (e.Key)
            {
                case Key.Down:
                    scrollViewer.LineDown();
                    e.Handled = true;
                    break;

                case Key.Up:
                    scrollViewer.LineUp();
                    e.Handled = true;
                    break;

                case Key.PageDown:
                    scrollViewer.PageDown();
                    e.Handled = true;
                    break;

                case Key.PageUp:
                    scrollViewer.PageUp();
                    e.Handled = true;
                    break;
                case Key.Home:
                    scrollViewer.ScrollToHome();
                    e.Handled = true;
                    break;

                case Key.End:
                    scrollViewer.ScrollToEnd();
                    e.Handled = true;
                    break;
            }
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
                    "Document Files|*.pdf;*.txt;*.md;*.csv;*.json;*.xml;*.docx;*.xlsx;*.cs|" +
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
            var dlg = new OpenFileDialog
            {
                Title = "Select any file inside the repository root",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dlg.ShowDialog() == true)
            {
                ResponsesState.DeveloperRepositoryRoot = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
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
                StatusText.Text = $"Local tool used: {toolName}";
            });

            Debug.WriteLine($"[LOCAL TOOL] {toolName}");
            Debug.WriteLine(argumentsJson);
            Debug.WriteLine(resultJson);

            return Task.CompletedTask;
        }
    }
}
