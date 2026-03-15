using openAIApps.Data;
using System;
using System.Collections.Generic;
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
        private bool _isLoadingVideoSession;
        private void InitVideoState()
        {
            VideoState.PromptText = string.Empty;
            VideoState.ResponseText = string.Empty;
            VideoState.SelectedModel = "sora-2";
            VideoState.SelectedLength = "4";
            VideoState.SelectedSize = "720x1280";
            VideoState.IsRemix = false;
            VideoState.SelectedLibraryVideo = null;
            VideoState.SelectedSessionTurn = null;
        }
        private async Task LoadVideoSessionAsync(int sessionId)
        {
            if (sessionId <= 0)
                return;

            _isLoadingVideoSession = true;
            try
            {
                var history = await _historyService.GetFullSessionHistoryAsync(sessionId);

                ReplaceCurrentVideoMessages(history);
                ApplyVideoSettingsFromHistory(history);
                SelectVideoLibraryItemFromHistory(history);

                if (CurrentVideoMessages.Count > 0)
                {
                    var lastMessage = CurrentVideoMessages.Last();
                    VideoState.SelectedSessionTurn = lastMessage;
                    lstVideoSessionTurns.ScrollIntoView(lastMessage);
                }
                else
                {
                    VideoState.SelectedSessionTurn = null;
                }

                if (VideoState.SelectedLibraryVideo != null)
                    ShowVideoPreviewForLibraryItem(VideoState.SelectedLibraryVideo);

                _activeVideoSessionId = sessionId;
                StatusText.Text = $"Video session loaded: {sessionId}";
            }
            finally
            {
                _isLoadingVideoSession = false;
            }
        }
        private void ReplaceCurrentVideoMessages(IEnumerable<ChatMessage> history)
        {
            CurrentVideoMessages.Clear();

            if (history == null)
                return;

            foreach (var message in history)
            {
                CurrentVideoMessages.Add(message);
            }
        }
        private static string GetVideoAssistantDisplayText(ChatMessage message)
        {
            if (message == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(message.RawJson))
                return message.RawJson;

            return message.Content ?? string.Empty;
        }
        private void ApplyVideoSettingsFromHistory(IReadOnlyList<ChatMessage> history)
        {
            if (history == null || history.Count == 0)
                return;

            var lastUser = history.LastOrDefault(m =>
                string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

            if (lastUser != null)
            {
                VideoState.PromptText = lastUser.Content ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(lastUser.ModelUsed))
                    VideoState.SelectedModel = lastUser.ModelUsed;

                if (!string.IsNullOrWhiteSpace(lastUser.VideoLength))
                    VideoState.SelectedLength = lastUser.VideoLength;

                if (!string.IsNullOrWhiteSpace(lastUser.VideoSize))
                    VideoState.SelectedSize = lastUser.VideoSize;

                VideoState.IsRemix = lastUser.IsRemix;
            }

            var lastAssistant = history.LastOrDefault(m =>
                string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));

            VideoState.ResponseText = lastAssistant != null
                ? GetVideoAssistantDisplayText(lastAssistant)
                : string.Empty;
        }
        private void SelectVideoLibraryItemFromHistory(IReadOnlyList<ChatMessage> history)
        {
            if (history == null || history.Count == 0)
            {
                VideoState.SelectedLibraryVideo = null;
                return;
            }

            var assistantWithRemoteId = history.LastOrDefault(m =>
                string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(m.RemoteId));

            if (assistantWithRemoteId == null)
            {
                VideoState.SelectedLibraryVideo = null;
                return;
            }

            var itemToSelect = _videoHistory.FirstOrDefault(v => v.Id == assistantWithRemoteId.RemoteId);
            VideoState.SelectedLibraryVideo = itemToSelect;

            if (itemToSelect != null)
                lstVideoFiles.ScrollIntoView(itemToSelect);
        }
        private void ShowVideoPreviewForLibraryItem(VideoListItem selectedVideo)
        {
            if (selectedVideo == null)
                return;

            string videosDir = _settings.VideosFolder;
            string localFilePath = Path.Combine(videosDir, selectedVideo.Id + ".mp4");

            if (!File.Exists(localFilePath))
                return;

            try
            {
                var bitmap = GetFirstFrameAsBitmap(localFilePath);
                imgVideo.Source = bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Preview failed: {ex.Message}");
            }
        }
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
            VideoState.ResponseText = JsonSerializer.Serialize(finalStatus, jsonOptions);

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
                //MessageBox.Show(errorDetail, "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = $"Processing Error: {errorDetail}";
            }
        }

        private async void btnVideoGenerateClick(object sender, RoutedEventArgs e)
        {
            string prompt = VideoState.PromptText ?? string.Empty;
            bool isRemix = VideoState.IsRemix;

            if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("Please enter a video prompt.",
                    "Missing Prompt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            VideoListItem selectedSourceVideo = null;
            string sourceVideoId = null;

            if (isRemix)
            {
                selectedSourceVideo = VideoState.SelectedLibraryVideo;
                if (selectedSourceVideo == null)
                {
                    MessageBox.Show("Please select a source video to remix.",
                        "No Source Video",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                sourceVideoId = selectedSourceVideo.Id;
            }

            int sessionId = await StartNewVideoJobSessionAsync(prompt, isRemix, sourceVideoId);

            CurrentVideoMessages.Clear();
            VideoState.SelectedSessionTurn = null;

            await _historyService.AddMessageAsync(
                sessionId,
                "user",
                prompt,
                model: VideoState.SelectedModel,
                videoLength: VideoState.SelectedLength,
                videoSize: VideoState.SelectedSize,
                isRemix: isRemix,
                sourceRemoteId: sourceVideoId
            );

            VideoClient.ResponseVideo? videoResult;

            if (isRemix)
            {
                videoResult = await _videoClient.RemixVideoAsync(selectedSourceVideo.Id, prompt);
            }
            else
            {
                videoResult = await _videoClient.CreateVideoAsync(new VideoClient.RequestVideo
                {
                    Prompt = prompt,
                    Model = VideoState.SelectedModel,
                    Seconds = VideoState.SelectedLength,
                    Size = VideoState.SelectedSize
                });
            }

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            string rawJson = JsonSerializer.Serialize(videoResult, jsonOptions);

            VideoState.ResponseText = rawJson;

            if (videoResult == null || videoResult.Error != null)
            {
                string errorMsg = videoResult?.Error?.Message ?? "Unknown API Error";
                StatusText.Text = $"Failed: {videoResult?.Error?.Code ?? "400"}";

                await _historyService.AddMessageAsync(
                    sessionId,
                    "assistant",
                    $"Error: {errorMsg}",
                    rawJson: rawJson,
                    model: VideoState.SelectedModel,
                    remoteId: videoResult?.Id,
                    sourceRemoteId: sourceVideoId
                );

                MessageBox.Show($"API Error: {errorMsg}",
                    "Generation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                await LoadVideoSessionAsync(sessionId);
                RefreshLogsTab();
                return;
            }

            int assistantMsgId = await _historyService.AddMessageAsync(
                sessionId,
                "assistant",
                isRemix ? "Remix task initiated..." : "Video task initiated...",
                rawJson: JsonSerializer.Serialize(videoResult),
                model: VideoState.SelectedModel,
                remoteId: videoResult?.Id,
                sourceRemoteId: sourceVideoId
            );

            if (videoResult != null && !string.IsNullOrEmpty(videoResult.Id))
            {
                await HandleVideoJobAsync(videoResult, sessionId, assistantMsgId);
            }
            else
            {
                string errorMsg = videoResult?.Error?.Message ?? "The API returned a success code but no Video ID.";
                StatusText.Text = $"Failed: {errorMsg}";
                MessageBox.Show(errorMsg, "API Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await LoadVideoSessionAsync(sessionId);
            RefreshLogsTab();
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
                VideoState.ResponseText = JsonSerializer.Serialize(status, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Optional: update your local _videoHistory so progress/status is current
                selectedVideo.Status = status.Status;
                selectedVideo.Progress = status.Progress;

            }
            catch (Exception ex)
            {
                VideoState.ResponseText = $"Error fetching video status:\n{ex.Message}";
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
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
                return;

            ShowVideoPreviewForLibraryItem(selectedVideo);

            if (_isLoadingVideoSession)
                return;

            var message = await _historyService.GetMessageByRemoteVideoIdAsync(selectedVideo.Id);

            if (message != null)
            {
                await LoadVideoSessionAsync(message.ChatSessionId);
            }
            else
            {
                VideoState.SelectedLibraryVideo = selectedVideo;
                StatusText.Text = $"Video selected: {selectedVideo.Id}";
            }
        }
        private void lstVideoSessionTurns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingVideoSession)
                return;

            if (lstVideoSessionTurns.SelectedItem is not ChatMessage selectedMsg)
                return;

            VideoState.SelectedSessionTurn = selectedMsg;

            if (string.Equals(selectedMsg.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                VideoState.PromptText = selectedMsg.Content ?? string.Empty;
            }
            else if (string.Equals(selectedMsg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                VideoState.ResponseText = GetVideoAssistantDisplayText(selectedMsg);

                if (!string.IsNullOrWhiteSpace(selectedMsg.RemoteId))
                {
                    var itemToSelect = _videoHistory.FirstOrDefault(v => v.Id == selectedMsg.RemoteId);
                    if (itemToSelect != null)
                    {
                        _isLoadingVideoSession = true;
                        try
                        {
                            VideoState.SelectedLibraryVideo = itemToSelect;
                            lstVideoFiles.ScrollIntoView(itemToSelect);
                            ShowVideoPreviewForLibraryItem(itemToSelect);
                        }
                        finally
                        {
                            _isLoadingVideoSession = false;
                        }
                    }
                }
            }
        }
        private void ResetVideoUI(bool resetSettings = false)
        {
            _activeVideoSessionId = null;
            _videoReferencePath = string.Empty;

            CurrentVideoMessages.Clear();
            VideoState.SelectedSessionTurn = null;
            VideoState.SelectedLibraryVideo = null;
            VideoState.PromptText = string.Empty;
            VideoState.ResponseText = string.Empty;
            VideoState.IsRemix = false;

            if (resetSettings)
            {
                VideoState.SelectedModel = "sora-2";
                VideoState.SelectedLength = "4";
                VideoState.SelectedSize = "720x1280";
            }

            imgVideo.Source = new BitmapImage(new Uri("/no_pic.png", UriKind.Relative));

            StatusText.Text = "Ready for a new video job.";
        }
        private void txtVideoPrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingVideoSession)
                return;

            if (_activeVideoSessionId != null && VideoState.IsRemix == false)
            {
                _activeVideoSessionId = null;
                StatusText.Text = "New prompt detected: A new session will be created.";
            }
        }

        private void btnNewVideSession_Click(object sender, RoutedEventArgs e)
        {
            ResetVideoUI();
        }

        private string BuildVideoSessionTitle(string prompt, bool isRemix, string sourceVideoId = null)
        {
            string baseTitle = ExtractTitle(prompt);

            if (!isRemix)
                return baseTitle;

            if (!string.IsNullOrWhiteSpace(sourceVideoId))
                return $"Remix {sourceVideoId}: {baseTitle}";

            return $"Remix: {baseTitle}";
        }

        private async Task<int> StartNewVideoJobSessionAsync(string prompt, bool isRemix, string sourceVideoId = null)
        {
            string title = BuildVideoSessionTitle(prompt, isRemix, sourceVideoId);
            int sessionId = await _historyService.StartNewSessionAsync(title, EndpointType.Video);
            _activeVideoSessionId = sessionId;
            return sessionId;
        }
    }
}