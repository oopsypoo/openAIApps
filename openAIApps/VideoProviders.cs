using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace openAIApps
{
    public enum VideoProviderType
    {
        OpenAI,
        Ltx
    }

    public sealed class VideoProviderOption
    {
        public VideoProviderType ProviderType { get; init; }
        public string DisplayName { get; init; } = string.Empty;

        public override string ToString() => DisplayName;
    }

    public sealed class VideoModelOption
    {
        public VideoProviderType ProviderType { get; init; }
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public IReadOnlyList<string> SupportedDurations { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> SupportedResolutions { get; init; } = Array.Empty<string>();
        public IReadOnlyList<int> SupportedFpsValues { get; init; } = Array.Empty<int>();
        public IReadOnlyList<string> SupportedCameraMotions { get; init; } = Array.Empty<string>();
        public bool SupportsGenerateAudio { get; init; }
        public bool SupportsRemix { get; init; }
        public bool SupportsReferenceImage { get; init; }

        public override string ToString() => DisplayName;
    }

    public sealed class VideoGenerationRequest
    {
        public VideoProviderType ProviderType { get; init; }
        public string Prompt { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public string Duration { get; init; } = string.Empty;
        public string Resolution { get; init; } = string.Empty;
        public int? Fps { get; init; }
        public string? CameraMotion { get; init; }
        public bool GenerateAudio { get; init; } = true;
        public bool IsRemix { get; init; }
        public string? SourceVideoId { get; init; }
        public string? SourceVideoPath { get; init; }
        public string? ReferenceImagePath { get; init; }
    }

    public sealed class VideoSubmitResult
    {
        public string JobId { get; init; } = string.Empty;
        public string InitialStatus { get; init; } = string.Empty;
        public DateTimeOffset? CreatedAt { get; init; }
        public string RawJson { get; init; } = string.Empty;
        public string Operation { get; init; } = "text-to-video";
    }

    public sealed class VideoPollResult
    {
        public string JobId { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string RawJson { get; init; } = string.Empty;
        public string? ErrorCode { get; init; }
        public string? ErrorType => ErrorCode;
        public string? ErrorMessage { get; init; }
        public string? VideoUrl { get; init; }
        public int? ProgressPercent { get; init; }
        public DateTimeOffset? CreatedAt { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
    }

    public interface IVideoGenerationProvider
    {
        VideoProviderType ProviderType { get; }
        Task<VideoSubmitResult> SubmitTextToVideoAsync(VideoGenerationRequest request, CancellationToken cancellationToken);
        Task<VideoPollResult> GetJobStatusAsync(string jobId, CancellationToken cancellationToken);
        Task<string?> DownloadResultAsync(string videosFolder, string jobId, VideoPollResult finalStatus, IProgress<double>? progress, CancellationToken cancellationToken);
        IReadOnlyList<VideoModelOption> GetSupportedModels();
    }

    public static class VideoProviderCatalog
    {
        public static readonly IReadOnlyList<VideoProviderOption> Providers =
            new List<VideoProviderOption>
            {
                new() { ProviderType = VideoProviderType.OpenAI, DisplayName = "OpenAI" },
                new() { ProviderType = VideoProviderType.Ltx, DisplayName = "LTX" }
            };

        public static readonly IReadOnlyList<string> LtxCameraMotions =
            new[]
            {
                "dolly_in",
                "dolly_out",
                "dolly_left",
                "dolly_right",
                "jib_up",
                "jib_down",
                "static",
                "focus_shift"
            };

        public static readonly IReadOnlyList<VideoModelOption> OpenAiModels =
            new List<VideoModelOption>
            {
                new()
                {
                    ProviderType = VideoProviderType.OpenAI,
                    Id = "sora-2",
                    DisplayName = "sora-2",
                    SupportedDurations = new[] { "4", "8", "12" },
                    SupportedResolutions = new[] { "720x1280", "1280x720", "1024x1792", "1792x1024" },
                    SupportsGenerateAudio = false,
                    SupportsRemix = true,
                    SupportsReferenceImage = true
                },
                new()
                {
                    ProviderType = VideoProviderType.OpenAI,
                    Id = "sora-2-pro",
                    DisplayName = "sora-2-pro",
                    SupportedDurations = new[] { "4", "8", "12" },
                    SupportedResolutions = new[] { "720x1280", "1280x720", "1024x1792", "1792x1024" },
                    SupportsGenerateAudio = false,
                    SupportsRemix = true,
                    SupportsReferenceImage = true
                }
            };

        public static readonly IReadOnlyList<VideoModelOption> LtxModels =
            new List<VideoModelOption>
            {
                new()
                {
                    ProviderType = VideoProviderType.Ltx,
                    Id = "ltx-2-fast",
                    DisplayName = "ltx-2-fast",
                    SupportedDurations = new[] { "6", "8", "10", "12", "14", "16", "18", "20" },
                    SupportedResolutions = new[] { "1920x1080", "2560x1440", "3840x2160" },
                    SupportedFpsValues = new[] { 25, 50 },
                    SupportedCameraMotions = LtxCameraMotions,
                    SupportsGenerateAudio = true,
                    SupportsRemix = false,
                    SupportsReferenceImage = false
                },
                new()
                {
                    ProviderType = VideoProviderType.Ltx,
                    Id = "ltx-2-pro",
                    DisplayName = "ltx-2-pro",
                    SupportedDurations = new[] { "6", "8", "10" },
                    SupportedResolutions = new[] { "1920x1080", "2560x1440", "3840x2160" },
                    SupportedFpsValues = new[] { 25, 50 },
                    SupportedCameraMotions = LtxCameraMotions,
                    SupportsGenerateAudio = true,
                    SupportsRemix = false,
                    SupportsReferenceImage = false
                },
                new()
                {
                    ProviderType = VideoProviderType.Ltx,
                    Id = "ltx-2-3-fast",
                    DisplayName = "ltx-2-3-fast",
                    SupportedDurations = new[] { "6", "8", "10", "12", "14", "16", "18", "20" },
                    SupportedResolutions = new[] { "1920x1080", "2560x1440", "3840x2160", "1080x1920", "1440x2560", "2160x3840" },
                    SupportedFpsValues = new[] { 24, 25, 48, 50 },
                    SupportedCameraMotions = LtxCameraMotions,
                    SupportsGenerateAudio = true,
                    SupportsRemix = false,
                    SupportsReferenceImage = false
                },
                new()
                {
                    ProviderType = VideoProviderType.Ltx,
                    Id = "ltx-2-3-pro",
                    DisplayName = "ltx-2-3-pro",
                    SupportedDurations = new[] { "6", "8", "10" },
                    SupportedResolutions = new[] { "1920x1080", "2560x1440", "3840x2160", "1080x1920", "1440x2560", "2160x3840" },
                    SupportedFpsValues = new[] { 24, 25, 48, 50 },
                    SupportedCameraMotions = LtxCameraMotions,
                    SupportsGenerateAudio = true,
                    SupportsRemix = false,
                    SupportsReferenceImage = false
                }
            };

        public static IReadOnlyList<VideoModelOption> GetModels(VideoProviderType providerType) =>
            providerType == VideoProviderType.Ltx ? LtxModels : OpenAiModels;
    }

    public sealed class OpenAiVideoProvider : IVideoGenerationProvider
    {
        private readonly VideoClient _videoClient;

        public OpenAiVideoProvider(VideoClient videoClient)
        {
            _videoClient = videoClient;
        }

        public VideoProviderType ProviderType => VideoProviderType.OpenAI;

        public IReadOnlyList<VideoModelOption> GetSupportedModels() => VideoProviderCatalog.OpenAiModels;

        public async Task<VideoSubmitResult> SubmitTextToVideoAsync(VideoGenerationRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            VideoClient.ResponseVideo? response;
            if (request.IsRemix)
            {
                response = await _videoClient.RemixVideoAsync(request.SourceVideoId ?? string.Empty, request.Prompt);
            }
            else
            {
                response = await _videoClient.CreateVideoAsync(new VideoClient.RequestVideo
                {
                    Prompt = request.Prompt,
                    Model = request.Model,
                    Seconds = request.Duration,
                    Size = request.Resolution
                });
            }

            var rawJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });

            if (response == null)
            {
                throw new InvalidOperationException("OpenAI video generation returned no response.");
            }

            if (response.Error != null)
            {
                throw new InvalidOperationException(response.Error.Message ?? "OpenAI video generation failed.");
            }

            return new VideoSubmitResult
            {
                JobId = response.Id ?? string.Empty,
                InitialStatus = response.Status ?? string.Empty,
                RawJson = rawJson
            };
        }

        public async Task<VideoPollResult> GetJobStatusAsync(string jobId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _videoClient.GetVideoStatusAsync(jobId);
            var rawJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });

            return new VideoPollResult
            {
                JobId = response?.Id ?? jobId,
                Status = response?.Status ?? string.Empty,
                RawJson = rawJson,
                ErrorMessage = response?.Error?.Message,
                ProgressPercent = response?.Progress,
                VideoUrl = null
            };
        }

        public async Task<string?> DownloadResultAsync(string videosFolder, string jobId, VideoPollResult finalStatus, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var success = await _videoClient.DownloadVideoAsync(videosFolder, jobId, progress);
            return success ? Path.Combine(videosFolder, $"{jobId}.mp4") : null;
        }
    }

    public sealed class LtxVideoProvider : IVideoGenerationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public LtxVideoProvider(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("LTX_API_KEY is not configured.");
            }

            _apiKey = apiKey;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.ltx.video/")
            };
        }

        public VideoProviderType ProviderType => VideoProviderType.Ltx;

        public IReadOnlyList<VideoModelOption> GetSupportedModels() => VideoProviderCatalog.LtxModels;

        public async Task<VideoSubmitResult> SubmitTextToVideoAsync(VideoGenerationRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payload = new LtxTextToVideoRequest
            {
                Prompt = request.Prompt,
                Model = request.Model,
                Duration = int.TryParse(request.Duration, out var duration) ? duration : 0,
                Resolution = request.Resolution,
                Fps = request.Fps,
                CameraMotion = string.IsNullOrWhiteSpace(request.CameraMotion) ? null : request.CameraMotion,
                GenerateAudio = request.GenerateAudio
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "v2/text-to-video");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(payload, SerializerOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"LTX text-to-video submit failed: {(int)response.StatusCode} {response.ReasonPhrase}\nURL: {_httpClient.BaseAddress}v2/text-to-video\n{rawJson}");
            }

            var submitResponse = JsonSerializer.Deserialize<LtxSubmitResponse>(rawJson, SerializerOptions)
                                 ?? throw new InvalidOperationException("LTX submit returned an invalid response.");

            return new VideoSubmitResult
            {
                JobId = submitResponse.Id ?? string.Empty,
                InitialStatus = "pending",
                CreatedAt = submitResponse.CreatedAt,
                RawJson = Pretty(rawJson)
            };
        }

        public async Task<VideoPollResult> GetJobStatusAsync(string jobId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativeUrl = $"v2/text-to-video/{jobId}";
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new VideoPollResult
                {
                    JobId = jobId,
                    Status = "not_found",
                    RawJson = Pretty(rawJson),
                    ErrorCode = "not_found",
                    ErrorMessage = "The LTX job was not found or the result has expired."
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"LTX status poll failed: {(int)response.StatusCode} {response.ReasonPhrase}\nURL: {_httpClient.BaseAddress}{relativeUrl}\n{rawJson}");
            }

            var statusResponse = JsonSerializer.Deserialize<LtxJobStatusResponse>(rawJson, SerializerOptions)
                                 ?? throw new InvalidOperationException("LTX status response was invalid.");

            string? videoUrl = null;
            statusResponse.Result?.TryGetValue("video_url", out videoUrl);

            return new VideoPollResult
            {
                JobId = statusResponse.Id ?? jobId,
                Status = statusResponse.Status ?? string.Empty,
                RawJson = Pretty(rawJson),
                ErrorCode = statusResponse.Error?.Type,
                ErrorMessage = statusResponse.Error?.Message,
                VideoUrl = videoUrl,
                CreatedAt = statusResponse.CreatedAt,
                CompletedAt = statusResponse.CompletedAt
            };
        }

        public async Task<string?> DownloadResultAsync(string videosFolder, string jobId, VideoPollResult finalStatus, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(finalStatus.VideoUrl))
            {
                return null;
            }

            Directory.CreateDirectory(videosFolder);
            progress?.Report(5);

            using var response = await _httpClient.GetAsync(finalStatus.VideoUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var extension = TryGetExtensionFromUrl(finalStatus.VideoUrl) ?? ".mp4";
            var safeId = string.Concat(jobId.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
            if (string.IsNullOrWhiteSpace(safeId))
            {
                safeId = Guid.NewGuid().ToString("N");
            }

            var filePath = Path.Combine(videosFolder, $"{safeId}{extension}");
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(filePath);

            var buffer = new byte[81920];
            long totalRead = 0;
            var contentLength = response.Content.Headers.ContentLength;
            while (true)
            {
                var bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead <= 0)
                    break;

                await output.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalRead += bytesRead;

                if (contentLength.HasValue && contentLength.Value > 0)
                {
                    var percent = 5d + (95d * totalRead / contentLength.Value);
                    progress?.Report(Math.Min(100d, percent));
                }
            }

            progress?.Report(100);
            return filePath;
        }

        private static string Pretty(string rawJson)
        {
            try
            {
                using var document = JsonDocument.Parse(rawJson);
                return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return rawJson;
            }
        }

        private static string? TryGetExtensionFromUrl(string url)
        {
            try
            {
                var path = new Uri(url).AbsolutePath;
                var ext = Path.GetExtension(path);
                return string.IsNullOrWhiteSpace(ext) ? null : ext;
            }
            catch
            {
                return null;
            }
        }

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        private sealed class LtxTextToVideoRequest
        {
            public string Prompt { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public int Duration { get; set; }
            public string Resolution { get; set; } = string.Empty;
            public int? Fps { get; set; }
            public string? CameraMotion { get; set; }
            public bool GenerateAudio { get; set; } = true;
        }

        private sealed class LtxSubmitResponse
        {
            public string? Id { get; set; }
            public DateTimeOffset? CreatedAt { get; set; }
        }

        private sealed class LtxJobStatusResponse
        {
            public string? Status { get; set; }
            public string? Id { get; set; }
            public DateTimeOffset? CreatedAt { get; set; }
            public DateTimeOffset? CompletedAt { get; set; }
            public Dictionary<string, string>? Result { get; set; }
            public LtxError? Error { get; set; }
        }

        private sealed class LtxError
        {
            public string? Type { get; set; }
            public string? Message { get; set; }
        }
    }
}
