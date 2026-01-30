using Microsoft.Win32;
using openAIApps.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

            string key = cb.Name switch
            {
                "cbToolText" => "text",
                "cbToolWebSearch" => "web_search",
                "cbToolComputerUse" => "computer_use",
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
        }

        private void ShowFirstAssistantImageOfSelectedTurn()
        {
            if (lstResponsesTurns.SelectedItem is Responses.ResponsesTurn turn &&
                turn.AssistantImagePaths != null &&
                turn.AssistantImagePaths.Count > 0)
            {
                string path = turn.AssistantImagePaths[0];
                if (File.Exists(path))
                {
                    imgResponsesPreview.Source = GetImageSource(path);
                    _responsesImagePath = path;

                    borderResponsesImage.Visibility = Visibility.Visible;
                    colResponsesPrompt.Width = new GridLength(2, GridUnitType.Star);
                    colResponsesImage.Width = new GridLength(1, GridUnitType.Star);
                }
            }
        }

        // Your existing btnResponsesSendRequest_Click stays the same, just simpler:
        private async void btnResponsesSendRequestClick(object sender, RoutedEventArgs e)
        {
            if (_responsesClient == null)
            {
                MessageBox.Show("Responses client not initialized.", "Error");
                return;
            }

            string prompt = txtResponsesPrompt.Text;
            this.IsEnabled = false;
            txtResponsesResponse.Text = string.Empty;

            try
            {
                var result = await _responsesClient.GetResponseAsync(prompt, _responsesImagePath);

                // Attach user text to last turn
                _responsesClient.SetLastUserText(prompt);

                // Attach user image + assistant images to last turn
                if (_responsesClient.ConversationLog.Count > 0)
                {
                    var lastTurn = _responsesClient.ConversationLog[^1];

                    lastTurn.ImagePath = _responsesImagePath;

                    // Save and attach assistant images
                    var savedPaths = SaveAssistantImages(result.ImagePayloads);
                    lastTurn.AssistantImagePaths.AddRange(savedPaths);
                }

                // Refresh listbox
                lstResponsesTurns.ItemsSource = null;
                lstResponsesTurns.ItemsSource = _responsesClient.ConversationLog;
                if (_responsesClient.ConversationLog.Count > 0)
                    lstResponsesTurns.SelectedIndex = _responsesClient.ConversationLog.Count - 1;

                txtResponsesResponse.Text = result.AssistantText;

                // Optionally show first assistant image somewhere
                ShowFirstAssistantImageOfSelectedTurn();
            }
            catch (Exception ex)
            {
                txtResponsesResponse.Text = $"Error: {ex.Message}";
            }
            finally
            {
                this.IsEnabled = true;
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
            _responsesClient?.ClearConversation();
            _responsesClient?.ConversationLog.Clear();

            lstResponsesTurns.ItemsSource = null;
            txtResponsesPrompt.Clear();
            txtResponsesResponse.Clear();
        }

        private async void btnResponsesDeleteChat_Click(object sender, RoutedEventArgs e)
        {
            if (_responsesClient == null || !_responsesClient.ConversationActive)
            {
                MessageBox.Show("No active conversation to delete.", "Info");
                return;
            }

            var confirm = MessageBox.Show(
                "Delete the current conversation on server and clear history?",
                "Delete Conversation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                bool ok = await _responsesClient.DeleteConversationAsync(_responsesClient.LastResponseId);
                if (ok)
                    MessageBox.Show("Conversation deleted.", "Deleted");

                _responsesClient.ClearConversation();
                _responsesClient.ConversationLog.Clear();
                lstResponsesTurns.ItemsSource = null;
                txtResponsesPrompt.Clear();
                txtResponsesResponse.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting conversation: {ex.Message}", "Error");
            }
        }


        private void lstResponsesTurns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstResponsesTurns.SelectedItem is not Responses.ResponsesTurn turn)
                return;

            // Restore text
            txtResponsesPrompt.Text = turn.UserText ?? string.Empty;
            txtResponsesResponse.Text = turn.AssistantText ?? string.Empty;

            // 1) Restore user image (if this turn had one)
            if (!string.IsNullOrEmpty(turn.ImagePath) && File.Exists(turn.ImagePath))
            {
                _responsesImagePath = turn.ImagePath;
                imgResponsesPreview.Source = GetImageSource(turn.ImagePath);

                borderResponsesImage.Visibility = Visibility.Visible;
                colResponsesPrompt.Width = new GridLength(2, GridUnitType.Star);
                colResponsesImage.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // No user image: clear preview and go back to full width
                _responsesImagePath = string.Empty;
                imgResponsesPreview.Source = null;
                borderResponsesImage.Visibility = Visibility.Collapsed;
                colResponsesPrompt.Width = new GridLength(1, GridUnitType.Star);
                colResponsesImage.Width = new GridLength(0);
            }

            // 2) Optionally override with first assistant image (if present)
            // If you prefer to always show assistant image in the preview instead of user image
            // you can move this block above the "else" above.
            if (turn.AssistantImagePaths != null && turn.AssistantImagePaths.Count > 0)
            {
                string firstAssistantImage = turn.AssistantImagePaths[0];
                if (File.Exists(firstAssistantImage))
                {
                    imgResponsesPreview.Source = GetImageSource(firstAssistantImage);
                    // If you want the working image to become the assistant image:
                    _responsesImagePath = firstAssistantImage;

                    borderResponsesImage.Visibility = Visibility.Visible;
                    colResponsesPrompt.Width = new GridLength(2, GridUnitType.Star);
                    colResponsesImage.Width = new GridLength(1, GridUnitType.Star);
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
        private BitmapImage GetFirstFrameAsBitmap(string videoPath)
        {
            string thumbPath = Path.ChangeExtension(videoPath, ".thumb.png");

            if (!File.Exists(thumbPath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{videoPath}\" -frames:v 1 \"{thumbPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p.WaitForExit();
            }

            if (!File.Exists(thumbPath))
                throw new FileNotFoundException("Thumbnail not created", thumbPath);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(thumbPath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private void lstVideoFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
                return;

            string videosDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            string localFilePath = Path.Combine(videosDir, selectedVideo.Id + ".mp4");

            if (!File.Exists(localFilePath))
            {
                // Not downloaded: optionally clear or keep current preview
                return;
            }

            // Extract first frame and show in imgVideo
            try
            {
                var bitmap = GetFirstFrameAsBitmap(localFilePath);
                imgVideo.Source = bitmap;
            }
            catch
            {
                // Ignore preview errors for now or log
            }
        }
        private void btnResponsesAttachImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select an image for this message",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*",
                CheckFileExists = true
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
            // Reuse your existing appRoot if you like; for now keep it simple
            string picturesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "openapi_responses");

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
