using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace openAIApps
{
    public class Responses : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string ResponsesEndpoint = "https://api.openai.com/v1/responses";
        // Add these properties to the Responses class (public for UI binding)
        public string CurrentReasoning { get; set; } = "none"; // default

        // Current configuration - updated by UI
        public string CurrentModel { get; set; } = "gpt-4o";
        public string CurrentTool { get; set; } = "text"; // default to plain text
                                                          // NEW: conversation tracking
        public string LastResponseId { get; private set; } = null;
        public bool ConversationActive => !string.IsNullOrEmpty(LastResponseId);

        public Responses(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        public async Task<string> GetResponseAsync(string prompt)
        {
            var request = BuildRequest(prompt);
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

            var result = JsonSerializer.Deserialize<ResponsesResponse>(responseString, options);
            
            // NEW: remember conversation id
            if (!string.IsNullOrEmpty(result?.Id))
                LastResponseId = result.Id;
            return ParseResponse(result);
        }

        private ResponsesRequest BuildRequest(string prompt)
        {
            var request = new ResponsesRequest
            {
                Model = CurrentModel,
                Input = prompt,
                Truncation = "auto",
                Tools = GetToolsForCurrentSelection(),
                Store = true,                             // keep responses on server
                PreviousResponseId = LastResponseId       // link to last turn if any
            };

            if (!string.IsNullOrEmpty(CurrentReasoning) && CurrentReasoning != "none")
            {
                request.Reasoning = new ReasoningConfig { Effort = CurrentReasoning };
            }

            return request;
        }

        private Tool[] GetToolsForCurrentSelection()
        {
            return CurrentTool switch
            {
                "text" => Array.Empty<Tool>(), // No tools, plain text
                "web_search" => new[]
                {
                    new WebSearchTool()
                },
                "reasoning" => new[]
                {
                    new ReasoningTool()
                },
                "computer_use" => new[]
                {
                    new ComputerUseTool
                    {
                        DisplayWidth = 3440,
                        DisplayHeight = 1440,
                        Environment = "windows"
                    }
                },
                _ => Array.Empty<Tool>()
            };
        }

        private string ParseResponse(ResponsesResponse result)
        {
            var sb = new StringBuilder();

            if (result?.Output != null)
            {
                foreach (var output in result.Output)
                {
                    foreach (var contentItem in output.Content ?? new List<ContentItem>())
                    {
                        switch (contentItem.Type)
                        {
                            case "output_text":
                                if (!string.IsNullOrEmpty(contentItem.Text))
                                    sb.AppendLine($"🧠 {contentItem.Text}");
                                break;

                            case "tool_use":
                                sb.AppendLine($"🛠️ Tool: {contentItem.ToolName ?? "unknown"}");
                                if (contentItem.ToolInput != null)
                                {
                                    sb.AppendLine($"   Input: {contentItem.ToolInput}");
                                    // Simulate tool execution
                                    sb.AppendLine($"   [Simulated: {SimulateTool(contentItem.ToolName, contentItem.ToolInput ?? default)}]");
                                }
                                break;
                        }
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : "No response content";
        }

        private string SimulateTool(string toolName, JsonElement toolInput)
        {
            string inputText = toolInput.ValueKind == JsonValueKind.Undefined ||
                       toolInput.ValueKind == JsonValueKind.Null
            ? "no input"
            : toolInput.GetRawText();

                return toolName switch
                {
                    "web_search" => $"Web search would execute: {inputText}",
                    "reasoning" => $"Reasoning step executed with input: {inputText}",
                    "computer_use_preview" => "Computer use action simulated (requires access)",
                    _ => $"Tool execution simulated: {inputText}"
                };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
        public void ClearConversation()
        {
            LastResponseId = null;
        }

        public async Task<bool> DeleteConversationAsync(string responseId)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete,
                $"{ResponsesEndpoint}/{responseId}");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadAsStringAsync();
            // API returns { "id": "...", "object": "response", "deleted": true }
            return body.Contains("\"deleted\": true", StringComparison.OrdinalIgnoreCase);
        }

        // ---------- Tool Definitions ----------
        private abstract class Tool
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }
        }

        private class WebSearchTool : Tool
        {
            public WebSearchTool()
            {
                Type = "web_search";
            }
        }

        private class ReasoningTool : Tool
        {
            public ReasoningTool()
            {
                Type = "reasoning";
            }
        }

        private class ComputerUseTool : Tool
        {
            public ComputerUseTool()
            {
                Type = "computer_use_preview";
            }

            [JsonPropertyName("display_width")]
            public int DisplayWidth { get; set; } = 3440;

            [JsonPropertyName("display_height")]
            public int DisplayHeight { get; set; } = 1440;

            [JsonPropertyName("environment")]
            public string Environment { get; set; } = "windows";
        }

        // ---------- DTOs ----------
        private class ResponsesRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("input")]
            public string Input { get; set; }

            [JsonPropertyName("truncation")]
            public string Truncation { get; set; } = "auto";

            [JsonPropertyName("tools")]
            public Tool[] Tools { get; set; }
            
            [JsonPropertyName("reasoning")]
            public ReasoningConfig Reasoning { get; set; }
            [JsonPropertyName("store")]
            public bool Store { get; set; } = true;

            [JsonPropertyName("previous_response_id")]
            public string PreviousResponseId { get; set; }
        }

        private class ResponsesResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("output")]
            public List<OutputItem>? Output { get; set; }

            [JsonPropertyName("previous_response_id")]
            public string PreviousResponseId { get; set; }
        }

        private class OutputItem
        {
            [JsonPropertyName("content")]
            public List<ContentItem>? Content { get; set; }
        }

        private class ContentItem
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }

            [JsonPropertyName("tool_name")]
            public string? ToolName { get; set; }

            [JsonPropertyName("tool_input")]
            public JsonElement? ToolInput { get; set; }
        }
        private class ReasoningConfig
        {
            [JsonPropertyName("effort")]
            public string Effort { get; set; }
        }
    }
}
