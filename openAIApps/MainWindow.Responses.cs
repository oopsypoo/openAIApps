using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using openAIApps.Data;
using openAIApps.Native;
using openAIApps.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using static openAIApps.VideoClient;

/*
 * Code for Responses-tab-controls
*/

namespace openAIApps
{
    public partial class MainWindow
    {

        private void InitResponsesControls()
        {
            _responsesClient = new Responses(OpenAPIKey);

            // Frontier GPT-5 family (reasoning-enabled)
            cmbResponsesModel.Items.Clear();
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5.2", Tag = "gpt-5.2" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5.2-pro", Tag = "gpt-5.2-pro" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5.1", Tag = "gpt-5.1" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5-pro", Tag = "gpt-5-pro" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-5-mini", Tag = "gpt-5-mini" });

            // GPT-4.1 family (non-reasoning)
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-4.1", Tag = "gpt-4.1" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-4.1-mini", Tag = "gpt-4.1-mini" });

            // GPT-4o series
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-4o", Tag = "gpt-4o", IsSelected = true });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "gpt-4o-mini", Tag = "gpt-4o-mini" });

            // Dedicated reasoning models (o-series)
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "o3", Tag = "o3" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "o3-pro", Tag = "o3-pro" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "o3-mini", Tag = "o3-mini" });
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "o4-mini", Tag = "o4-mini" });

            // Specialized / preview
            cmbResponsesModel.Items.Add(new ComboBoxItem { Content = "computer-use-preview", Tag = "computer-use-preview" });

            cmbResponsesModel.SelectedIndex = 6; // gpt-4o (safe default)
            _responsesClient.CurrentModel = "gpt-4o";

            // Tools (unchanged)
            _responsesClient.ActiveTools.Clear();
            _responsesClient.ActiveTools.Add("text");       // conceptual default
            cbToolText.IsChecked = true;
            cbToolWebSearch.IsChecked = false;
            cbToolComputerUse.IsChecked = false;
            _responsesClient.WebSearchContextSize = "medium";
            cmbSearchContextSize.SelectedIndex = 1; // medium
            cmbSearchContextSize.IsEnabled = false;



            // Reasoning levels (per docs)
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

        private string GetCurrentPreviewImagePath()
        {
            string path = null;

            if (lstResponsesTurns.SelectedItem is Responses.ResponsesTurn turn)
            {
                if (turn.AssistantImagePaths != null && turn.AssistantImagePaths.Count > 0)
                {
                    path = turn.AssistantImagePaths[0];
                }
                else if (!string.IsNullOrEmpty(turn.ImagePath))
                {
                    path = turn.ImagePath;
                }
            }

            if (string.IsNullOrEmpty(path))
                path = _responsesImagePath;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            return path;
        }

        private void cbTool_Checked(object sender, RoutedEventArgs e)
        {
            if (_responsesClient == null || sender is not CheckBox cb)
                return;
            if (sender == cbToolComputerUse && cbToolComputerUse.IsChecked == true)
            {
                // Disable and uncheck others
                cbToolImageGeneration.IsChecked = false;
                cbToolWebSearch.IsChecked = false;
                // ... etc
            }
            string key = cb.Name switch
            {
                "cbToolText" => "text",
                "cbToolWebSearch" => "web_search",
                "cbToolComputerUse" => "computer_use_preview",
                "cbToolImageGeneration" => "image_generation", // NEW
                _ => null
            };

            if (key == null)
                return;

            if (cb.IsChecked == true)
            {
                if (key == "text")
                {
                    _responsesClient.ActiveTools.Clear();
                    _responsesClient.ActiveTools.Add("text");

                    cbToolWebSearch.IsChecked = false;
                    cbToolComputerUse.IsChecked = false;
                    cbToolImageGeneration.IsChecked = false;
                }
                else
                {
                    _responsesClient.ActiveTools.Remove("text");
                    cbToolText.IsChecked = false;
                    _responsesClient.ActiveTools.Add(key);
                }
            }
            else
            {
                _responsesClient.ActiveTools.Remove(key);

                if (_responsesClient.ActiveTools.Count == 0)
                {
                    _responsesClient.ActiveTools.Add("text");
                    cbToolText.IsChecked = true;
                }
            }
            
            // Optionally enable/disable quality/size controls when checkbox changes:
            bool imageToolOn = cbToolImageGeneration.IsChecked == true;
            cmbImageGenQuality.IsEnabled = imageToolOn;
            cmbImageGenSize.IsEnabled = imageToolOn;

            bool webSearchOn = cbToolWebSearch.IsChecked == true;
            cmbSearchContextSize.IsEnabled = webSearchOn;
        }

        private void ShowFirstAssistantImageOfSelectedTurn()
        {
            // 1. Check if the selected item is our new ChatMessage model
            if (lstResponsesTurns.SelectedItem is ChatMessage message)
            {
                // 2. Look at the MediaFiles collection we defined in the database model
                if (message.MediaFiles != null && message.MediaFiles.Count > 0)
                {
                    // Grab the path from the first media file (usually the generated image)
                    string path = message.MediaFiles.FirstOrDefault()?.LocalPath;

                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        imgResponsesPreview.Source = GetImageSource(path);
                        _responsesImagePath = path;

                        // Show the UI elements
                        borderResponsesImage.Visibility = Visibility.Visible;
                        colResponsesPrompt.Width = new GridLength(2, GridUnitType.Star);
                        colResponsesImage.Width = new GridLength(1, GridUnitType.Star);
                        return; // Image found and shown
                    }
                }
            }

            // 3. Fallback: If no image found, hide the preview area
            borderResponsesImage.Visibility = Visibility.Collapsed;
            colResponsesPrompt.Width = new GridLength(1, GridUnitType.Star); // Reset layout
            colResponsesImage.Width = new GridLength(0);
        }
        private async void btnResponsesSendRequestClick(object sender, RoutedEventArgs e)
        {
            string userPrompt = txtResponsesPrompt.Text;
            if (string.IsNullOrWhiteSpace(userPrompt)) return;

            this.IsEnabled = false;
            try
            {
                // Capture UI "Snapshot"
                string model = cmbResponsesModel.Text;
                string reasoning = cmbReasoning.Text;
                string imgSize = cmbImageGenSize.Text;
                string imgQual = cmbImageGenQuality.Text;
                string searchSize = cmbSearchContextSize.Text;

                // Helper to get active tools as a string
                var activeToolsList = new List<string>();
                if (cbToolImageGeneration.IsChecked == true) activeToolsList.Add("image_generation");
                if (cbToolWebSearch.IsChecked == true) activeToolsList.Add("web_tool");
                if (cbToolComputerUse.IsChecked == true) activeToolsList.Add("computer_use");
                string toolsCsv = string.Join(",", activeToolsList);

                // 1. Get or Create Session
                int sid = await EnsureSessionActiveAsync(EndpointType.Responses, userPrompt);

                // Save User Prompt with the DNA
                await _historyService.AddMessageAsync(sid, "user", userPrompt,
                    model: model, reasoning: reasoning, tools: toolsCsv,
                    imgSize: imgSize, imgQual: imgQual, searchSize: searchSize);

                // 3. Fetch the full history (now includes the prompt from step 2)
                var context = await _historyService.GetContextForApiAsync(sid);

                // 4. Call API with the full context
                var result = await _responsesClient.GetChatCompletionAsync(context);

                if (result != null)
                {
                    // Save Assistant Response with the SAME DNA
                    int assistantMsgId = await _historyService.AddMessageAsync(sid, "assistant", result.AssistantText,
                        result.RawJson, model: model, reasoning: reasoning, tools: toolsCsv,
                        imgSize: imgSize, imgQual: imgQual, searchSize: searchSize);

                    // 6. If images were generated, save those paths too
                    if (result.ImagePayloads?.Count > 0)
                    {
                        var paths = SaveAssistantImages(result.ImagePayloads);
                        foreach (var path in paths)
                            await _historyService.LinkMediaAsync(assistantMsgId, path, "image/png");
                    }

                    // 7. Refresh UI from the Single Source of Truth (Database)
                    await RefreshCurrentChatUI(sid);
                    txtResponsesResponse.Text = result.AssistantText;
                    //txtResponsesPrompt.Clear();
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
            if (sessionId <= 0) return;

            // Get the full list of ChatMessage objects from SQLite
            var history = await _historyService.GetFullSessionHistoryAsync(sessionId);

            // Update the ListBox
            lstResponsesTurns.ItemsSource = history;

            // Auto-scroll to the bottom so the newest message is visible
            if (lstResponsesTurns.Items.Count > 0)
            {
                var lastMsg = history.Last();
                lstResponsesTurns.SelectedItem = lastMsg;
                lstResponsesTurns.ScrollIntoView(lastMsg);
            }
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
            //_currentResponsesSessionId = 0;
            _activeResponsesSessionId = null;

            // 2. Clear the UI elements
            lstResponsesTurns.ItemsSource = null;
            txtResponsesResponse.Clear();
            txtResponsesPrompt.Clear();

            // Optional: Reset the client-side context tracking
            //_responsesClient.LastResponseId = null;

            StatusText.Text = "New session started. History will be saved once you send a message.";
        }

        private async void btnResponsesDeleteChat_Click(object sender, RoutedEventArgs e)
        {
            if (_activeResponsesSessionId == null) return;

            var confirm = MessageBox.Show("Delete this conversation from history?", "Confirm", MessageBoxButton.YesNo);
            if (confirm == MessageBoxResult.Yes)
            {
                if (_activeResponsesSessionId != null)
                {
                    // Delete from SQLite and clear UI
                    StatusText.Text = "Deleting session...: " + _activeResponsesSessionId.Value;
                    await _historyService.DeleteSessionAsync(_activeResponsesSessionId.Value);
                    //await DeleteSessionWithMediaAsync(_historyService.);
                    _activeResponsesSessionId = null;
                    lstResponsesTurns.ItemsSource = null;
                    txtResponsesPrompt.Clear();
                    txtResponsesResponse.Clear();
                    RefreshLogsTab();
                }
            }
        }
        private async Task DeleteSessionWithMediaAsync(ChatSession session)
        {
            // Join Media with Messages to filter by SessionId
            var mediaFiles = await _context.Media
                .Join(_context.Messages,
                        media => media.ChatMessageId,
                        msg => msg.Id,
                        (media, msg) => new { media, msg })
                .Where(x => x.msg.ChatSessionId == session.Id)
                .Select(x => x.media)
                .ToListAsync();

            // 2. Delete local files from disk
            foreach (var file in mediaFiles)
            {
                if (File.Exists(file.LocalPath)) File.Delete(file.LocalPath);
            }

            // 3. Remove session (Cascading delete handles the DB records)
            _context.Sessions.Remove(session);
            await _context.SaveChangesAsync();
            
        }
        private void lstResponsesTurns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstResponsesTurns.SelectedItem is ChatMessage selectedMsg)
            {
                if (selectedMsg.Role.ToLower() == "user")
                {
                    txtResponsesPrompt.Text = selectedMsg.Content;
                    // Clear the response box or leave it? Usually better to clear 
                    // so the user knows this is a "Prompt" selection
                    txtResponsesResponse.Text = string.Empty;
                }
                else if (selectedMsg.Role.ToLower() == "assistant")
                {
                    txtResponsesResponse.Text = selectedMsg.Content;
                    // Show image if this message has one
                    ShowFirstAssistantImageOfSelectedTurn();
                }
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
                InitialDirectory = _settings.ImagesFolder
            };

            if (dlg.ShowDialog() == true)
            {
                _responsesImagePath = dlg.FileName;

                imgResponsesPreview.Source = GetImageSource(_responsesImagePath);
                borderResponsesImage.Visibility = Visibility.Visible;

                // Switch to 2/3–1/3 layout
                colResponsesPrompt.Width = new GridLength(2, GridUnitType.Star);
                colResponsesImage.Width = new GridLength(1, GridUnitType.Star);
            }

        }
        private void btnResponsesRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            _responsesImagePath = string.Empty;
            imgResponsesPreview.Source = null;
            borderResponsesImage.Visibility = Visibility.Collapsed;

            // Restore full-width prompt
            colResponsesPrompt.Width = new GridLength(1, GridUnitType.Star);
            colResponsesImage.Width = new GridLength(0);
        }

        public static class ImageInputHelper
        {
            public static string ToDataUrl(string filePath)
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return null;

                var ext = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant();
                if (ext == "jpg") ext = "jpeg";

                var bytes = File.ReadAllBytes(filePath);
                var b64 = Convert.ToBase64String(bytes);
                return $"data:image/{ext};base64,{b64}";
            }
        }
        private string EnsureResponsesImageFolder()
        {
            //string picturesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"openapi_responses");
            string picturesRoot = _settings.ImagesFolder;
            Directory.CreateDirectory(picturesRoot);
            return picturesRoot;
        }

        private List<string> SaveAssistantImages(List<string> payloads)
        {
            var savedPaths = new List<string>();
            if (payloads == null || payloads.Count == 0)
                return savedPaths;

            string folder = EnsureResponsesImageFolder();

            foreach (var payload in payloads)
            {
                if (string.IsNullOrWhiteSpace(payload))
                    continue;

                string filePath = null;

                try
                {
                    if (payload.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    {
                        // data URL
                        var commaIndex = payload.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            string meta = payload.Substring(0, commaIndex);
                            string b64 = payload.Substring(commaIndex + 1);

                            string ext = ".png";
                            if (meta.Contains("jpeg")) ext = ".jpg";

                            byte[] bytes = Convert.FromBase64String(b64);
                            string name = $"resp_{DateTime.Now:yyyyMMdd_HHmmss_fff}{ext}";
                            filePath = Path.Combine(folder, name);
                            File.WriteAllBytes(filePath, bytes);
                        }
                    }
                    else if (payload.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             payload.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        // TODO: implement download if needed
                    }
                    else
                    {
                        // Assume pure base64 png (image_generation_call result)
                        byte[] bytes = Convert.FromBase64String(payload);
                        string name = $"resp_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                        filePath = Path.Combine(folder, name);
                        File.WriteAllBytes(filePath, bytes);
                    }

                    if (!string.IsNullOrEmpty(filePath))
                        savedPaths.Add(filePath);
                }
                catch
                {
                    // ignore individual failures
                }
            }

            return savedPaths;
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
            if (lstResponsesTurns.SelectedItem is not Responses.ResponsesTurn turn)
                return;

            // Prefer assistant image if available, otherwise user image
            string path = null;

            if (turn.AssistantImagePaths != null && turn.AssistantImagePaths.Count > 0)
            {
                path = turn.AssistantImagePaths[0];
            }
            else if (!string.IsNullOrEmpty(turn.ImagePath))
            {
                path = turn.ImagePath;
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true // open with default image viewer
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
