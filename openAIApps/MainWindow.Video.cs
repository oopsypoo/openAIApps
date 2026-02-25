using openAIApps.Data;
using openAIApps.Services;
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
        private async Task HandleVideoJobAsync(ResponseVideo jobResponse)
        {
            // Show JSON (with error if any)
            if (jobResponse?.Error != null)
            {
                txtVideoResponse.Text =
                    $"Error code: {jobResponse.Error.Code}\r\nMessage: {jobResponse.Error.Message}\r\n\r\n" +
                    JsonSerializer.Serialize(jobResponse, new JsonSerializerOptions { WriteIndented = true });

                MessageBox.Show(
                    $"Video error: {jobResponse.Error.Code}\n{jobResponse.Error.Message}",
                    "Video Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            txtVideoResponse.Text = JsonSerializer.Serialize(
                jobResponse,
                new JsonSerializerOptions { WriteIndented = true });

            if (jobResponse == null || string.IsNullOrEmpty(jobResponse.Id))
            {
                MessageBox.Show("Video request failed. No video ID returned.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Add to list (or update if already present)
            var existing = _videoHistory.FirstOrDefault(v => v.Id == jobResponse.Id);
            if (existing == null)
            {
                _videoHistory.Add(new VideoClient.VideoListItem
                {
                    Id = jobResponse.Id,
                    Status = jobResponse.Status,
                    Progress = jobResponse.Progress,
                    Model = jobResponse.Model
                });
            }
            else
            {
                existing.Status = jobResponse.Status;
                existing.Progress = jobResponse.Progress;
            }

            // Progress window
            var progressWindow = new ProgressWindow("Processing video...");
            progressWindow.Owner = this;

            var cts = new CancellationTokenSource();
            progressWindow.Canceled += (s, _) => cts.Cancel();

            progressWindow.Show();

            var progress = new Progress<double>(value =>
            {
                progressWindow.UpdateProgress(value);
            });

            await _videoClient.MonitorVideoProgressAsync(jobResponse.Id, progress, cts.Token);

            progressWindow.Close();

            // Final status
            var finalStatus = await _videoClient.GetVideoStatusAsync(jobResponse.Id);
            if (finalStatus != null)
            {
                txtVideoResponse.Text = JsonSerializer.Serialize(
                    finalStatus,
                    new JsonSerializerOptions { WriteIndented = true });

                var item = _videoHistory.FirstOrDefault(v => v.Id == finalStatus.Id);
                if (item != null)
                {
                    item.Status = finalStatus.Status;
                    item.Progress = finalStatus.Progress;
                }

                if (finalStatus.Status == "completed")
                {
                    if (jobResponse.Status == "completed")
                    {
                        // 1. Ensure we have a DB session [cite: 267]
                        int sessionId = await EnsureSessionActiveAsync(EndpointType.Video, txtVideoPrompt.Text);

                        // 2. Log the completion
                        int msgId = await _historyService.AddMessageAsync(sessionId, "assistant", "Video Generated: " + jobResponse.Id);

                        // 3. Link the local file path to this database entry
                        string videosDir = _settings.VideosFolder; // [cite: 185]
                        string localFilePath = Path.Combine(videosDir, jobResponse.Id + ".mp4");
                        await _historyService.LinkMediaAsync(msgId, localFilePath, "video/mp4");
                    }
                    MessageBox.Show(
                        $"Video {finalStatus.Id} created successfully!",
                        "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (finalStatus.Status == "failed")
                {
                    MessageBox.Show(
                        $"Video {finalStatus.Id} failed to generate.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(
                        $"Video {finalStatus.Id} has status: {finalStatus.Status}",
                        "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show(
                    "Could not retrieve final video status.",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private async void btnVideoGenerateClick(object sender, RoutedEventArgs e)
        {
            string prompt = txtVideoPrompt.Text;
            int sessionId = await EnsureSessionActiveAsync(EndpointType.Video, prompt);

            // Save the prompt
            int userMsgId = await _historyService.AddMessageAsync(sessionId, "user", prompt);

            // Call Video API
            var videoResult = await _videoClient.CreateVideoAsync(new VideoClient.RequestVideo
            {
                Prompt = prompt,
                Model = cmbVideoModel.Text,
                Seconds = cmbVideoLength.Text,
                Size = cmbVideoSize.Text
            });
            // Log the outcome regardless of success
            // Check success based on the actual ResponseVideo model [cite: 531]
            bool isSuccess = videoResult != null && videoResult.Error == null && !string.IsNullOrEmpty(videoResult.Id);
            string statusMessage = isSuccess ? "Video generated successfully." : $"Error: {videoResult?.Error?.Message ?? "Unknown Error"}";

            // Log the outcome to history
            int assistantMsgId = await _historyService.AddMessageAsync(
                sessionId,
                "assistant",
                statusMessage,
                // Note: You need a RawJson property or use JsonSerializer if your client doesn't provide it
                JsonSerializer.Serialize(videoResult)
            );

            if (isSuccess)
            {
                // Link the local file path created in your existing video-save logic
                await _historyService.LinkMediaAsync(assistantMsgId, savepath_videos, "completed");
            }
            else
            {
                // Log the failure status for the search utility
                await _historyService.LinkMediaAsync(assistantMsgId, "", videoResult.Status); // e.g., "content_filter"
            }
        }
        private async void btnVideoSendRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResponseVideo? jobResponse;

                if (cbVideoRemix.IsChecked == true)
                {
                    if (lstVideoFiles.SelectedItem is not VideoListItem selected)
                    {
                        MessageBox.Show(
                            "Select a completed video to remix first.",
                            "No Selection",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    var remixPrompt = txtVideoPrompt.Text;
                    jobResponse = await _videoClient.RemixVideoAsync(selected.Id, remixPrompt);
                }
                else
                {
                    var request = new VideoClient.RequestVideo
                    {
                        Prompt = txtVideoPrompt.Text,
                        Model = cmbVideoModel.Text,
                        Seconds = cmbVideoLength.Text,
                        Size = cmbVideoSize.Text
                    };

                    jobResponse = await _videoClient.CreateVideoAsync(request);
                }

                if (jobResponse == null)
                {
                    MessageBox.Show(
                        "Video request failed (no response).",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                await HandleVideoJobAsync(jobResponse);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error: {ex.Message}",
                    "Exception",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

            bool success = await _videoClient.DownloadVideoAsync(videoId, progress);

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
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete video {selectedVideo.Id}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                await _videoClient.DeleteVideoAsync(selectedVideo.Id);

                // Remove it from the UI list
                _videoHistory.Remove(selectedVideo);

                MessageBox.Show("Video deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete video:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void lstVideoFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstVideoFiles.SelectedItem is not VideoListItem selectedVideo)
                return;

            //string videosDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            string videosDir = _settings.VideosFolder;
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
    }
}
