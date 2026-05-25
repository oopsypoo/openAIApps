using openAIApps.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private bool _isRefreshingVideoOptions;
        private bool _videoStateEventsSubscribed;
        private void InitVideoState()
        {
            if (!_videoStateEventsSubscribed)
            {
                VideoState.PropertyChanged += VideoState_PropertyChanged;
                _videoStateEventsSubscribed = true;
            }

            ReplaceCollection(VideoState.AvailableProviders, VideoProviderCatalog.Providers);

            _isRefreshingVideoOptions = true;
            try
            {
                VideoState.PromptText = string.Empty;
                VideoState.ResponseText = string.Empty;
                VideoState.SelectedProvider = VideoProviderType.OpenAI;
                VideoState.SelectedModel = "sora-2";
                VideoState.SelectedLength = "4";
                VideoState.SelectedSize = "720x1280";
                VideoState.SelectedFps = null;
                VideoState.SelectedCameraMotion = string.Empty;
                VideoState.GenerateAudio = true;
                VideoState.IsRemix = false;
                VideoState.SelectedLibraryVideo = null;
                VideoState.SelectedSessionTurn = null;
            }
            finally
            {
                _isRefreshingVideoOptions = false;
            }

            RefreshVideoOptionsForProvider();
        }

        private void VideoState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isRefreshingVideoOptions)
                return;

            switch (e.PropertyName)
            {
                case nameof(VideoPanelState.SelectedProvider):
                    RefreshVideoOptionsForProvider();
                    break;
                case nameof(VideoPanelState.SelectedModel):
                    RefreshVideoOptionsForSelectedModel();
                    break;
            }
        }

        private void cmbVideoProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshingVideoOptions)
                return;

            var selectedProvider = (cmbVideoProvider.SelectedItem as VideoProviderOption)?.ProviderType
                ?? (cmbVideoProvider.SelectedValue is VideoProviderType providerType ? providerType : VideoState.SelectedProvider);

            if (VideoState.SelectedProvider != selectedProvider)
            {
                VideoState.SelectedProvider = selectedProvider;
            }

            RefreshVideoOptionsForProvider();
        }

        private void cmbVideoModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshingVideoOptions)
                return;

            var selectedModelId = (cmbVideoModel.SelectedItem as VideoModelOption)?.Id
                ?? cmbVideoModel.SelectedValue as string
                ?? VideoState.SelectedModel;

            if (!string.Equals(VideoState.SelectedModel, selectedModelId, StringComparison.OrdinalIgnoreCase))
            {
                VideoState.SelectedModel = selectedModelId;
            }

            RefreshVideoOptionsForSelectedModel();
        }

        private void RefreshVideoOptionsForProvider()
        {
            _isRefreshingVideoOptions = true;
            try
            {
                var models = VideoProviderCatalog.GetModels(VideoState.SelectedProvider);
                ReplaceCollection(VideoState.AvailableModels, models);

                if (!models.Any(m => string.Equals(m.Id, VideoState.SelectedModel, StringComparison.OrdinalIgnoreCase)))
                {
                    VideoState.SelectedModel = models.FirstOrDefault()?.Id ?? string.Empty;
                }
            }
            finally
            {
                _isRefreshingVideoOptions = false;
            }

            RefreshVideoOptionsForSelectedModel();
        }

        private void RefreshVideoOptionsForSelectedModel()
        {
            var selectedModel = VideoState.AvailableModels
                .FirstOrDefault(m => string.Equals(m.Id, VideoState.SelectedModel, StringComparison.OrdinalIgnoreCase));

            _isRefreshingVideoOptions = true;
            try
            {
                ReplaceCollection(VideoState.AvailableLengths, selectedModel?.SupportedDurations ?? Array.Empty<string>());
                ReplaceCollection(VideoState.AvailableSizes, selectedModel?.SupportedResolutions ?? Array.Empty<string>());
                ReplaceCollection(VideoState.AvailableFpsValues, selectedModel?.SupportedFpsValues ?? Array.Empty<int>());
                ReplaceCollection(VideoState.AvailableCameraMotions, selectedModel?.SupportedCameraMotions ?? Array.Empty<string>());

                VideoState.SupportsFps = selectedModel?.SupportedFpsValues?.Count > 0;
                VideoState.SupportsCameraMotion = selectedModel?.SupportedCameraMotions?.Count > 0;
                VideoState.SupportsGenerateAudio = selectedModel?.SupportsGenerateAudio ?? false;
                VideoState.SupportsRemix = selectedModel?.SupportsRemix ?? false;
                VideoState.SupportsReferenceImage = selectedModel?.SupportsReferenceImage ?? false;

                if (!VideoState.AvailableLengths.Contains(VideoState.SelectedLength))
                {
                    VideoState.SelectedLength = VideoState.AvailableLengths.FirstOrDefault() ?? string.Empty;
                }

                if (!VideoState.AvailableSizes.Contains(VideoState.SelectedSize))
                {
                    VideoState.SelectedSize = VideoState.AvailableSizes.FirstOrDefault() ?? string.Empty;
                }

                if (!VideoState.SupportsFps)
                {
                    VideoState.SelectedFps = null;
                }
                else if (!VideoState.SelectedFps.HasValue || !VideoState.AvailableFpsValues.Contains(VideoState.SelectedFps.Value))
                {
                    VideoState.SelectedFps = VideoState.AvailableFpsValues.FirstOrDefault();
                }

                if (!VideoState.SupportsCameraMotion)
                {
                    VideoState.SelectedCameraMotion = string.Empty;
                }
                else if (!VideoState.AvailableCameraMotions.Contains(VideoState.SelectedCameraMotion))
                {
                    VideoState.SelectedCameraMotion = VideoState.AvailableCameraMotions.FirstOrDefault() ?? string.Empty;
                }

                if (!VideoState.SupportsGenerateAudio)
                {
                    VideoState.GenerateAudio = false;
                }
                else if (VideoState.SelectedProvider == VideoProviderType.Ltx && !VideoState.GenerateAudio)
                {
                    VideoState.GenerateAudio = true;
                }

                if (!VideoState.SupportsRemix)
                {
                    VideoState.IsRemix = false;
                }
            }
            finally
            {
                _isRefreshingVideoOptions = false;
            }
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (var item in items ?? Enumerable.Empty<T>())
            {
                target.Add(item);
            }
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
                _appStatus.Set($"Video session loaded: {sessionId}");
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

                var provider = ResolveVideoProviderType(lastUser);
                VideoState.SelectedProvider = provider;

                if (!string.IsNullOrWhiteSpace(lastUser.ModelUsed))
                    VideoState.SelectedModel = lastUser.ModelUsed;

                if (!string.IsNullOrWhiteSpace(lastUser.VideoLength))
                    VideoState.SelectedLength = lastUser.VideoLength;

                if (!string.IsNullOrWhiteSpace(lastUser.VideoSize))
                    VideoState.SelectedSize = lastUser.VideoSize;

                if (!string.IsNullOrWhiteSpace(lastUser.VideoFps) && int.TryParse(lastUser.VideoFps, out var fps))
                    VideoState.SelectedFps = fps;
                else if (provider == VideoProviderType.Ltx)
                    VideoState.SelectedFps = VideoState.AvailableFpsValues.FirstOrDefault();
                else
                    VideoState.SelectedFps = null;

                VideoState.SelectedCameraMotion = lastUser.VideoCameraMotion ?? string.Empty;
                VideoState.GenerateAudio = lastUser.VideoGenerateAudio;
                VideoState.IsRemix = lastUser.IsRemix;
            }

            var lastAssistant = history.LastOrDefault(m =>
                string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));

            VideoState.ResponseText = lastAssistant != null
                ? GetVideoAssistantDisplayText(lastAssistant)
                : string.Empty;
        }
        private static VideoProviderType ResolveVideoProviderType(ChatMessage? message)
        {
            if (message != null && !string.IsNullOrWhiteSpace(message.VideoProvider) &&
                Enum.TryParse<VideoProviderType>(message.VideoProvider, true, out var providerFromMetadata))
            {
                return providerFromMetadata;
            }

            if (!string.IsNullOrWhiteSpace(message?.ModelUsed) &&
                message.ModelUsed.StartsWith("ltx-", StringComparison.OrdinalIgnoreCase))
            {
                return VideoProviderType.Ltx;
            }

            return VideoProviderType.OpenAI;
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
            if (itemToSelect == null)
            {
                itemToSelect = new VideoClient.VideoListItem
                {
                    Id = assistantWithRemoteId.RemoteId,
                    Model = assistantWithRemoteId.ModelUsed,
                    Status = InferLibraryItemStatus(assistantWithRemoteId),
                    IsDownloaded = IsHistoryVideoDownloaded(assistantWithRemoteId),
                    HasError = string.Equals(InferLibraryItemStatus(assistantWithRemoteId), "failed", StringComparison.OrdinalIgnoreCase)
                };
                _videoHistory.Add(itemToSelect);
            }

            VideoState.SelectedLibraryVideo = itemToSelect;

            if (itemToSelect != null)
                lstVideoFiles.ScrollIntoView(itemToSelect);
        }
        private void ShowVideoPreviewForLibraryItem(VideoListItem selectedVideo)
        {
            if (selectedVideo == null)
            {
                imgVideo.Source = new BitmapImage(new Uri("/no_pic.png", UriKind.Relative));
                return;
            }

            string videosDir = _settings.VideosFolder;
            string localFilePath = Path.Combine(videosDir, selectedVideo.Id + ".mp4");

            if (!File.Exists(localFilePath))
            {
                imgVideo.Source = new BitmapImage(new Uri("/no_pic.png", UriKind.Relative));
                return;
            }

            try
            {
                var bitmap = GetFirstFrameAsBitmap(localFilePath);
                imgVideo.Source = bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Preview failed: {ex.Message}");
                imgVideo.Source = new BitmapImage(new Uri("/no_pic.png", UriKind.Relative));
            }
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
                //await _historyService.LinkMediaAsync(assistantMsgId, localFilePath, "video/mp4");
                _appStatus.Set("Video Ready.");
            }
            else if (finalStatus?.Error != null)
            {
                // THIS handles if it fails mid-way through processing
                string errorDetail = $"Code: {finalStatus.Error.Code}\nMessage: {finalStatus.Error.Message}";
                await _historyService.AddMessageAsync(
                    sessionId,
                    "assistant",
                    $"Processing Failed: {finalStatus.Error.Message}",
                    model: VideoState.SelectedModel,
                    videoLength: VideoState.SelectedLength,
                    videoSize: VideoState.SelectedSize,
                    remoteId: jobResponse.Id,
                    videoProvider: VideoProviderType.OpenAI.ToString(),
                    videoOperation: VideoState.IsRemix ? "remix" : "text-to-video");
                //MessageBox.Show(errorDetail, "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _appStatus.Set($"Processing Error: {errorDetail}");
            }
        }

        private async void btnVideoGenerateClick(object sender, RoutedEventArgs e)
        {
            string prompt = txtVideoPrompt.Text ?? VideoState.PromptText ?? string.Empty;

            var selectedProvider = (cmbVideoProvider.SelectedItem as VideoProviderOption)?.ProviderType
                ?? (cmbVideoProvider.SelectedValue is VideoProviderType providerType ? providerType : VideoState.SelectedProvider);

            var selectedModel = (cmbVideoModel.SelectedItem as VideoModelOption)?.Id
                ?? cmbVideoModel.SelectedValue as string
                ?? VideoState.SelectedModel;

            string? selectedLength = cmbVideoLength.SelectedItem as string ?? VideoState.SelectedLength;
            string? selectedSize = cmbVideoSize.SelectedItem as string ?? VideoState.SelectedSize;

            int? selectedFps = cmbVideoFps.SelectedItem switch
            {
                int fps => fps,
                string fpsText when int.TryParse(fpsText, out var parsedFps) => parsedFps,
                _ => VideoState.SelectedFps
            };

            string? selectedCameraMotion = cmbVideoCameraMotion.SelectedItem as string ?? VideoState.SelectedCameraMotion;
            bool generateAudio = cbVideoGenerateAudio.IsChecked ?? VideoState.GenerateAudio;

            VideoState.PromptText = prompt;
            VideoState.SelectedProvider = selectedProvider;
            VideoState.SelectedModel = selectedModel;
            VideoState.SelectedLength = selectedLength;
            VideoState.SelectedSize = selectedSize;
            VideoState.SelectedFps = selectedFps;
            VideoState.SelectedCameraMotion = selectedCameraMotion;
            VideoState.GenerateAudio = generateAudio;

            bool isRemix = VideoState.IsRemix && VideoState.SupportsRemix;

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

            string videoProvider = selectedProvider.ToString();
            string videoOperation = isRemix ? "remix" : "text-to-video";
            string? videoFps = VideoState.SelectedFps?.ToString();
            string? videoCameraMotion = string.IsNullOrWhiteSpace(VideoState.SelectedCameraMotion)
                ? null
                : VideoState.SelectedCameraMotion;
            bool videoGenerateAudio = selectedProvider == VideoProviderType.Ltx && VideoState.GenerateAudio;

            await _historyService.AddMessageAsync(
                sessionId,
                "user",
                prompt,
                model: VideoState.SelectedModel,
                videoLength: VideoState.SelectedLength,
                videoSize: VideoState.SelectedSize,
                isRemix: isRemix,
                sourceRemoteId: sourceVideoId,
                videoProvider: videoProvider,
                videoOperation: videoOperation,
                videoFps: videoFps,
                videoCameraMotion: videoCameraMotion,
                videoGenerateAudio: videoGenerateAudio
            );

            if (selectedProvider == VideoProviderType.Ltx)
            {
                await HandleLtxVideoGenerationAsync(sessionId, prompt, sourceVideoId);
                await LoadVideoSessionAsync(sessionId);
                InitVideoList();
                RefreshLogsTab();
                return;
            }

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
                _appStatus.Set($"Failed: {videoResult?.Error?.Code ?? "400"}");

                await _historyService.AddMessageAsync(
                    sessionId,
                    "assistant",
                    $"Error: {errorMsg}",
                    rawJson: rawJson,
                    model: VideoState.SelectedModel,
                    videoLength: VideoState.SelectedLength,
                    videoSize: VideoState.SelectedSize,
                    remoteId: videoResult?.Id,
                    sourceRemoteId: sourceVideoId,
                    videoProvider: videoProvider,
                    videoOperation: videoOperation,
                    videoFps: videoFps,
                    videoCameraMotion: videoCameraMotion,
                    videoGenerateAudio: videoGenerateAudio
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
                videoLength: VideoState.SelectedLength,
                videoSize: VideoState.SelectedSize,
                remoteId: videoResult?.Id,
                sourceRemoteId: sourceVideoId,
                videoProvider: videoProvider,
                videoOperation: videoOperation,
                videoFps: videoFps,
                videoCameraMotion: videoCameraMotion,
                videoGenerateAudio: videoGenerateAudio
            );

            if (videoResult != null && !string.IsNullOrEmpty(videoResult.Id))
            {
                await HandleVideoJobAsync(videoResult, sessionId, assistantMsgId);
            }
            else
            {
                string errorMsg = videoResult?.Error?.Message ?? "The API returned a success code but no Video ID.";
                _appStatus.Set($"Failed: {errorMsg}");
                MessageBox.Show(errorMsg, "API Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await LoadVideoSessionAsync(sessionId);
            InitVideoList();
            RefreshLogsTab();
        }

        private async Task HandleLtxVideoGenerationAsync(int sessionId, string prompt, string? sourceVideoId)
        {
            if (_ltxVideoProvider == null)
            {
                const string missingKeyMessage = "LTX_API_KEY is not configured.";
                VideoState.ResponseText = missingKeyMessage;
                _appStatus.Set(missingKeyMessage);
                MessageBox.Show(missingKeyMessage, "Missing LTX API key", MessageBoxButton.OK, MessageBoxImage.Warning);
                await _historyService.AddMessageAsync(
                    sessionId,
                    "assistant",
                    $"Error: {missingKeyMessage}",
                    rawJson: missingKeyMessage,
                    model: VideoState.SelectedModel,
                    videoLength: VideoState.SelectedLength,
                    videoSize: VideoState.SelectedSize,
                    sourceRemoteId: sourceVideoId,
                    videoProvider: VideoProviderType.Ltx.ToString(),
                    videoOperation: "text-to-video",
                    videoFps: VideoState.SelectedFps?.ToString(),
                    videoCameraMotion: string.IsNullOrWhiteSpace(VideoState.SelectedCameraMotion) ? null : VideoState.SelectedCameraMotion,
                    videoGenerateAudio: VideoState.GenerateAudio);
                return;
            }

            try
            {
                var request = new VideoGenerationRequest
                {
                    ProviderType = VideoProviderType.Ltx,
                    Prompt = prompt,
                    Model = VideoState.SelectedModel,
                    Duration = VideoState.SelectedLength,
                    Resolution = VideoState.SelectedSize,
                    Fps = VideoState.SelectedFps,
                    CameraMotion = string.IsNullOrWhiteSpace(VideoState.SelectedCameraMotion) ? null : VideoState.SelectedCameraMotion,
                    GenerateAudio = VideoState.GenerateAudio,
                    IsRemix = false,
                    SourceVideoId = sourceVideoId,
                    ReferenceImagePath = _videoReferencePath
                };

                var submitResult = await _ltxVideoProvider.SubmitTextToVideoAsync(request, CancellationToken.None);
                VideoState.ResponseText = submitResult.RawJson;

                int assistantMsgId = await _historyService.AddMessageAsync(
                    sessionId,
                    "assistant",
                    "LTX video task initiated...",
                    rawJson: submitResult.RawJson,
                    model: VideoState.SelectedModel,
                    videoLength: VideoState.SelectedLength,
                    videoSize: VideoState.SelectedSize,
                    remoteId: submitResult.JobId,
                    sourceRemoteId: sourceVideoId,
                    videoProvider: VideoProviderType.Ltx.ToString(),
                    videoOperation: submitResult.Operation,
                    videoFps: VideoState.SelectedFps?.ToString(),
                    videoCameraMotion: string.IsNullOrWhiteSpace(VideoState.SelectedCameraMotion) ? null : VideoState.SelectedCameraMotion,
                    videoGenerateAudio: VideoState.GenerateAudio);

                if (string.IsNullOrWhiteSpace(submitResult.JobId))
                {
                    _appStatus.Set("LTX submit did not return a job id.");
                    return;
                }

                EnsureVideoHistoryItem(submitResult.JobId, VideoState.SelectedModel, submitResult.InitialStatus, false, false);
                await MonitorLtxVideoJobAsync(submitResult.JobId, assistantMsgId);
            }
            catch (Exception ex)
            {
                VideoState.ResponseText = ex.Message;
                _appStatus.Set($"LTX error: {ex.Message}");
                await _historyService.AddMessageAsync(
                    sessionId,
                    "assistant",
                    $"Error: {ex.Message}",
                    rawJson: ex.ToString(),
                    model: VideoState.SelectedModel,
                    videoLength: VideoState.SelectedLength,
                    videoSize: VideoState.SelectedSize,
                    sourceRemoteId: sourceVideoId,
                    videoProvider: VideoProviderType.Ltx.ToString(),
                    videoOperation: "text-to-video",
                    videoFps: VideoState.SelectedFps?.ToString(),
                    videoCameraMotion: string.IsNullOrWhiteSpace(VideoState.SelectedCameraMotion) ? null : VideoState.SelectedCameraMotion,
                    videoGenerateAudio: VideoState.GenerateAudio);
                MessageBox.Show(ex.Message, "LTX generation failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task MonitorLtxVideoJobAsync(string jobId, int assistantMsgId)
        {
            var progressWindow = new ProgressWindow("Processing video...")
            {
                Owner = this
            };
            var cts = new CancellationTokenSource();
            progressWindow.Canceled += (s, _) => cts.Cancel();
            progressWindow.Show();

            VideoPollResult? finalStatus = null;

            try
            {
                while (true)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    finalStatus = await _ltxVideoProvider!.GetJobStatusAsync(jobId, cts.Token);
                    progressWindow.UpdateProgress(GetProviderProgress(finalStatus));

                    if (IsTerminalProviderStatus(finalStatus.Status))
                        break;

                    await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                await _historyService.UpdateMessageContentAndRawJsonAsync(
                    assistantMsgId,
                    "LTX video polling canceled.",
                    finalStatus?.RawJson ?? string.Empty);
                _appStatus.Set("LTX video polling canceled.");
                return;
            }
            catch (Exception ex)
            {
                await _historyService.UpdateMessageContentAndRawJsonAsync(
                    assistantMsgId,
                    $"LTX status check failed: {ex.Message}",
                    ex.ToString());
                EnsureVideoHistoryItem(jobId, VideoState.SelectedModel, "failed", false, true);
                _appStatus.Set($"LTX status check failed: {ex.Message}");
                return;
            }
            finally
            {
                progressWindow.Close();
            }

            if (finalStatus == null)
                return;

            VideoState.ResponseText = finalStatus.RawJson;

            if (string.Equals(finalStatus.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                var downloadWindow = new ProgressWindow("Downloading video...")
                {
                    Owner = this
                };
                downloadWindow.Show();

                string? localFilePath = null;
                try
                {
                    var progress = new Progress<double>(value => downloadWindow.UpdateProgress(value));
                    localFilePath = await _ltxVideoProvider!.DownloadResultAsync(_settings.VideosFolder, jobId, finalStatus, progress, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    await _historyService.UpdateMessageContentAndRawJsonAsync(
                        assistantMsgId,
                        $"LTX download failed: {ex.Message}",
                        ex.ToString());
                    EnsureVideoHistoryItem(jobId, VideoState.SelectedModel, "failed", false, true);
                    _appStatus.Set($"LTX download failed: {ex.Message}");
                    return;
                }
                finally
                {
                    downloadWindow.Close();
                }

                string completionText = string.IsNullOrWhiteSpace(localFilePath)
                    ? "LTX video completed."
                    : $"LTX video completed. Saved to {Path.GetFileName(localFilePath)}.";

                await _historyService.UpdateMessageContentAndRawJsonAsync(assistantMsgId, completionText, finalStatus.RawJson);

                if (!string.IsNullOrWhiteSpace(localFilePath))
                {
                    await _historyService.LinkMediaAsync(assistantMsgId, localFilePath, "video/mp4");
                }

                EnsureVideoHistoryItem(jobId, VideoState.SelectedModel, finalStatus.Status, !string.IsNullOrWhiteSpace(localFilePath), false);
                _appStatus.Set("Video Ready.");
                return;
            }

            string failureText = string.IsNullOrWhiteSpace(finalStatus.ErrorMessage)
                ? $"LTX video failed with status: {finalStatus.Status}."
                : $"LTX video failed: {finalStatus.ErrorMessage}";

            await _historyService.UpdateMessageContentAndRawJsonAsync(assistantMsgId, failureText, finalStatus.RawJson);
            EnsureVideoHistoryItem(jobId, VideoState.SelectedModel, finalStatus.Status, false, true);
            _appStatus.Set(failureText);
        }

        private static bool IsTerminalProviderStatus(string status)
        {
            return string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, "not_found", StringComparison.OrdinalIgnoreCase);
        }

        private static double GetProviderProgress(VideoPollResult status)
        {
            if (status.ProgressPercent.HasValue)
                return Math.Clamp(status.ProgressPercent.Value, 0, 100);

            if (string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase))
                return 100;
            if (string.Equals(status.Status, "processing", StringComparison.OrdinalIgnoreCase))
                return 65;
            if (string.Equals(status.Status, "pending", StringComparison.OrdinalIgnoreCase))
                return 15;
            if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
                return 100;
            return 5;
        }

        private void EnsureVideoHistoryItem(string videoId, string model, string status, bool isDownloaded, bool hasError)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                return;

            var existing = _videoHistory.FirstOrDefault(v => string.Equals(v.Id, videoId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new VideoClient.VideoListItem
                {
                    Id = videoId,
                    Model = model,
                    Status = status
                };
                _videoHistory.Add(existing);
            }

            existing.Model = model;
            existing.Status = status;
            existing.IsDownloaded = isDownloaded;
            existing.HasError = hasError;
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

        private static string InferLibraryItemStatus(ChatMessage message)
        {
            if (message == null)
                return string.Empty;

            if (message.MediaFiles != null && message.MediaFiles.Any(m => !string.IsNullOrWhiteSpace(m.LocalPath) && File.Exists(m.LocalPath)))
                return "completed";

            if (!string.IsNullOrWhiteSpace(message.Content) &&
                (message.Content.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                 message.Content.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0))
                return "failed";

            return "stored";
        }

        private bool IsHistoryVideoDownloaded(ChatMessage message)
        {
            if (message?.MediaFiles != null && message.MediaFiles.Any(m => !string.IsNullOrWhiteSpace(m.LocalPath) && File.Exists(m.LocalPath)))
                return true;

            return message != null && IsVideoDownloaded(message.RemoteId);
        }

        private async void InitVideoList()
        {
            _videoHistory.Clear();

            try
            {
                var listResponse = await _videoClient.GetAllVideosAsync();
                if (listResponse?.Data != null)
                {
                    foreach (var item in listResponse.Data)
                    {
                        item.IsDownloaded = IsVideoDownloaded(item.Id);
                        item.HasError = string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase);
                        _videoHistory.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Video list remote load failed: {ex.Message}");
            }

            try
            {
                var historyMessages = await _historyService.GetVideoLibraryMessagesAsync();
                foreach (var message in historyMessages)
                {
                    if (string.IsNullOrWhiteSpace(message.RemoteId))
                        continue;

                    if (_videoHistory.Any(v => string.Equals(v.Id, message.RemoteId, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _videoHistory.Add(new VideoClient.VideoListItem
                    {
                        Id = message.RemoteId,
                        Model = message.ModelUsed,
                        Status = InferLibraryItemStatus(message),
                        IsDownloaded = IsHistoryVideoDownloaded(message),
                        HasError = string.Equals(InferLibraryItemStatus(message), "failed", StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Video history merge failed: {ex.Message}");
            }
        }

        private async void btnDownloadVideo_Click(object sender, RoutedEventArgs e)
        {
            if (VideoState.SelectedLibraryVideo is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video to download.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string videoId = selectedVideo.Id;
            var message = await _historyService.GetMessageByRemoteVideoIdAsync(selectedVideo.Id);
            var provider = ResolveVideoProviderType(message);

            if (provider == VideoProviderType.Ltx)
            {
                if (IsVideoDownloaded(videoId))
                {
                    MessageBox.Show($"Video {videoId} is already available locally.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("LTX videos are downloaded automatically when the job completes. This video is not currently available locally.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

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
                if (message != null)
                {
                    string localFilePath = Path.Combine(_settings.VideosFolder, $"{selectedVideo.Id}.mp4");
                    await _historyService.LinkMediaAsync(message.Id, localFilePath, "video/mp4");
                }
            }
            else
            {
                selectedVideo.HasError = true;
                MessageBox.Show($"Failed to download video {videoId}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private async void btnDeleteVideo_Click(object sender, RoutedEventArgs e)
        {
            if (VideoState.SelectedLibraryVideo is not VideoClient.VideoListItem selectedVideo) return;

            var message = await _historyService.GetMessageByRemoteVideoIdAsync(selectedVideo.Id);
            var provider = ResolveVideoProviderType(message);

            string confirmText = provider == VideoProviderType.Ltx
                ? $"Permanently delete local video {selectedVideo.Id} and its logs?"
                : $"Permanently delete video {selectedVideo.Id} from OpenAI and local storage?";

            var confirm = MessageBox.Show(
                confirmText,
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {

                if (provider == VideoProviderType.Ltx)
                {
                    string localFilePath = GetLocalVideoPath(selectedVideo.Id);
                    if (File.Exists(localFilePath))
                    {
                        File.Delete(localFilePath);
                    }

                    string thumbPath = Path.ChangeExtension(localFilePath, ".thumb.png");
                    if (File.Exists(thumbPath))
                    {
                        File.Delete(thumbPath);
                    }
                }
                else
                {
                    await _videoClient.DeleteVideoAsync(selectedVideo.Id, _settings.VideosFolder);
                }

                if (message != null)
                {
                    await _historyService.DeleteSessionAsync(message.ChatSessionId);
                    if (_activeVideoSessionId == message.ChatSessionId) ResetVideoUI();
                }

                _videoHistory.Remove(selectedVideo);
                RefreshLogsTab();

                _appStatus.Set("Video and logs deleted.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnGetStatus_Click(object sender, RoutedEventArgs e)
        {
            if (VideoState.SelectedLibraryVideo is not VideoListItem selectedVideo)
            {
                MessageBox.Show("Please select a video first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var message = await _historyService.GetMessageByRemoteVideoIdAsync(selectedVideo.Id);
                var provider = ResolveVideoProviderType(message);

                if (provider == VideoProviderType.Ltx)
                {
                    if (_ltxVideoProvider == null)
                    {
                        VideoState.ResponseText = "LTX_API_KEY is not configured.";
                        return;
                    }

                    var status = await _ltxVideoProvider.GetJobStatusAsync(selectedVideo.Id, CancellationToken.None);
                    VideoState.ResponseText = status.RawJson;
                    selectedVideo.Status = status.Status;
                    selectedVideo.HasError = string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase);
                    selectedVideo.IsDownloaded = IsVideoDownloaded(selectedVideo.Id);
                    return;
                }

                var openAiStatus = await _videoClient.GetVideoStatusAsync(selectedVideo.Id);

                // Show JSON string in your response TextBox (or format nicely)
                VideoState.ResponseText = JsonSerializer.Serialize(openAiStatus, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Optional: update your local _videoHistory so progress/status is current
                selectedVideo.Status = openAiStatus.Status;
                selectedVideo.Progress = openAiStatus.Progress;

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
            if (VideoState.SelectedLibraryVideo is not VideoListItem selectedVideo)
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
            if (VideoState.SelectedLibraryVideo is not VideoListItem selectedVideo)
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
                _appStatus.Set($"Video selected: {selectedVideo.Id}");
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
                VideoState.SelectedProvider = VideoProviderType.OpenAI;
                VideoState.SelectedModel = "sora-2";
                VideoState.SelectedLength = "4";
                VideoState.SelectedSize = "720x1280";
                VideoState.SelectedFps = null;
                VideoState.SelectedCameraMotion = string.Empty;
                VideoState.GenerateAudio = true;
            }

            imgVideo.Source = new BitmapImage(new Uri("/no_pic.png", UriKind.Relative));

            _appStatus.Set("Ready for a new video job.");
        }
        private void txtVideoPrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingVideoSession)
                return;

            if (_activeVideoSessionId != null && VideoState.IsRemix == false)
            {
                _activeVideoSessionId = null;
                _appStatus.Set("New prompt detected: A new session will be created.");
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