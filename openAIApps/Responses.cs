using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static openAIApps.MainWindow;

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
        public HashSet<string> ActiveTools { get; } = new();
        public string WebSearchContextSize { get; set; } = "medium";
        public string ImageGenQuality { get; set; } = "auto";
        public string ImageGenSize { get; set; } = "auto";


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
            var tools = new List<Tool>();

            // "text" means: no tools at all → leave list empty
            if (ActiveTools.Contains("web_search"))
                tools.Add(new WebSearchTool
                {
                    SearchContextSize = WebSearchContextSize
                });

            if (ActiveTools.Contains("computer_use"))
                tools.Add(new ComputerUseTool
                {
                    DisplayWidth = 3440,
                    DisplayHeight = 1440,
                    Environment = "windows"
                });
            if (ActiveTools.Contains("image_generation"))
            {
                tools.Add(new ImageGenerationTool(ImageGenQuality, ImageGenSize));
            }

            return tools.ToArray();
        }

        private string ParseResponse(ResponsesResponse result)
        {
            var sb = new StringBuilder();
            string assistantText = "";

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
            // Log the turn
            assistantText = sb.ToString().TrimEnd();
            
            if (!string.IsNullOrEmpty(result?.Id))
            {
                ConversationLog.Add(new ResponsesTurn
                {
                    ResponseId = result.Id,
                    // UserText will be injected from MainWindow (see below)
                    AssistantText = assistantText
                });
            }

            return assistantText.Length > 0 ? assistantText : "No response content";
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
                Type = "web_search_preview";
            }
            [JsonPropertyName("search_context_size")]
            public string SearchContextSize { get; set; } = "low"; // optional: low/medium/high
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

        private class ImageGenerationTool : Tool
        {
            public ImageGenerationTool(string quality, string size)
            {
                Type = "image_generation";
                Quality = quality ?? "auto";
                Size = size ?? "auto";
            }

            [JsonPropertyName("quality")]
            public string Quality { get; set; } = "auto";

            [JsonPropertyName("size")]
            public string Size { get; set; } = "auto";
        }


        // ---------- DTOs ----------
        private class ResponsesRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("input")]
            public object Input { get; set; }

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
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("content")]
            public List<ContentItem>? Content { get; set; }
            // For image_generation_call
            [JsonPropertyName("result")]
            public string? Result { get; set; }
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
        /// <summary>
        /// Class for minimal log of turns (user + assistant text, plus response id)
        /// </summary>
        public class ResponsesTurn
        {
            public string ResponseId { get; set; }
            public string UserText { get; set; }
            public string AssistantText { get; set; }

            // User-side image
            public string ImagePath { get; set; }

            // Assistant-side images for this turn
            public List<string> AssistantImagePaths { get; set; } = new();

            public override string ToString()
            {
                if (!string.IsNullOrWhiteSpace(UserText))
                {
                    var trimmed = UserText.Trim();
                    return trimmed.Length > 40 ? trimmed[..40] + "..." : trimmed;
                }
                return ResponseId;
            }
        }

        public class ResponsesResult
{
    public string AssistantText { get; set; }
    public List<string> ImagePayloads { get; set; } = new();
}

        public void AddTurn(string responseId, string assistantText, string imagePath)
        {
            ConversationLog.Add(new ResponsesTurn
            {
                ResponseId = responseId,
                AssistantText = assistantText,
                ImagePath = imagePath
            });
        }

        public List<ResponsesTurn> ConversationLog { get; } = new();

        private ResponsesResult ParseResponseRich(ResponsesResponse result)
        {
            var sb = new StringBuilder();
            var images = new List<string>();

            if (result?.Output != null)
            {
                foreach (var output in result.Output)
                {
                    // 1) Image generation call: result is base64 image data
                    if (string.Equals(output.Type, "image_generation_call", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(output.Result))
                        {
                            images.Add(output.Result); // pure base64
                        }
                        continue;
                    }

                    // 2) Normal text/tool output
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
                                    sb.AppendLine($" Input: {contentItem.ToolInput}");
                                sb.AppendLine($" [Simulated: {SimulateTool(contentItem.ToolName, contentItem.ToolInput ?? default)}]");
                                break;
                        }
                    }
                }
            }

            var assistantText = sb.ToString().TrimEnd();
            if (string.IsNullOrEmpty(assistantText))
                assistantText = "No response content";

            if (!string.IsNullOrEmpty(result?.Id))
            {
                ConversationLog.Add(new ResponsesTurn
                {
                    ResponseId = result.Id,
                    AssistantText = assistantText
                    // UserText/ImagePath set from MainWindow
                });
            }

            return new ResponsesResult
            {
                AssistantText = assistantText,
                ImagePayloads = images  // base64 images from tool
            };
        }



        /// <summary>
        /// Sets the user text for the most recent entry in the conversation log.
        /// </summary>
        /// <remarks>If the conversation log contains no entries, this method does not modify
        /// anything.</remarks>
        /// <param name="userText">The text to associate with the last conversation entry. If the conversation log is empty, this parameter is
        /// ignored.</param>
        public void SetLastUserText(string userText)
        {
            if (ConversationLog.Count == 0) return;
            ConversationLog[^1].UserText = userText;
        }
        // In Responses.cs
        
        public async Task<ResponsesResult> GetResponseAsync(string prompt, string imagePath)
        {
            var request = BuildRequestWithImage(prompt, imagePath);

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
            {
                return new ResponsesResult
                {
                    AssistantText = $"Error {response.StatusCode}: {responseString}"
                };
            }

            var result = JsonSerializer.Deserialize<ResponsesResponse>(responseString, options);

            if (!string.IsNullOrEmpty(result?.Id))
                LastResponseId = result.Id;

            return ParseResponseRich(result);
        }

        private ResponsesRequest BuildRequestWithImage(string prompt, string imagePath)
        {
            // Convert image to data URL (may be null)
            string dataUrl = ImageInputHelper.ToDataUrl(imagePath);

            object inputObject;

            if (!string.IsNullOrWhiteSpace(prompt) && !string.IsNullOrEmpty(dataUrl))
            {
                // Mixed text + image
                inputObject = new[]
                {
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "input_text", text = prompt },
                    new { type = "input_image", image_url = dataUrl }
                }
            }
        };
            }
            else if (!string.IsNullOrWhiteSpace(prompt))
            {
                // Text only (keep it compatible with your old style if you want)
                inputObject = prompt;
            }
            else if (!string.IsNullOrEmpty(dataUrl))
            {
                // Image only
                inputObject = new[]
                {
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "input_image", image_url = dataUrl }
                }
            }
        };
            }
            else
            {
                // Fallback: empty text
                inputObject = prompt ?? string.Empty;
            }

            var request = new ResponsesRequest
            {
                Model = CurrentModel,
                Input = inputObject,
                Truncation = "auto",
                Tools = GetToolsForCurrentSelection(),
                Store = true,
                PreviousResponseId = LastResponseId
            };

            if (!string.IsNullOrEmpty(CurrentReasoning) && CurrentReasoning != "none")
            {
                request.Reasoning = new ReasoningConfig
                {
                    Effort = CurrentReasoning
                };
            }

            return request;
        }



    }
}
