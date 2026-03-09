using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace openAIApps
{
    public class ModelsResponse
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public List<ModelData> Data { get; set; } = new();
    }

    public class ModelData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    public static class ModelApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string ModelsEndpoint = "https://api.openai.com/v1/models";
        public static async Task<List<string>> GetAvailableModelsAsync(string apiKey, string? endpoint = null)
        {
            string url = string.IsNullOrWhiteSpace(endpoint)
                ? ModelsEndpoint
                : endpoint;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            var modelsResponse = JsonSerializer.Deserialize<ModelsResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return modelsResponse?.Data?
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .OrderBy(id => id)
                .ToList()
                ?? new List<string>();
        }
    }
}