using openAIApps.Data;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static openAIApps.VideoClient;

namespace openAIApps
{
    public partial class MainWindow
    {
        private void cmbModelChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private void cmbVideoLengthChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private void cmbVideoSizeChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private async Task HandleVideoJobAsync(VideoClient.ResponseVideo jobResponse, int sessionId, int assistantMsgId)
        {
            // Update the UI listbox
            var existing = _videoHistory.FirstOrDefault(v => v.Id == jobResponse.Id);
            if (existing == null)
            {
                _videoHistory.Add(new VideoClient.VideoListItem { Id = jobResponse.Id, Status = jobResponse.Status });
            }

            // Show Progress
            var progressWindow = new ProgressWindow("Processing video...");
            progressWindow.Owner = this;
            var cts = new CancellationTokenSource();
            progressWindow.Canceled += (s, _) => cts.Cancel();
            progressWindow.Show();

            var progress = new Progress<double>(value => progressWindow.UpdateProgress(value));
            await _videoClient.MonitorVideoProgressAsync(jobResponse.Id, progress, cts.Token);
            progressWindow.Close();

            // Final check
            var finalStatus = await _videoClient.GetVideoStatusAsync(jobResponse.Id);

            // Display final JSON
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            txtVideoResponse.Text = JsonSerializer.Serialize(finalStatus, jsonOptions);

            if (finalStatus != null && finalStatus.Status == "completed")
            {
                string localFilePath = Path.Combine(_settings.VideosFolder, jobResponse.Id + ".mp4");
                await _historyService.LinkMediaAsync(assistantMsgId, localFilePath, "video/mp4");
                StatusText.Text = "Video Ready.";
            }
            else if (finalStatus?.Error != null)
            {
                // THIS handles if it fails mid-way through processing
                string errorDetail = $"Code: {finalStatus.Error.Code}\nMessage: {finalStatus.Error.Message}";
                await _historyService.AddMessageAsync(sessionId, "assistant", $"Processing Failed: {finalStatus.Error.Message}");
                MessageBox.Show(errorDetail, "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnVideoGenerateClick(object sender, RoutedEventArgs e)
        {
            string prompt = txtVideoPrompt.Text;
            bool isRemix = cbVideoRemix.IsChecked ?? false;

            // 1. Ensure Session & Log User DNA
            int sessionId = await EnsureSessionActiveAsync(EndpointType.Video, prompt);
            await _historyService.AddMessageAsync(
                sessionId, "user", prompt,
                model: cmbVideoModel.Text,
                videoLength: cmbVideoLength.Text,
                videoSize: cmbVideoSize.Text,
                isRemix: isRemix
            );

            // 2. Call API
            VideoClient.ResponseVideo? videoResult;
            if (isRemix)
            {
                if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo) return;
                videoResult = await _videoClient.RemixVideoAsync(selectedVideo.Id, prompt);
            }
            else
            {
                videoResult = await _videoClient.CreateVideoAsync(new VideoClient.RequestVideo
                {
                    Prompt = prompt,
                    Model = cmbVideoModel.Text,
                    Seconds = cmbVideoLength.Text,
                    Size = cmbVideoSize.Text
                });
            }
            // NEW ERROR LOGGING LOGIC ---
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            string rawJson = JsonSerializer.Serialize(videoResult, jsonOptions);

            // Always show the JSON in the text box immediately so you can see billing/safety errors
            txtVideoResponse.Text = rawJson;

            if (videoResult == null || videoResult.Error != null)
            {
                string errorMsg = videoResult?.Error?.Message ?? "Unknown API Error";
                StatusText.Text = $"Failed: {videoResult?.Error?.Code ?? "400"}";

                // Log the failure to the database so it's in your history
                await _historyService.AddMessageAsync(sessionId, "assistant", $"Error: {errorMsg}", rawJson: rawJson);

                MessageBox.Show($"API Error: {errorMsg}", "Generation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return; // STOP HERE. Do not call HandleVideoJobAsync.
            }
            // --- END ERROR LOGGING ---
            // 3. Log Initial Assistant Response (Task Created)
            int assistantMsgId = await _historyService.AddMessageAsync(
                sessionId, "assistant", "Video task initiated...",
                rawJson: JsonSerializer.Serialize(videoResult),
                model: cmbVideoModel.Text,
                remoteId: videoResult?.Id
            );
            if (videoResult != null && !string.IsNullOrEmpty(videoResult.Id))
            {
                // ONLY call this if we actually have an ID to track
                await HandleVideoJobAsync(videoResult, sessionId, assistantMsgId);
            }
            else
            {
                string errorMsg = videoResult?.Error?.Message ?? "The API returned a success code but no Video ID.";
                StatusText.Text = $"Failed: {errorMsg}";
                MessageBox.Show(errorMsg, "API Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnOpenVReference_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a reference image",
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                _videoReferencePath = dlg.FileName;

                // Make sure VideoClient knows about it
                if (_videoClient != null)
                {
                    _videoClient.ReferenceFilePath = _videoReferencePath;
                }

                // Display preview in the Image control
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_videoReferencePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                imgVideo.Source = bitmap;
            }
        }

        private string GetLocalVideoPath(string videoId)
        {
            //string videosDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            string videosDir = _settings.VideosFolder;
            return Path.Combine(videosDir, videoId + ".mp4");
        }

        private bool IsVideoDownloaded(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                return false;

            string path = GetLocalVideoPath(videoId);
            return File.Exists(path);
        }

        private async void InitVideoList()
        {
            try
            {
                var listResponse = await _videoClient.GetAllVideosAsync();
                _videoHistory.Clear();
                if (listResponse?.Data != null)
                {
                    foreach (var item in listResponse.Data)
                    {
                        item.IsDownloaded = IsVideoDownloaded(item.Id);
                        item.HasError = string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase);
                        _videoHistory.Add(item);
                    }

                    lstVideoFiles.ItemsSource = _videoHistory;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load video list:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnDownloadVideo_Click(object sender, RoutedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video to download.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string videoId = selectedVideo.Id;

            // Create progress window
            var progressWindow = new ProgressWindow("Downloading video...");
            progressWindow.Owner = this;
            progressWindow.Show();

            var progress = new Progress<double>(value =>
            {
                progressWindow.UpdateProgress(value);
            });

            bool success = await _videoClient.DownloadVideoAsync(_settings.VideosFolder, videoId, progress);

            progressWindow.Close();

            if (success)
            {
                selectedVideo.IsDownloaded = IsVideoDownloaded(selectedVideo.Id);
                MessageBox.Show($"Video {videoId} downloaded successfully.", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                //update list(item color) to green
                selectedVideo.IsDownloaded = true;
            }
            else
            {
                selectedVideo.HasError = true;
                MessageBox.Show($"Failed to download video {videoId}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private async void btnDeleteVideo_Click(object sender, RoutedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoClient.VideoListItem selectedVideo) return;

            // Ask once, and be specific
            var confirm = MessageBox.Show(
                $"Permanently delete video {selectedVideo.Id} from OpenAI and local storage?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                // Instruction: Delete everything associated with this ID
                await _videoClient.DeleteVideoAsync(selectedVideo.Id, _settings.VideosFolder);

                // Database Cleanup
                var message = await _historyService.GetMessageByRemoteVideoIdAsync(selectedVideo.Id);
                if (message != null)
                {
                    await _historyService.DeleteSessionAsync(message.ChatSessionId);
                    if (_activeVideoSessionId == message.ChatSessionId) ResetVideoUI();
                }

                _videoHistory.Remove(selectedVideo);
                RefreshLogsTab();

                StatusText.Text = "Video and logs deleted.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnGetStatus_Click(object sender, RoutedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var status = await _videoClient.GetVideoStatusAsync(selectedVideo.Id);

                // Show JSON string in your response TextBox (or format nicely)
                txtVideoResponse.Text = JsonSerializer.Serialize(status, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Optional: update your local _videoHistory so progress/status is current
                selectedVideo.Status = status.Status;
                selectedVideo.Progress = status.Progress;

            }
            catch (Exception ex)
            {
                txtVideoResponse.Text = $"Error fetching video status:\n{ex.Message}";
            }
        }

        private void btmRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            // Clear the reference file path
            _videoReferencePath = string.Empty;
            _videoClient.ReferenceFilePath = string.Empty; // if your VideoClient uses this

            // Reset the preview image
            imgVideo.Source = new BitmapImage(new Uri("/no_pic.png", UriKind.Relative));
        }
        private void btnPlayVideo_Click(object sender, RoutedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video first.", "No Selection",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            //string videosDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            string videosDir = _settings.VideosFolder;
            string localFilePath = Path.Combine(videosDir, $"{selectedVideo.Id}.mp4");

            if (!File.Exists(localFilePath))
            {
                MessageBox.Show("Video not found locally. Please download it first.", "Missing File",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Open preview window
            var previewWindow = new VideoPreviewWindow(localFilePath);
            previewWindow.ShowDialog();
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

        private async void lstVideoFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 1. Get the selected video object from the ListBox
            // (Adjust 'dynamic' if you have a specific class for the ListBox items)
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo) return;

            string videosDir = _settings.VideosFolder;
            string localFilePath = Path.Combine(videosDir, selectedVideo.Id + ".mp4");

            // 2. Local File & Preview Logic
            if (File.Exists(localFilePath))
            {
                try
                {
                    // Use your specific frame extraction method
                    var bitmap = GetFirstFrameAsBitmap(localFilePath);
                    imgVideo.Source = bitmap;
                    //_videoPath = localFilePath; // Track current path for playback
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Preview failed: {ex.Message}");
                }
            }

            // 3. Database "Rehydration" Logic
            // Use the OpenAI ID from the selected item to find our logged DNA
            var message = await _historyService.GetMessageByRemoteVideoIdAsync(selectedVideo.Id);

            if (message != null)
            {
                // Set the UI controls back to the state used for this generation
                txtVideoPrompt.Text = message.Content;
                cmbVideoModel.Text = message.ModelUsed;
                cmbVideoLength.Text = message.VideoLength;
                cmbVideoSize.Text = message.VideoSize;
                cbVideoRemix.IsChecked = message.IsRemix;

                // Update the active session ID so new remixes stay in this thread
                _activeVideoSessionId = message.ChatSessionId;

                StatusText.Text = $"Video loaded from log. ID: {selectedVideo.Id}";
            }
        }
        private void ResetVideoUI()
        {
            // 1. Clear State
            _activeVideoSessionId = null;
            _videoReferencePath = string.Empty;
            //_videoPath = string.Empty;

            // 2. Clear UI
            txtVideoPrompt.Text = string.Empty;
            txtVideoResponse.Text = string.Empty;
            imgVideo.Source = new BitmapImage(new Uri("/no_pic.png", UriKind.Relative)); ; // Or your 'no_pic.png' placeholder
            cbVideoRemix.IsChecked = false;

            // 3. Reset ComboBoxes to defaults
            cmbVideoModel.SelectedIndex = 0;
            cmbVideoLength.SelectedIndex = 0;
            cmbVideoSize.SelectedIndex = 0;

            StatusText.Text = "Ready for a new video session.";
        }
        private void txtVideoPrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            // If the user starts typing a NEW prompt while looking at an OLD session...
            // AND it is not a Remix attempt...
            if (_activeVideoSessionId != null && cbVideoRemix.IsChecked == false)
            {
                // We "Detach" from the old session. 
                // This ensures the next 'Generate' creates a NEW session with a NEW title.
                _activeVideoSessionId = null;
                StatusText.Text = "New prompt detected: A new session will be created.";
            }
        }

        private void btnNewVideSession_Click(object sender, RoutedEventArgs e)
        {
            ResetVideoUI();
        }
    }
}
