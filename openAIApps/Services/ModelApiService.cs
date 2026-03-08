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
        public static async Task<List<string>> GetAvailableModelsAsync(HttpClient httpClient, string apiKey, string endpoint)
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var modelsResponse = JsonSerializer.Deserialize<ModelsResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return modelsResponse?.Data?.Select(m => m.Id).ToList() ?? new List<string>();
        }
    }
}