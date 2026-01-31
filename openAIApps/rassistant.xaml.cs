using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace openAIApps
{
    /// <summary>
    /// 
    /// This is a retreival assistant. It's main purpose is to help users of the OpenAiApps program it can do.
    /// Files that will be uploaded are source-files and and README.md file. If thisis successfull or not we will see.
    /// In my logic: If the commentary is good in the sourcefiles, maybe the help-assistant will be good.
    /// </summary>
    /// 
    public partial class rassistant : Window
    {

        /// <summary>
        /// My assistant is allready made: asst_YQ30hXuDo9LKg7wrLDQQmyCG
        /// GET https://api.openai.com/v1/assistants/asst_YQ30hXuDo9LKg7wrLDQQmyCG
        /// 
        /// </summary>
        public string id { get; set; }
        public string _object { get; set; }
        public int created_at { get; set; }
        public string name { get; set; }
        public object description { get; set; }
        public string model { get; set; }
        public string instructions { get; set; }
        /// <summary>
        /// tools is optional. But in my case it's retrieval
        /// </summary>
        public Tool[] tools { get; set; }
        /// <summary>
        /// file-id's is optional. In this case it's README.md and all source-files
        /// </summary>
        public object[] file_ids { get; set; }
        public Metadata metadata { get; set; }

        protected static string GetAssistantUri()
        {
            return "https://api.openai.com/v1/assistants/asst_YQ30hXuDo9LKg7wrLDQQmyCG";
        }
        public async Task<T> GetDataAsync<T>()
        {
            using (var client = new HttpClient())
            {
                // Optionally configure the HttpClient instance (e.g., add default headers)
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

                // Send the GET request
                HttpResponseMessage response = await client.GetAsync(GetAssistantUri());
                response.EnsureSuccessStatusCode();

                // Read the response as a string
                string jsonString = await response.Content.ReadAsStringAsync();

                // Deserialize the JSON string into the specified type T
                T data = JsonSerializer.Deserialize<T>(jsonString); // For Json.NET
                                                                    // T data = JsonSerializer.Deserialize<T>(jsonString); // For System.Text.Json

                return data;
            }
        }

        protected async Task FetchAssistant()
        {
            try
            {
                rassistant data = await GetDataAsync<rassistant>();

                // Use the retrieved data as needed
            }
            catch (HttpRequestException httpEx)
            {
                // Handle any HTTP errors here
            }
            catch (Exception ex)
            {
                // Handle other potential errors (e.g., serialization issues)
            }
        }
        public rassistant()
        {
            InitializeComponent();
            var res = FetchAssistant();
            txtAssistantId.Name = this.id;
        }

        private void btnAssistantSendRequest_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    public class Metadata
    {
    }

    public class Tool
    {
        public string type { get; set; }
    }

    ///Thread-object which represents a conversation using the assistant with a certain id
    ///

    public class AssistantThreadRequest
    {

    }
    public class AssistantThreadResponse
    {
        public string id { get; set; }
        public string _object { get; set; }
        public int created_at { get; set; }
        public Metadata metadata { get; set; }
    }

    /* public class Metadata
     {
     }
    */

}
