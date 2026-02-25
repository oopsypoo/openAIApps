#pragma warning disable CS8632 // annotation for nullable ref types should only be used in code within a '#nullable' annotations context
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace openAIApps
{
    internal class VideoClient : IDisposable
    {
        private const string VideoEndpoint = "https://api.openai.com/v1/videos";
        private readonly System.Net.Http.HttpClient _httpClient;
        /// <summary>
        /// If user chooses to use a reference video for variation or editing, ref: RequestVideo.InputReference. THis is local path to that file.
        /// Data in RequestVideo.InputReference must be base64-encoded file data/multipart form data. See CreateVideoAsync method.
        /// </summary>
        public string ReferenceFilePath = "";

        // --- Common fields between request and response ---
        public abstract class VideoBase
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("size")]
            public string Size { get; set; }

            [JsonPropertyName("seconds")]
            public string Seconds { get; set; }
        }

        // --- Request model ---
        public class RequestVideo : VideoBase
        {
            [JsonPropertyName("prompt")]
            public string Prompt { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            [JsonPropertyName("input_reference")]
            public string? InputReference { get; set; }
        }

        // --- Response model ---
        public class ResponseVideo : VideoBase
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("object")]
            public string ObjectType { get; set; }

            [JsonPropertyName("status")]
            public string Status { get; set; }

            [JsonPropertyName("progress")]
            public int Progress { get; set; }

            [JsonPropertyName("created_at")]
            public long CreatedAt { get; set; }

            [JsonPropertyName("quality")]
            public string Quality { get; set; }
            [JsonPropertyName("error")]
            public VideoError Error { get; set; }
        }
        public class VideoError
        {
            [JsonPropertyName("code")]
            public string Code { get; set; }

            [JsonPropertyName("message")]
            public string Message { get; set; }
        }
        public class VideoListResponse
        {
            [JsonPropertyName("object")]
            public string Object { get; set; }

            [JsonPropertyName("data")]
            public List<VideoListItem> Data { get; set; }
        }

        public class VideoListItem
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("object")]
            public string Object { get; set; }

            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("status")]
            public string Status { get; set; }
            [JsonPropertyName("progress")]
            public int Progress { get; set; } = 0;
            // New properties-local properties for UI state tracking
            public bool IsDownloaded { get; set; }
            public bool HasError { get; set; }
        }
        public async Task<VideoListResponse?> GetAllVideosAsync()
        {
            // The documentation says POST, so we'll use that.
            // (If it turns out GET is correct, just change it to GetAsync)
            var response = await _httpClient.GetAsync(VideoEndpoint);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<VideoListResponse>(responseJson);
        }
        public async Task<ResponseVideo?> GetVideoDetailsAsync(string videoId)
        {
            var url = $"{VideoEndpoint}/{videoId}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ResponseVideo>(responseJson);
        }
        public async Task<bool> DownloadVideoAsync(string videoId, IProgress<double>? progress = null)
        {
            var requestUri = $"{VideoEndpoint}/{videoId}/content";

            using var response = await _httpClient.GetAsync(requestUri, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
                return false;

            var videosDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            Directory.CreateDirectory(videosDir);

            var filePath = Path.Combine(videosDir, $"{videoId}.mp4");

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalRead = 0L;
            var buffer = new byte[8192];
            int read;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fs.WriteAsync(buffer, 0, read);
                totalRead += read;

                if (totalBytes > 0)
                {
                    double progressValue = (double)totalRead / totalBytes * 100.0;
                    progress?.Report(progressValue);
                }
            }

            return true;
        }

        public async Task<bool> DeleteVideoAsync(string videoId)
        {
            var url = $"{VideoEndpoint}/{videoId}";
            using var response = await _httpClient.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                // Optional: read response content for error details
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to delete video: {errorContent}");
            }
        }
        public async Task<ResponseVideo> CreateVideoAsync(RequestVideo request)
        {
            System.Net.Http.HttpResponseMessage response;

            // Check if a reference image was selected
            if (!string.IsNullOrEmpty(this.ReferenceFilePath) && File.Exists(this.ReferenceFilePath))
            {
                // --- Multipart/form-data request ---
                using var form = new MultipartFormDataContent
                {
                    { new StringContent(request.Model), "model" },
                    { new StringContent(request.Prompt), "prompt" },
                    { new StringContent(request.Size), "size" },
                    { new StringContent(request.Seconds), "seconds" } // already string
                };

                // Add the reference image
                var fileBytes = await File.ReadAllBytesAsync(this.ReferenceFilePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png"); // or detect from extension
                form.Add(fileContent, "input_reference", Path.GetFileName(this.ReferenceFilePath));

                response = await _httpClient.PostAsync(VideoEndpoint, form);
            }
            else
            {
                // --- Standard JSON request ---
                // Remove InputReference if null
                var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
                var json = JsonSerializer.Serialize(request, jsonOptions);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(VideoEndpoint, content);
            }

            // Try to read the response even if error
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Instead of throwing, return the response so you can show in UI
                return new ResponseVideo
                {
                    Status = $"Error {response.StatusCode}: {responseJson}"
                };
            }

            return JsonSerializer.Deserialize<ResponseVideo>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        public async Task<ResponseVideo?> RemixVideoAsync(string videoId, string prompt)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                throw new ArgumentException("Video ID cannot be null or empty.", nameof(videoId));
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var url = $"{VideoEndpoint}/{videoId}/remix";

            var body = new
            {
                prompt = prompt
                // add other supported fields here if the API allows (e.g. model, seconds, size)
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Let your UI inspect the error inside ResponseVideo.Error
                return JsonSerializer.Deserialize<ResponseVideo>(
                    responseJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return JsonSerializer.Deserialize<ResponseVideo>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task MonitorVideoProgressAsync(string videoId, IProgress<double> progress, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{VideoEndpoint}/{videoId}", cancellationToken);
                    var json = await response.Content.ReadAsStringAsync();
                    var videoStatus = JsonSerializer.Deserialize<ResponseVideo>(json);

                    if (videoStatus == null)
                        break;

                    // Update progress (0–100)
                    progress.Report(videoStatus.Progress);

                    // Stop polling if completed or failed
                    if (videoStatus.Status is "completed" or "failed")
                        break;
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while monitoring video: {ex.Message}");
                    break;
                }

                await Task.Delay(2000, cancellationToken); // Poll every 2 seconds
            }
        }

        public async Task<ResponseVideo> GetVideoStatusAsync(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                throw new ArgumentException("Video ID cannot be null or empty.", nameof(videoId));

            try
            {
                var response = await _httpClient.GetAsync($"{VideoEndpoint}/{videoId}");
                var json = await response.Content.ReadAsStringAsync();

                // Optional: you can log or inspect the JSON here if needed
                return JsonSerializer.Deserialize<ResponseVideo>(json);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP error getting video status: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error getting video status: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }


        // Constructor to initialize _httpClient
        public VideoClient(string apiKey)
        {
            _httpClient = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri(VideoEndpoint)
            };
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }
}
