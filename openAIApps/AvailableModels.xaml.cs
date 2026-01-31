using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace openAIApps
{
    /// <summary>
    /// Interaction logic for AvailableModels.xaml
    /// </summary>
    public partial class AvailableModels : Window
    {
        public HttpClient HttpClient { get; }
        public string OpenAPIKey { get; }

        public AvailableModels()
        {
            InitializeComponent();
        }

        public AvailableModels(HttpClient httpClient, string openAPIKey)
        {
            HttpClient = httpClient;
            OpenAPIKey = openAPIKey;
        }

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

        public async Task<List<string>> GetAvailableModelsAsync(HttpClient httpClient, string apiKey, string endpoint)
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            var response = await httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var modelsResponse = JsonSerializer.Deserialize<ModelsResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var modelIds = modelsResponse?.Data?.Select(m => m.Id).ToList() ?? new List<string>();
            return modelIds;
        }

        public void UpdateAvailableModels(List<string> models)
        {
            InitializeComponent();
            cbModels.ItemsSource = models;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
