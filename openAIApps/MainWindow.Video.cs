using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
        private async void btnVideoSendRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Build request from your UI controls
                var request = new VideoClient.RequestVideo
                {
                    Prompt = txtVideoPrompt.Text,
                    Model = cmbVideoModel.Text,
                    Seconds = cmbVideoLength.Text,
                    Size = cmbVideoSize.Text
                };

                // Create the video job
                var response = await _videoClient.CreateVideoAsync(request);

                // Show raw JSON in your response text box
                txtVideoResponse.Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });

                // --- Handle invalid response ---
                if (response == null || string.IsNullOrEmpty(response.Id))
                {
                    MessageBox.Show("Video creation request failed. No video ID returned.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // --- Add to your listbox immediately ---
                _videoHistory.Add(new VideoClient.VideoListItem
                {
                    Id = response.Id,
                    Status = response.Status,
                    Progress = response.Progress,
                    Model = response.Model
                });

                // --- Create and show progress window ---
                var progressWindow = new ProgressWindow("Creating video...");
                progressWindow.Owner = this;

                var cts = new CancellationTokenSource();
                progressWindow.Canceled += (s, _) => cts.Cancel();

                progressWindow.Show();

                // --- Track progress using the client helper ---
                var progress = new Progress<double>(value =>
                {
                    progressWindow.UpdateProgress(value);
                });

                await _videoClient.MonitorVideoProgressAsync(response.Id, progress, cts.Token);

                progressWindow.Close();

                // --- Check final status ---
                var finalStatus = await _videoClient.GetVideoStatusAsync(response.Id);

                if (finalStatus != null)
                {
                    txtVideoResponse.Text = JsonSerializer.Serialize(finalStatus, new JsonSerializerOptions { WriteIndented = true });

                    // Update listbox item if found
                    var existing = lstVideoFiles.Items.OfType<VideoClient.VideoListItem>().FirstOrDefault(v => v.Id == finalStatus.Id);
                    if (existing != null)
                    {
                        existing.Status = finalStatus.Status;
                        existing.Progress = finalStatus.Progress;
                        lstVideoFiles.Items.Refresh();
                    }

                    if (finalStatus.Status == "completed")
                    {
                        MessageBox.Show($"✅ Video {finalStatus.Id} created successfully!", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (finalStatus.Status == "failed")
                    {
                        MessageBox.Show($"❌ Video {finalStatus.Id} failed to generate.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        MessageBox.Show($"⚠️ Video {finalStatus.Id} has status: {finalStatus.Status}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Could not retrieve final video status.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}", "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private static string GetLocalVideoPath(string videoId)
        {
            string videosDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            return Path.Combine(videosDir, videoId + ".mp4");
        }

        private static bool IsVideoDownloaded(string videoId)
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

            string videosDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
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
    }
}
