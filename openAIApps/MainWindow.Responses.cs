using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using openAIApps.Data;
using openAIApps.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

            ApplyResponsesStateToClient();
        }
        private ChatMessage GetSelectedResponseMessage()
        {
            return ResponsesState.SelectedTurn;
        }

        private string GetPrimaryMediaPath(ChatMessage message)
        {
            return message?.MediaFiles?.FirstOrDefault()?.LocalPath;
        }

        private void ShowResponsesImagePreview(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                HideResponsesImagePreview();
                return;
            }

            imgResponsesPreview.Source = GetImageSource(path);
            _responsesImagePath = path;

            borderResponsesImage.Visibility = Visibility.Visible;
            colResponsesPrompt.Width = new GridLength(2, GridUnitType.Star);
            colResponsesImage.Width = new GridLength(1, GridUnitType.Star);
        }

        private void HideResponsesImagePreview()
        {
            imgResponsesPreview.Source = null;
            borderResponsesImage.Visibility = Visibility.Collapsed;
            colResponsesPrompt.Width = new GridLength(1, GridUnitType.Star);
            colResponsesImage.Width = new GridLength(0);
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

            var history = await _historyService.GetFullSessionHistoryAsync(sessionId);

            ReplaceCurrentChatMessages(history);
            ApplyResponsesSettingsFromHistory(history);

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

            if (ResponsesState.SelectedTurn is ChatMessage selected &&
                string.Equals(selected.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                ResponsesState.ResponseText = selected.Content;
                ShowFirstAssistantImageOfSelectedTurn();
            }
            else
            {
                ResponsesState.ResponseText = string.Empty;
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
            HideResponsesImagePreview();

            if (_responsesClient != null)
                _responsesClient.ClearConversation();
        }
        private string GetCurrentPreviewImagePath()
        {
            string path = GetPrimaryMediaPath(GetSelectedResponseMessage());

            if (string.IsNullOrWhiteSpace(path))
                path = _responsesImagePath;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            return path;
        }

        private void ShowFirstAssistantImageOfSelectedTurn()
        {
            var message = GetSelectedResponseMessage();

            if (message == null ||
                !string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                HideResponsesImagePreview();
                return;
            }

            string path = GetPrimaryMediaPath(message);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                HideResponsesImagePreview();
                return;
            }

            ShowResponsesImagePreview(path);
        }
        private async void btnResponsesSendRequestClick(object sender, RoutedEventArgs e)
        {
            string userPrompt = ResponsesState.PromptText ?? string.Empty;
            bool hasAttachedImage =
                !string.IsNullOrWhiteSpace(_responsesImagePath) &&
                File.Exists(_responsesImagePath);

            if (string.IsNullOrWhiteSpace(userPrompt) && !hasAttachedImage)
                return;

            this.IsEnabled = false;

            try
            {
                string model = ResponsesState.SelectedModel;
                string reasoning = ResponsesState.SelectedReasoning;
                string imgSize = ResponsesState.ImageGenSize;
                string imgQual = ResponsesState.ImageGenQuality;
                string searchSize = ResponsesState.SearchContextSize;

                string toolsCsv = string.Join(",",
                    _responsesClient.ActiveTools
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase));

                string titleSeed = !string.IsNullOrWhiteSpace(userPrompt) ? userPrompt : "[image prompt]";
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
                    searchSize: searchSize);

                if (hasAttachedImage)
                {
                    string storedImagePath = _mediaStorageService.ImportUserImage(_responsesImagePath);

                    if (!string.IsNullOrWhiteSpace(storedImagePath))
                    {
                        await _historyService.LinkMediaAsync(
                            userMsgId,
                            storedImagePath,
                            ImageInputHelper.GetMimeType(storedImagePath));
                    }
                }

                var context = await _historyService.GetContextForApiAsync(sid);
                var result = await _responsesClient.GetChatCompletionAsync(context);

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
                        searchSize: searchSize);

                    if (result.ImagePayloads?.Count > 0)
                    {
                        var paths = _mediaStorageService.SaveAssistantImages(result.ImagePayloads);

                        foreach (var path in paths)
                        {
                            await _historyService.LinkMediaAsync(
                                assistantMsgId,
                                path,
                                ImageInputHelper.GetMimeType(path));
                        }
                    }

                    _responsesImagePath = string.Empty;

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

            string path = GetPrimaryMediaPath(selectedMsg);

            if (string.Equals(selectedMsg.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                ResponsesState.PromptText = selectedMsg.Content;
                ResponsesState.ResponseText = string.Empty;

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    ShowResponsesImagePreview(path);
                else
                    HideResponsesImagePreview();
            }
            else if (string.Equals(selectedMsg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                ResponsesState.ResponseText = selectedMsg.Content;

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    ShowResponsesImagePreview(path);
                else
                    HideResponsesImagePreview();
            }
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

            string path = GetPrimaryMediaPath(message);

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
                !string.IsNullOrWhiteSpace(message.ImageQuality);
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

                ApplyResponsesToolsToState(settingsMessage.ActiveTools);
            }
            finally
            {
                _isApplyingResponsesSettings = false;
            }

            NormalizeResponsesToolsState();
            ApplyResponsesStateToClient();
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
    }
}
