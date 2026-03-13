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


        private void ApplyModelsToResponsesCombo(IEnumerable<string> models, string preferredModel = "gpt-4o")
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
                _responsesClient.CurrentModel = string.Empty;
                return;
            }

            string selectedModel =
                list.FirstOrDefault(m => m.Equals(_responsesClient.CurrentModel, StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(m => m.Equals(preferredModel, StringComparison.OrdinalIgnoreCase))
                ?? list[0];

            int selectedIndex = list.FindIndex(m => m.Equals(selectedModel, StringComparison.OrdinalIgnoreCase));
            cmbResponsesModel.SelectedIndex = selectedIndex;
            _responsesClient.CurrentModel = selectedModel;
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

            // Tools
            _responsesClient.ActiveTools.Clear();
            _responsesClient.ActiveTools.Add(ResponseToolKeys.Text);
            cbToolText.IsChecked = true;
            cbToolWebSearch.IsChecked = false;
            cbToolComputerUse.IsChecked = false;
            _responsesClient.WebSearchContextSize = "medium";
            cmbSearchContextSize.SelectedIndex = 1;
            cmbSearchContextSize.IsEnabled = false;

            // Reasoning
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
        private ChatMessage GetSelectedResponseMessage()
        {
            return lstResponsesTurns.SelectedItem as ChatMessage;
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

            if (CurrentChatMessages.Count > 0)
            {
                var lastMessage = CurrentChatMessages.Last();
                lstResponsesTurns.SelectedItem = lastMessage;
                lstResponsesTurns.ScrollIntoView(lastMessage);
            }

            if (restoreLastUserPrompt)
            {
                var lastUserMessage = history.LastOrDefault(m =>
                    string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

                if (lastUserMessage != null)
                {
                    txtResponsesPrompt.Text = lastUserMessage.Content;
                }
            }

            if (lstResponsesTurns.SelectedItem is ChatMessage selected &&
                string.Equals(selected.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                txtResponsesResponse.Text = selected.Content;
                ShowFirstAssistantImageOfSelectedTurn();
            }
            else
            {
                HideResponsesImagePreview();
            }
        }

        private void ResetResponsesUi(bool clearPrompt = true)
        {
            CurrentChatMessages.Clear();
            txtResponsesResponse.Clear();

            if (clearPrompt)
                txtResponsesPrompt.Clear();

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

        private void cbTool_Checked(object sender, RoutedEventArgs e)
        {
            if (_responsesClient == null || sender is not CheckBox cb)
                return;

            if (sender == cbToolComputerUse && cbToolComputerUse.IsChecked == true)
            {
                cbToolImageGeneration.IsChecked = false;
                cbToolWebSearch.IsChecked = false;
            }

            string key = cb.Name switch
            {
                "cbToolText" => ResponseToolKeys.Text,
                "cbToolWebSearch" => ResponseToolKeys.WebSearch,
                "cbToolComputerUse" => ResponseToolKeys.ComputerUsePreview,
                "cbToolImageGeneration" => ResponseToolKeys.ImageGeneration,
                _ => null
            };

            if (key == null)
                return;

            if (cb.IsChecked == true)
            {
                if (key == ResponseToolKeys.Text)
                {
                    _responsesClient.ActiveTools.Clear();
                    _responsesClient.ActiveTools.Add(ResponseToolKeys.Text);

                    cbToolWebSearch.IsChecked = false;
                    cbToolComputerUse.IsChecked = false;
                    cbToolImageGeneration.IsChecked = false;
                }
                else
                {
                    _responsesClient.ActiveTools.Remove(ResponseToolKeys.Text);
                    cbToolText.IsChecked = false;
                    _responsesClient.ActiveTools.Add(key);
                }
            }
            else
            {
                _responsesClient.ActiveTools.Remove(key);

                if (_responsesClient.ActiveTools.Count == 0)
                {
                    _responsesClient.ActiveTools.Add(ResponseToolKeys.Text);
                    cbToolText.IsChecked = true;
                }
            }

            bool imageToolOn = cbToolImageGeneration.IsChecked == true;
            cmbImageGenQuality.IsEnabled = imageToolOn;
            cmbImageGenSize.IsEnabled = imageToolOn;

            bool webSearchOn = cbToolWebSearch.IsChecked == true;
            cmbSearchContextSize.IsEnabled = webSearchOn;
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
            string userPrompt = txtResponsesPrompt.Text ?? string.Empty;
            bool hasAttachedImage =
                !string.IsNullOrWhiteSpace(_responsesImagePath) &&
                File.Exists(_responsesImagePath);

            if (string.IsNullOrWhiteSpace(userPrompt) && !hasAttachedImage)
                return;

            this.IsEnabled = false;

            try
            {
                string model = cmbResponsesModel.Text;
                string reasoning = cmbReasoning.Text;
                string imgSize = cmbImageGenSize.Text;
                string imgQual = cmbImageGenQuality.Text;
                string searchSize = cmbSearchContextSize.Text;

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
                    txtResponsesResponse.Text = result.AssistantText;
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
            if (lstResponsesTurns.SelectedItem is not ChatMessage selectedMsg)
                return;

            string path = GetPrimaryMediaPath(selectedMsg);

            if (string.Equals(selectedMsg.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                txtResponsesPrompt.Text = selectedMsg.Content;
                txtResponsesResponse.Text = string.Empty;

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    ShowResponsesImagePreview(path);
                else
                    HideResponsesImagePreview();
            }
            else if (string.Equals(selectedMsg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                txtResponsesResponse.Text = selectedMsg.Content;

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    ShowResponsesImagePreview(path);
                else
                    HideResponsesImagePreview();
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

        private void cmbImageGenQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_responsesClient == null)
                return;

            if (cmbImageGenQuality.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag)
            {
                // Tag contains "auto", "low", "medium", "high"
                _responsesClient.ImageGenQuality = tag.ToLowerInvariant();
            }
        }

        private void cmbImageGenSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_responsesClient == null)
                return;

            if (cmbImageGenSize.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag)
            {
                // Tag contains "auto" or "WxH"
                _responsesClient.ImageGenSize = tag;
            }
        }

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
    }
}
