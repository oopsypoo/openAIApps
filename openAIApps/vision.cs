using Azure;
using Azure.AI.OpenAI;
using Microsoft.Win32;
using System;
using System.IO;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Dalle;

/// <summary>
/// Summary description for Vision
/// Documentation: https://platform.openai.com/docs/guides/vision
/// I changed to using Azure-library. Seems like I got some problems with chat-format when using the OpenAPI amnually.
/// Irritating but ok. This works too...
/// </summary>
public class Vision
{
    const string VisionModel = "gpt-4o";
    const string defaultpath_pics = "D:\\Users\\frode\\Documents\\openapi\\pics\\";
    //const string defaultpath_pics = "D:\\Users\\%UserProfile%\\Pictures\\";
   // const string EndPointVision = "https://api.openai.com/v1/chat/completions";
   // public static RequestVision rxVision = new RequestVision(null);

    public static string ImageFileName { get; set; }

    public static ChatCompletionsOptions chatCompletionsOptions {  get; set; }

    public static void InitChatVision(string prompt)
    {
        using Stream ImageStream = File.OpenRead(ImageFileName);
        chatCompletionsOptions = new()
        {
            DeploymentName = VisionModel,
            Messages =
            {
                new ChatRequestUserMessage(
                new ChatMessageTextContentItem(prompt),
                new ChatMessageImageContentItem(ImageStream, $"image/{Path.GetExtension(Vision.ImageFileName)}", ChatMessageImageDetailLevel.High)),
            }
        };
    }
    public static async Task<ChatChoice> ResponseChat(string key)
    {
        var client = new OpenAIClient(key);
        Response<ChatCompletions> chatResponse = await client.GetChatCompletionsAsync(chatCompletionsOptions);
        ChatChoice choice = chatResponse.Value.Choices[0];
        if (choice.FinishReason == CompletionsFinishReason.Stopped)
        {
            return choice;
        }
        return null;
    }
    
    public Vision()
    {
        //
        // TODO: Add constructor logic here
        //
        
    }
    public static string GetImageFileName()
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "Image Files(*.jpg; *.jpeg; *.webp; *.png)|*.jpg; *.jpeg; *.webp; *.png";
        ofd.FilterIndex = 1;
        ofd.Multiselect = false;
        ofd.InitialDirectory = defaultpath_pics;

        if (ofd.ShowDialog() == true)
        {
            //return full path and filename
            return ofd.FileName;
        }
        return null;
    }

    // Create class based on same structure as "dalle" and "requestImage".
 /*   public class RequestVision
    {
        public string model { get; set; }
        public Message[] messages { get; set; }

        public int max_tokens { get; set; }

        public RequestVision(string visionModel)
        {
            if (visionModel != null)
                model = visionModel;
            else
                model = VisionModel;
            messages = new Message[]
            {
                new Message
                {
                    role = "user",
                    //let's just create what we need
                    content = new Content[2]
                    {
                        new Content
                        {
                            type = "text",
                            text = "...",
                            image_url = null
                        },
                        new Content
                        {
                            type = "image_url",
                            text = "what's in the picture",
                            image_url = new Image_Url
                            {
                                url = "...",
                                detail = "high"
                            }
                        }
                    }
                }
            };
            max_tokens = 300;
        }

        public class Message
        {
            public string role { get; set; }
            public Content[] content { get; set; }
        }

        public class Content
        {
            public string type { get; set; }
            public string text { get; set; }
            
            public Image_Url image_url { get; set; }
        }
        
        public class Image_Url
        {
            public string url { get; set; }
            public string detail { get; set; }
        }


        public async Task<HttpResponseMessage> PostFile(string key)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var jsonString = JsonSerializer.Serialize<Vision.RequestVision>(rxVision);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                var res = await httpClient.PostAsync(EndPointVision, content);
                return res;

            }
        }
    }
    */

}
