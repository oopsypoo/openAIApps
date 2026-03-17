using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


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
        // Updated to accept the DB-rehydrated history
        // Responses.cs

        public async Task<ResponsesResult> GetChatCompletionAsync(List<object> openAIContext)
        {
            // 1. Build the request object using our new overload
            var request = BuildRequest(openAIContext);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };

            // 2. Serialize and Send
            var json = JsonSerializer.Serialize(request, options);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            //using var response = await _httpClient.PostAsync(ResponsesEndpoint, content);
            var response = await _httpClient.PostAsync(ResponsesEndpoint, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI Error {response.StatusCode}: {responseString}");
            }

            // 3. Deserialize the raw response
            var apiResponse = JsonSerializer.Deserialize<ResponsesResponse>(responseString, options);

            if (!string.IsNullOrEmpty(apiResponse?.Id))
            {
                LastResponseId = apiResponse.Id;
            }

            var parsed = ParseResponseRich(apiResponse);
            parsed.RawJson = responseString;
            return parsed;
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
            var tools = GetToolsForCurrentSelection();

            var request = new ResponsesRequest
            {
                Model = CurrentModel,
                Input = prompt,
                Truncation = "auto",
                Tools = tools.Cast<object>().ToArray(),
                Store = false,
                PreviousResponseId = LastResponseId
            };

            if (!string.IsNullOrEmpty(CurrentReasoning) && CurrentReasoning != "none")
            {
                request.Reasoning = new ReasoningConfig { Effort = CurrentReasoning };
            }

            if (tools.Length > 0)
            {
                if (ActiveTools.Contains(ResponseToolKeys.ImageGeneration) && ActiveTools.Count == 1)
                {
                    request.ToolChoice = new { type = "image_generation" };
                }
                else
                {
                    request.ToolChoice = "auto";
                }
            }

            return request;
        }
        // Responses.cs

        /// <summary>
        /// Overload for SQLite: Builds a request using the full conversation context.
        /// </summary>
        private ResponsesRequest BuildRequest(List<object> context)
        {
            var tools = GetToolsForCurrentSelection();

            var request = new ResponsesRequest
            {
                Model = CurrentModel,
                Input = context,
                Store = false,
                PreviousResponseId = null,
                Tools = tools.Cast<object>().ToArray()
            };

            if (!string.IsNullOrEmpty(CurrentReasoning) && CurrentReasoning != "none")
            {
                request.Reasoning = new ReasoningConfig { Effort = CurrentReasoning };
            }

            if (tools.Length > 0)
            {
                if (ActiveTools.Contains(ResponseToolKeys.ImageGeneration) && ActiveTools.Count == 1)
                {
                    request.ToolChoice = new { type = "image_generation" };
                }
                else
                {
                    request.ToolChoice = "auto";
                }
            }

            return request;
        }
        private Tool[] GetToolsForCurrentSelection()
        {
            var tools = new List<Tool>();

            if (ActiveTools.Contains(ResponseToolKeys.WebSearch))
            {
                tools.Add(new WebSearchTool
                {
                    SearchContextSize = WebSearchContextSize
                });
            }

            if (ActiveTools.Contains(ResponseToolKeys.ComputerUsePreview))
            {
                tools.Add(new ComputerUseTool
                {
                    DisplayWidth = 3440,
                    DisplayHeight = 1440,
                    Environment = "windows"
                });
            }

            if (ActiveTools.Contains(ResponseToolKeys.ImageGeneration))
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
            //public Tool[] Tools { get; set; }
            //serializing as object[] to allow empty array or null
            public Object[] Tools { get; set; }

            [JsonPropertyName("tool_choice")]
            public object ToolChoice { get; set; }

            [JsonPropertyName("reasoning")]
            public ReasoningConfig Reasoning { get; set; }
            [JsonPropertyName("store")]
            public bool Store { get; set; } = false;
            [JsonPropertyName("instructions")]
            public string Instructions { get; set; } = "You are a helpful assistant. Return all answers as GitHub-style Markdown. Use headings, bullet lists, and fenced code blocks ";

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
        

        public class ResponsesResult
        {
            public string AssistantText { get; set; }
            public string RawJson { get; set; }
            public List<string> ImagePayloads { get; set; } = new();
            // You can add this if you want the if(result.IsSuccess) syntax:
            public bool IsSuccess => !string.IsNullOrEmpty(AssistantText);
        }


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
                                    sb.AppendLine($"{contentItem.Text}");
                                break;

                            case "tool_use":
                                sb.AppendLine($"Tool: {contentItem.ToolName ?? "unknown"}");
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



            return new ResponsesResult
            {
                AssistantText = assistantText,
                ImagePayloads = images  // base64 images from tool
            };
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

            var parsed = ParseResponseRich(result);
            parsed.RawJson = responseString;
            return parsed;
        }

        private ResponsesRequest BuildRequestWithImage(string prompt, string imagePath)
        {
            string dataUrl = ImageInputHelper.ToDataUrl(imagePath);

            object inputObject;

            if (!string.IsNullOrWhiteSpace(prompt) && !string.IsNullOrEmpty(dataUrl))
            {
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
                inputObject = prompt;
            }
            else if (!string.IsNullOrEmpty(dataUrl))
            {
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
                inputObject = prompt ?? string.Empty;
            }

            var tools = GetToolsForCurrentSelection();

            var request = new ResponsesRequest
            {
                Model = CurrentModel,
                Input = inputObject,
                Truncation = "auto",
                Tools = tools.Cast<object>().ToArray(),
                Store = false,
                PreviousResponseId = LastResponseId
            };

            if (!string.IsNullOrEmpty(CurrentReasoning) && CurrentReasoning != "none")
            {
                request.Reasoning = new ReasoningConfig
                {
                    Effort = CurrentReasoning
                };
            }

            if (tools.Length > 0)
            {
                if (ActiveTools.Contains(ResponseToolKeys.ImageGeneration) && ActiveTools.Count == 1)
                {
                    request.ToolChoice = new { type = "image_generation" };
                }
                else
                {
                    request.ToolChoice = "auto";
                }
            }

            return request;
        }
    }
}
