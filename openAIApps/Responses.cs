using openAIApps.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Object = System.Object;


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
        public string ImageGenOutputFormat { get; set; } = "jpeg";
        public int ImageGenOutputCompression { get; set; } = 85;
        public string ImageGenBackground { get; set; } = "auto";
        public string ImageGenInputFidelity { get; set; } = "high";

        public Responses(string apiKey)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        }
        // Updated to accept the DB-rehydrated history
        // Responses.cs

        public async Task<ResponsesResult> GetChatCompletionAsync(List<object> openAIContext, IProgress<string> progress = null)
        {
            // 1. Build the request object using our new overload
            progress?.Report("Building request");
            var request = BuildRequest(openAIContext);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };

            // 2. Serialize and Send
            var json = JsonSerializer.Serialize(request, options);
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Responses request JSON:");
            System.Diagnostics.Debug.WriteLine(json);
#endif
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            //using var response = await _httpClient.PostAsync(ResponsesEndpoint, content);
            progress?.Report("Sending request to OpenAI");
            var response = await _httpClient.PostAsync(ResponsesEndpoint, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                progress?.Report($"OpenAI Error {response.StatusCode}: {responseString}");
                throw new Exception($"OpenAI Error {response.StatusCode}: {responseString}");
            }
            else
                progress?.Report("Response from OpenAI successfull");

            // 3. Deserialize the raw response
            var apiResponse = JsonSerializer.Deserialize<ResponsesResponse>(responseString, options);

            if (!string.IsNullOrEmpty(apiResponse?.Id))
            {
                LastResponseId = apiResponse.Id;
            }
            progress?.Report("Parsing response");
            var parsed = ParseResponseRich(apiResponse);
            parsed.RawJson = responseString;
            parsed.ImageOutputFormat = NormalizeImageOutputFormat(ImageGenOutputFormat);
            return parsed;
        }
        private string BuildInstructions(DeveloperToolsOptions developerToolsOptions)
        {
            var baseInstructions = @"Return all final answers as clean, well-formed GitHub-style Markdown.
                                Use Markdown features fully when they improve clarity, including:
                                -headings
                                - short paragraphs
                                - bullet lists
                                - numbered lists
                                - nested lists
                                - task lists
                                - tables
                                - blockquotes
                                - inline code
                                - fenced code blocks with language tags
                                - links
                                - horizontal rules

                                Formatting rules:
                                -Use headings to organize longer answers.
                                - Use bullet lists and numbered lists where appropriate.
                                - Use nested lists when structure benefits from it.
                                - Use fenced code blocks with the correct language tag whenever you provide code.
                                - When providing C#, XAML, JSON, XML, PowerShell, bash, SQL, or JavaScript examples, always use fenced code blocks with the correct language tag.
                                -Use inline code for identifiers, class names, method names, file names, commands, property names, enum values, and config keys.
                                - Use tables when they clearly improve readability.
                                - Use blockquotes for notes, warnings, and important remarks.
                                - Keep Markdown valid, clean, and well-formed.
                                - Do not wrap the entire answer in a single code block.
                                - Do not use raw HTML unless explicitly requested.
                                - Prefer readable structure over dense prose.
                                For technical answers, prefer this structure when useful:
                                1. short summary
                                2. step-by-step instructions
                                3. code examples
                                4. notes or caveats
                                """;

            if (developerToolsOptions == null || !developerToolsOptions.Enabled)
                return baseInstructions;

            string allowedExtensions = developerToolsOptions.AllowedExtensions?.Length > 0
                ? string.Join(", ", developerToolsOptions.AllowedExtensions)
                : "(application-defined)";

            return
                    $@"{baseInstructions}

                        You are assisting inside a local C# / WPF project workspace.

                        You have access to read-only local developer tools.
                        The repository root is fixed by the application and cannot be changed.
                        Allowed file types are: {allowedExtensions}

                        Behavior rules:
                        - Prefer search_project_text before reading files.
                        - Read only the minimum number of files needed.
                        - Prefer narrow line ranges when reading files.
                        - Use list_project_files only when needed.
                        - Treat tool results as the source of truth.
                        - Do not assume access outside the configured repository root.
                        - Do not claim to modify files directly.";
        }
        private object[] GetLocalFunctionTools(DeveloperToolsOptions developerToolsOptions)
        {
            if (developerToolsOptions == null || !developerToolsOptions.Enabled)
                return Array.Empty<object>();

            var tools = new List<object>();

            if (developerToolsOptions.SearchProjectTextEnabled)
            {
                tools.Add(new
                {
                    type = "function",
                    name = "search_project_text",
                    description = "Search text in allowed files under the configured repository root. Returns matching relative paths, line numbers, and snippets. Read-only.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "Text to search for."
                            },
                            glob = new
                            {
                                type = "string",
                                description = "Optional filter like *.cs or *.xaml."
                            },
                            subpath = new
                            {
                                type = "string",
                                description = "Optional relative subfolder inside the repository root."
                            },
                            case_sensitive = new
                            {
                                type = "boolean",
                                description = "Whether the search should be case sensitive."
                            },
                            max_results = new
                            {
                                type = "integer",
                                minimum = 1,
                                maximum = 200
                            }
                        },
                        required = new[] { "query" },
                        additionalProperties = false
                    }
                });
            }

            if (developerToolsOptions.ReadProjectFileEnabled)
            {
                tools.Add(new
                {
                    type = "function",
                    name = "read_project_file",
                    description = "Read a text file from the configured repository root using a relative path. Returns line-numbered text. Read-only.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new
                            {
                                type = "string",
                                description = "Relative file path inside the repository root."
                            },
                            start_line = new
                            {
                                type = "integer",
                                minimum = 1
                            },
                            end_line = new
                            {
                                type = "integer",
                                minimum = 1
                            }
                        },
                        required = new[] { "path" },
                        additionalProperties = false
                    }
                });
            }

            if (developerToolsOptions.ListProjectFilesEnabled)
            {
                tools.Add(new
                {
                    type = "function",
                    name = "list_project_files",
                    description = "List files under the configured repository root. Returns relative paths only. Read-only.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            subpath = new { type = "string" },
                            glob = new { type = "string" },
                            max_results = new
                            {
                                type = "integer",
                                minimum = 1,
                                maximum = 500
                            }
                        },
                        additionalProperties = false
                    }
                });
            }

            return tools.ToArray();
        }
        private ResponsesRequest BuildRequest(object input, string previousResponseId, DeveloperToolsOptions developerToolsOptions)
        {
            var hostedTools = GetToolsForCurrentSelection().Cast<object>().ToList();
            var localFunctionTools = GetLocalFunctionTools(developerToolsOptions);

            var allTools = hostedTools.Concat(localFunctionTools).ToArray();

            var request = new ResponsesRequest
            {
                Model = CurrentModel,
                Input = input,
                Store = true,
                PreviousResponseId = previousResponseId,
                Tools = allTools,
                ToolChoice = allTools.Length > 0 ? "auto" : null,
                ParallelToolCalls = true
            };

            if (!string.IsNullOrEmpty(CurrentReasoning) && CurrentReasoning != "none")
            {
                request.Reasoning = new ReasoningConfig { Effort = CurrentReasoning };
            }

            request.Instructions = BuildInstructions(developerToolsOptions);

            return request;
        }

        public async Task<ResponsesResult> GetChatCompletionWithLocalToolsAsync(
        List<object> openAIContext,
        DeveloperToolsOptions developerToolsOptions,
        Func<string, string, Task<bool>> confirmLocalCallAsync = null,
        Func<string, string, string, Task> onToolCallLoggedAsync = null,
        IProgress<string> progress = null)
        {
            if (developerToolsOptions == null ||
                !developerToolsOptions.Enabled ||
                string.IsNullOrWhiteSpace(developerToolsOptions.RepositoryRoot))
            {
                progress?.Report("Sending request to OpenAI...");
                return await GetChatCompletionAsync(openAIContext, progress);
            }

            var guard = new WorkspaceGuard(developerToolsOptions);
            var fileService = new ProjectFileToolService(developerToolsOptions, guard);
            var searchService = new ProjectSearchToolService(developerToolsOptions, guard);
            var dispatcher = new LocalToolDispatcher(fileService, searchService);

            string previousResponseId = null;
            object currentInput = openAIContext;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };

            while (true)
            {
                progress?.Report("Building request...");
                var request = BuildRequest(currentInput, previousResponseId, developerToolsOptions);

                var json = JsonSerializer.Serialize(request, options);
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Responses request JSON:");
                System.Diagnostics.Debug.WriteLine(json);
#endif
                progress?.Report("Sending request to OpenAI...");
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(ResponsesEndpoint, content);
                progress?.Report("Reading response content..");
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    progress?.Report($"OpenAI returned error: {(int)response.StatusCode} {response.StatusCode}");
                    throw new Exception($"OpenAI Error {response.StatusCode}: {responseString}");
                }
                progress?.Report("Parsing response...");
                var apiResponse = JsonSerializer.Deserialize<ResponsesResponse>(responseString, options);

                if (!string.IsNullOrEmpty(apiResponse?.Id))
                {
                    previousResponseId = apiResponse.Id;
                    LastResponseId = apiResponse.Id; // okay to store, but not use as input source
                }

                var functionCalls = ExtractFunctionCalls(apiResponse);

                if (functionCalls.Count == 0)
                {
                    progress?.Report("Finalizing response...");
                    var parsed = ParseResponseRich(apiResponse);
                    parsed.RawJson = responseString;
                    parsed.ImageOutputFormat = NormalizeImageOutputFormat(ImageGenOutputFormat);
                    return parsed;
                }

                var toolOutputs = new List<object>();
                progress?.Report($"Model requested {functionCalls.Count} local tool call(s)...");
                foreach (var call in functionCalls)
                {
                    if (confirmLocalCallAsync != null)
                    {
                        progress?.Report($"Running local tool: {call.Name}...");
                        bool allowed = await confirmLocalCallAsync(call.Name, call.Arguments ?? "{}");
                        if (!allowed)
                        {
                            progress?.Report($"Local tool denied: {call.Name}. Continuing without it...");
                            string denied = JsonSerializer.Serialize(new
                            {
                                ok = false,
                                error = "User denied local tool call."
                            });

                            if (onToolCallLoggedAsync != null)
                                await onToolCallLoggedAsync(call.Name, call.Arguments ?? "{}", denied);

                            toolOutputs.Add(new
                            {
                                type = "function_call_output",
                                call_id = call.CallId,
                                output = denied
                            });

                            continue;
                        }
                    }

                    progress?.Report($"Executing local tool: {call.Name}...");
                    string toolResult = await dispatcher.DispatchAsync(call.Name, call.Arguments ?? "{}");
                    progress?.Report($"Local tool finished: {call.Name}. Sending output back...");

                    if (onToolCallLoggedAsync != null)
                        await onToolCallLoggedAsync(call.Name, call.Arguments ?? "{}", toolResult);

                    toolOutputs.Add(new
                    {
                        type = "function_call_output",
                        call_id = call.CallId,
                        output = toolResult
                    });
                }

                currentInput = toolOutputs;
                progress?.Report("Continuing conversation with tool outputs...");
            }
        }
        private static string NormalizeImageOutputFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                return "png";

            return format.Trim().ToLowerInvariant() switch
            {
                "jpg" => "jpeg",
                "jpeg" => "jpeg",
                "png" => "png",
                "webp" => "webp",
                _ => "png"
            };
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
                int? compression = null;

                if (string.Equals(ImageGenOutputFormat, "jpeg", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ImageGenOutputFormat, "webp", StringComparison.OrdinalIgnoreCase))
                {
                    compression = ImageGenOutputCompression;
                }

                tools.Add(new ImageGenerationTool(
                    ImageGenQuality,
                    ImageGenSize,
                    ImageGenOutputFormat,
                    compression,
                    ImageGenBackground,
                    ImageGenInputFidelity));
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
            public ImageGenerationTool(
                string quality,
                string size,
                string outputFormat,
                int? outputCompression,
                string background,
                string inputFidelity)
            {
                Type = "image_generation";
                Quality = quality ?? "auto";
                Size = size ?? "auto";
                OutputFormat = string.IsNullOrWhiteSpace(outputFormat) ? "jpeg" : outputFormat;
                Background = string.IsNullOrWhiteSpace(background) ? "auto" : background;
                InputFidelity = string.IsNullOrWhiteSpace(inputFidelity) ? "high" : inputFidelity;

                if (outputCompression.HasValue)
                    OutputCompression = outputCompression.Value;
            }

            [JsonPropertyName("quality")]
            public string Quality { get; set; } = "auto";

            [JsonPropertyName("size")]
            public string Size { get; set; } = "auto";

            [JsonPropertyName("output_format")]
            public string OutputFormat { get; set; } = "jpeg";

            [JsonPropertyName("output_compression")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? OutputCompression { get; set; }

            [JsonPropertyName("background")]
            public string Background { get; set; } = "auto";

            [JsonPropertyName("input_fidelity")]
            public string InputFidelity { get; set; } = "high";
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

            [JsonPropertyName("parallel_tool_calls")]
            public bool ParallelToolCalls { get; set; } = true;

            [JsonPropertyName("reasoning")]
            public ReasoningConfig Reasoning { get; set; }

            [JsonPropertyName("store")]
            public bool Store { get; set; } = false;
            [JsonPropertyName("instructions")]
            public string Instructions { get; set; } = "Return all final answers as clean, well-formed GitHub-style Markdown.\r\n\r\nUse Markdown features fully when they improve clarity, including:\r\n- headings\r\n- short paragraphs\r\n- bullet lists\r\n- numbered lists\r\n- nested lists\r\n- task lists\r\n- tables\r\n- blockquotes\r\n- inline code\r\n- fenced code blocks with language tags\r\n- links\r\n- horizontal rules\r\n\r\nFormatting rules:\r\n- Use headings to organize longer answers.\r\n- Use bullet lists and numbered lists where appropriate.\r\n- Use nested lists when structure benefits from it.\r\n- Use fenced code blocks with the correct language tag whenever you provide code.\r\n- Use inline code for identifiers, class names, method names, file names, commands, property names, enum values, and config keys.\r\n- Use tables when they clearly improve readability.\r\n- Use blockquotes for notes, warnings, and important remarks.\r\n- Keep Markdown valid, clean, and well-formed.\r\n- Do not wrap the entire answer in a single code block.\r\n- Do not use raw HTML unless explicitly requested.\r\n- Prefer readable structure over dense prose.";

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

            [JsonPropertyName("result")]
            public string? Result { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("arguments")]
            public string? Arguments { get; set; }

            [JsonPropertyName("call_id")]
            public string? CallId { get; set; }
        }
        private sealed class FunctionCallItem
        {
            public string Name { get; set; }
            public string Arguments { get; set; }
            public string CallId { get; set; }
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
            public string ImageOutputFormat { get; set; } = "png";
            public bool IsSuccess => !string.IsNullOrEmpty(AssistantText);
        }

        private List<FunctionCallItem> ExtractFunctionCalls(ResponsesResponse response)
        {
            var result = new List<FunctionCallItem>();

            if (response?.Output == null)
                return result;

            foreach (var item in response.Output)
            {
                if (!string.Equals(item.Type, "function_call", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.CallId))
                    continue;

                result.Add(new FunctionCallItem
                {
                    Name = item.Name,
                    Arguments = item.Arguments ?? "{}",
                    CallId = item.CallId
                });
            }

            return result;
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

    }
}
