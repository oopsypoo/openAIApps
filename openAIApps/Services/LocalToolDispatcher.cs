using System.Text.Json;
using System.Threading.Tasks;

namespace openAIApps.Services
{
    public sealed class LocalToolDispatcher
    {
        private readonly ProjectFileToolService _fileService;
        private readonly ProjectSearchToolService _searchService;

        public LocalToolDispatcher(ProjectFileToolService fileService, ProjectSearchToolService searchService)
        {
            _fileService = fileService;
            _searchService = searchService;
        }

        public Task<string> DispatchAsync(string toolName, string argumentsJson)
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var root = doc.RootElement;

            string result = toolName switch
            {
                "read_project_file" => _fileService.ReadProjectFile(
                    GetString(root, "path"),
                    GetInt(root, "start_line"),
                    GetInt(root, "end_line")),

                "search_project_text" => _searchService.SearchProjectText(
                    GetString(root, "query"),
                    GetString(root, "glob"),
                    GetString(root, "subpath"),
                    GetBool(root, "case_sensitive"),
                    GetInt(root, "max_results")),

                "list_project_files" => _fileService.ListProjectFiles(
                    GetString(root, "subpath"),
                    GetString(root, "glob"),
                    GetInt(root, "max_results")),

                _ => JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = $"Unknown tool: {toolName}"
                })
            };

            return Task.FromResult(result);
        }

        private static string GetString(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
        }

        private static int? GetInt(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
                ? prop.GetInt32()
                : (int?)null;
        }

        private static bool? GetBool(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var prop) &&
                   (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                ? prop.GetBoolean()
                : (bool?)null;
        }
    }
}