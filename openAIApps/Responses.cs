using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace openAIApps
{
    internal class Responses : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string ResponsesEndpoint = "https://api.openai.com/v1/responses";
        
       

        public Responses(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        public async Task<string> GetResponseAsync(string prompt, string model = "computer-use-preview")
        {
            // Build the request
            var request = new ResponsesRequest
            {
                Model = model,
                Input = prompt,
                Truncation = "auto",
                Tools = new[]
                {
                    new ComputerUseTool
                    {
                        DisplayWidth = 3440,
                        DisplayHeight = 1440,
                        Environment = "windows"
                    }
                }
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(request, options);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(ResponsesEndpoint, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"Error {response.StatusCode}: {responseString}";

            // Parse the response
            var result = JsonSerializer.Deserialize<ResponsesResponse>(responseString, options);

            var sb = new StringBuilder();

            if (result?.Output != null)
            {
                foreach (var output in result.Output)
                {
                    foreach (var contentItem in output.Content ?? new List<ContentItem>())
                    {
                        if (contentItem.Type == "output_text" && !string.IsNullOrEmpty(contentItem.Text))
                        {
                            sb.AppendLine($"🧠 Text: {contentItem.Text}");

                            // Simple simulation for possible computer-use instructions
                            if (contentItem.Text.Contains("open", StringComparison.OrdinalIgnoreCase) &&
                                contentItem.Text.Contains("notepad", StringComparison.OrdinalIgnoreCase))
                            {
                                sb.AppendLine("💻 [Simulated Action] Would open Notepad.");
                            }
                            if (contentItem.Text.Contains("create", StringComparison.OrdinalIgnoreCase) &&
                                contentItem.Text.Contains(".txt", StringComparison.OrdinalIgnoreCase))
                            {
                                sb.AppendLine("💻 [Simulated Action] Would create a text file.");
                            }
                        }
                        else if (contentItem.Type == "tool_use")
                        {
                            sb.AppendLine($"🛠️ Tool call: {contentItem.ToolName}");
                            sb.AppendLine($"   Input: {contentItem.ToolInput}");
                        }
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : responseString;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        // ---------- Inner DTOs ----------
        private class ComputerUseTool
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "computer_use_preview";

            [JsonPropertyName("display_width")]
            public int DisplayWidth { get; set; }

            [JsonPropertyName("display_height")]
            public int DisplayHeight { get; set; }

            [JsonPropertyName("environment")]
            public string Environment { get; set; }
        }

        private class ResponsesRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("input")]
            public string Input { get; set; }

            [JsonPropertyName("truncation")]
            public string Truncation { get; set; } = "auto";

            [JsonPropertyName("tools")]
            public ComputerUseTool[] Tools { get; set; }
        }

        private class ResponsesResponse
        {
            [JsonPropertyName("output")]
            public List<OutputItem>? Output { get; set; }
        }

        private class OutputItem
        {
            [JsonPropertyName("content")]
            public List<ContentItem>? Content { get; set; }
        }

        private class ContentItem
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }  // "output_text" or "tool_use"

            [JsonPropertyName("text")]
            public string? Text { get; set; }

            [JsonPropertyName("tool_name")]
            public string? ToolName { get; set; }

            [JsonPropertyName("tool_input")]
            public JsonElement? ToolInput { get; set; }
        }
    }
}
