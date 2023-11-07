using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;


namespace whisper
{

    public class Whisper
    {
        //public static AudioTools AuxRecord = new AudioTools();
        public static RequestWhisper RxWhisper = new RequestWhisper();
        public static OptionsWhisper WOptions = new OptionsWhisper();
        public static ResponseWhisper ResWhisper = new ResponseWhisper();
        const string url_transcriptions = "https://api.openai.com/v1/audio/transcriptions";
        const string url_translations = "https://api.openai.com/v1/audio/translations";
        
        const string whisper_model = "whisper-1";
        public static string full_audiofilename = "";

        public static readonly string[] WhisperEndpoints =
        {
            url_transcriptions,
            url_translations
        };
        public static string GetAudioFileName(string initial_directory)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            //ofd.Filter = "Sound Files(*.wav; *.mp3; *.aac; *.wma)|*.wav; *.mp3; *.aac; *.wma";//apparently aac and wma is not supported.. but ok
            ofd.Filter = "Sound Files(*.wav; *.mp3; *.mp4; *.mpeg; *.webm; *.m4a)|*.wav; *.mp3; *.mp4; *.mpeg; *.webm; *.m4a";
            ofd.FilterIndex = 1;
            ofd.Multiselect = false;
            ofd.InitialDirectory = initial_directory;
            ofd.Title = "Open file for Soeach to Text";

            if (ofd.ShowDialog() == true)
            {
                //return full path and filename
                RxWhisper.file = ofd.SafeFileName;
                return ofd.FileName;
            }
            return null;
        }
        /// <summary>
        /// options for the requestWhisper class
        /// </summary>
        public class OptionsWhisper
        {
            /// <summary>
            /// soeach to text type. Default is set to transcriptions
            /// </summary>
            public string STT_Type { get; set; }
            /// <summary>
            /// language of the speach to text that we are sending
            /// </summary>
            public string STT_language { get; set; }
            public string STT_audiofile { get; set; }
            public OptionsWhisper()
            {
                STT_Type = url_transcriptions;
                STT_language = "en";
                STT_audiofile = "";
            }
        }
        public class RequestWhisper
        {
            public string file { get; set; }
            public string model { get; set; }
            public string prompt { get; set; }
            public string response_format { get; set; }
            public int temperature { get; set; }
            /// <summary>
            /// language in ISO-639-1 format
            /// </summary>
            public string language { get; set; }
            public RequestWhisper()
            {
                //set defaults
                file = "";
                model = whisper_model;
                prompt = "";
                response_format = "json";
                temperature = 0;
                language = "en";
            }
            public async Task<HttpResponseMessage> PostFile(string key)
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                    using (var content = new MultipartFormDataContent())
                    {
                        using (var fileStream = new FileStream(WOptions.STT_audiofile, FileMode.Open, FileAccess.Read))
                        {
                            content.Add(new StreamContent(fileStream), "file", file);
                            content.Add(new StringContent(model), "model");
                            content.Add(new StringContent(prompt), "prompt");
                            content.Add(new StringContent(prompt), "response_format");
                            content.Add(new StringContent(prompt), "temperature");
                            content.Add(new StringContent(prompt), "language");

                            var res = await httpClient.PostAsync(Whisper.WOptions.STT_Type, content);
                            return res;
                        }
                    }
                }
            }
        }

        public class ResponseWhisper
        {
            public string text { get; set; }
        }

    }
}