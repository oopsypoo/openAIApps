using Microsoft.CognitiveServices.Speech;
using Microsoft.Win32;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;


/// <summary>
/// I should set up a #ifdef on the edit and image variations. They should not be visible as long as it only works on DALL-E 2 and not DALL-E 3
/// </summary>

public class Dalle
{
    /* 
     * There is different behaviour and capabilities depending on the DALL-E version. I should set this up so that 
     * it's reflected in the application.
     * 3 => no edit and image variations of pics should be available
     * < 3 => edit and image variations is available
    */
    
    public const string url_image_generations = "https://api.openai.com/v1/images/generations";
    public const string url_image_edit = "https://api.openai.com/v1/images/edits";
    public const string url_image_variations = "https://api.openai.com/v1/images/variations";
    const string savepath_pics = "D:\\Users\\frode\\Documents\\openapi\\pics\\";
    public static OptionsImage optImages = new OptionsImage();
    public static requestImage rxImages = new requestImage();
    public static requestImageVariation rxImagesVariation = new requestImageVariation();
    public static requestImageEdit rxImagesEdit = new requestImageEdit();
    public static responseImage resource = new responseImage();
    public static TempWindow[] temp;
    public static SaveThisImage[] saveButton;
    
    /// <summary>
    /// options for the image request. User can set this(in practice it will be constant)
    /// </summary>
    public class OptionsImage
    {
        public int noImages;
        public string csize;
        
        public string requestURL;
        //public string responseFormat;

        //for DALL-E you can use these sizes
#if DALLE_VERSION3  //this preprocessor symbol is defined under the project-properties, Build->General->Conditional compilation symbols. 
                    //Cannot request for imagevariations or image edits for this version. At this point...
        public readonly int[] optImages = { 1 };
        public string Quality;
        /// <summary>
        /// these values are for DALL-E 3
        /// </summary>
        //public readonly string[] optSize = { "1024x1024", "1024x1792", "1792x1024" };
        ///these are the sizes supported by gpt-image-1
        public readonly string[] optSize = { "1024x1024", "1024x1536", "1536x1024" }; //square, portrait, landscape
        //public readonly string[] optQuality = { "standard", "hd" };
        public readonly string[] optQuality = { "low", "medium", "high", "auto" };
#else
        //if DALLE_VERSION is < 3
        public readonly string[] optSize = { "256x256", "512x512", "1024x1024"};
        public readonly int[] optImages = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
#endif


        public OptionsImage()
        {
            //set default values
            noImages = 1;
            requestURL = url_image_generations;
            csize = optSize[0];
#if DALLE_VERSION3
            Quality = optQuality[0];
#endif
            //  responseFormat = "url";
        }

    }
    /// <summary>
    /// The default model is DALL-E 3. If I want to choose between these two I have to make to many changes. Variations and Edit will only work with DALL-E 2.
    /// We have to recompile if we want to do this or make use of this.
    /// </summary>
    public class requestImage
    {
        //to use version 3 we haveto set the model name
        public string model { get; set; }
        public string prompt { get; set; }
        public int n { get; set; }
        public string output_format { get; set; }
        public string size { get; set; }
        /// to make original code from DALLE3 work i have to set this to 'url'. Cannot use with gpt-image-1
        //public string response_format { get; set; }
        public string quality { get; set; }
        //public string response_format;
        /// <summary>
        /// for the moment, just set some defaults for testing
        /// </summary>
        public requestImage()
        {
            //set default model
            //model = "dall-e-3";
            model = "gpt-image-1";
            //two qualitities: standard and hd.
            quality = optImages.Quality;
            //set format
            output_format = "jpeg";
            n = optImages.noImages;
            size = optImages.csize;
            /// to make original code from DALLE3 work i have to set this to 'url'. Cannot use with gpt-image-1
            //response_format = "url";
            //  response_format = optImages.responseFormat;
        }
        public async Task<HttpResponseMessage> PostFile(string key)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                httpClient.DefaultRequestHeaders.Add("OpenAI-Beta", "image-generations-2023-10-01"); // ✅ important for gpt-image-1
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                
                var jsonString = JsonSerializer.Serialize<Dalle.requestImage>(rxImages);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                var res = await httpClient.PostAsync(url_image_generations, content);
                return res;

            }
        }
    }
    public static string GetImageFileName()
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp; *.png)|*.jpg; *.jpeg; *.gif; *.bmp; *.png";
        ofd.FilterIndex = 1;
        ofd.Multiselect = false;
        ofd.InitialDirectory = savepath_pics;

        if (ofd.ShowDialog() == true)
        {
            //return full path and filename
            return ofd.FileName;
        }
        return null;
    }
    /// <summary>
    /// ImageVariation..not sure if this will work with gpt-image-1(DALL-E 3) anymore..do some research later
    /// </summary>
    public class requestImageVariation
    {
        public string image { get; set; }
        public int n { get; set; }
        public string size { get; set; }
        
        public requestImageVariation()
        {
            n = optImages.noImages;
            size = optImages.csize;
        }
        public async Task<HttpResponseMessage> PostFile(string key)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                
                using (var content = new MultipartFormDataContent())
                {
                    using (var fileStream = new FileStream(savepath_pics + image, FileMode.Open, FileAccess.Read))
                    {
                        content.Add(new StreamContent(fileStream), "image", image);
                        content.Add(new StringContent(n.ToString()), "n");
                        content.Add(new StringContent(n.ToString()), "output_format");
                        content.Add(new StringContent(size), "size");
                        var res = await httpClient.PostAsync(url_image_variations, content);
                        return res;
                    }
                }
            }
        }
    }
    /// <summary>
    /// ImageEdit. this needs to be upgraded. Do not use yet.
    /// </summary>
    public class requestImageEdit
    {
        public string image { get; set; }
        public string mask { get; set; }
        public string prompt { get; set; }
        public int n { get; set; }
        //to make original code from DALLE3 work i have to set this to 'url'
        public string response_format { get; set; }
        public string size { get; set; }

        public requestImageEdit()
        {
            n = optImages.noImages;
            size = optImages.csize;
            image = null;
            mask = null;
            prompt = null;
        }
        public async Task<HttpResponseMessage> PostFile(string key)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                using (var content = new MultipartFormDataContent())
                {
                    FileStream fileStreamImage = null;
                    FileStream fileStreamMask = null;

                    if (!string.IsNullOrEmpty(image))
                    { //this one cannot be null
                        fileStreamImage = new FileStream(savepath_pics + image, FileMode.Open, FileAccess.Read);
                        content.Add(new StreamContent(fileStreamImage), "image", image);
                    }
                    else
                        return null;
                    if (!string.IsNullOrEmpty(mask))
                    {
                        fileStreamMask = new FileStream(savepath_pics + mask, FileMode.Open, FileAccess.Read);
                        content.Add(new StreamContent(fileStreamMask), "mask", mask);
                    }
                    content.Add(new StringContent(prompt), "prompt");
                    content.Add(new StringContent(n.ToString()), "n");
                    content.Add(new StringContent(size), "size");

                    var res = await httpClient.PostAsync(url_image_edit, content);
                    return res;
                }
            }
        }
    }
    public class responseImage
    {
        public int created { get; set; }
        public imageData[] data { get; set; }
        /// <summary>
        /// if I want to convert from base64 to BitmapImage using json-data and not url(url is for DALL-E 3/2)
        /// </summary>
        public BitmapImage Base64ToBitmap(string base64)
        {
            byte[] imageBytes = Convert.FromBase64String(base64);
            using (var ms = new MemoryStream(imageBytes))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze(); // important if using in WPF UI thread
                return bitmap;
            }
        }
 
        public void DrawImages()
        {
            int i = 0, nImages = data.Length;
            temp = new TempWindow[nImages];
            saveButton = new SaveThisImage[nImages];
            while (i < nImages)
            {
                /*if (data[i].url != null)
                {
                    temp[i] = new TempWindow(i);
                    //this.AddChild(temp);
                    temp[i].index = i;
                    temp[i].bi = new BitmapImage();
                    //var bitmapImage = new BitmapImage();
                    temp[i].bi.BeginInit();
                    temp[i].bi.UriSource = new Uri(data[i].url);
                    //temp[i].bi = Base64ToBitmap(data[i].url);
                    temp[i].bi.EndInit();

                    ImageBrush myBackground = new ImageBrush();
                    myBackground.ImageSource = temp[i].bi;
                    myBackground.Stretch = Stretch.Uniform;
                    temp[i].Background = myBackground;

                    temp[i].Visibility = Visibility.Visible;
                    temp[i].ShowButton();
                }*/
                if (data[i].b64_json != null)
                {                     
                    temp[i] = new TempWindow(i);
                    //this.AddChild(temp);
                    temp[i].index = i;
                    temp[i].bi = Base64ToBitmap(data[i].b64_json);
                    ImageBrush myBackground = new ImageBrush();
                    myBackground.ImageSource = temp[i].bi;
                    myBackground.Stretch = Stretch.Uniform;
                    temp[i].Background = myBackground;
                    temp[i].Visibility = Visibility.Visible;
                    temp[i].ShowButton();
                }
                i++;
            }
        }
    }

    public class imageData
    {
        public string b64_json { get; set; }
    }
    public class SaveThisImage : Button
    {

        public int index { get; set; }
        protected override void OnClick()
        {

            SaveFileDialog save = new SaveFileDialog();
            save.Title = "Save picture as ";
            save.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp; *.png)|*.jpg; *.jpeg; *.gif; *.bmp; *.png";
            save.FileName = resource.created + "-" + index + ".jpg";
            save.CheckPathExists = true;
            save.InitialDirectory = savepath_pics;
            if (resource != null)
            {
                if (save.ShowDialog() == true)
                {
                    PngBitmapEncoder png = new PngBitmapEncoder();
                    png.Frames.Add(BitmapFrame.Create(temp[index].bi));
                    using (Stream stm = File.Create(save.FileName))
                    {
                        png.Save(stm);
                    }
                }
            }
        }
        public SaveThisImage()
        {
            this.AddText("Save Image");
            this.Name = "Button" + index;

        }
    }
    public class TempWindow : Window
    {
        public int index { get; set; }
        public BitmapImage bi;
        protected override void OnGotFocus(RoutedEventArgs e)///why does this not trigger?
        {
            MessageBox.Show("We got focus");
            base.OnGotFocus(e);
        }
        //Apparently height and widt are the same, but maybe it will change in the future
        //Therefore we might as well get both height and width.
        public double GetStringHeight(string str_size)
        {
            var i = str_size.IndexOf('x');

            var ss = str_size.Substring(i + 1); //everything after 'x'(=> i+1)
            return Convert.ToInt32(ss);
        }
        public double GetStringWidth(string str_size)
        {
            var i = str_size.IndexOf('x');
            var ss = str_size.Substring(0, i);
            return Convert.ToInt32(ss);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

        }
        public void ShowButton()
        {
            saveButton[index].Visibility = Visibility.Visible;

        }
        public TempWindow(int i)
        {
            saveButton[i] = new SaveThisImage();
            this.AddChild(saveButton[i]);
            //remember to get right size of window from string in combobox
            this.Height = this.GetStringHeight(rxImages.size);
            this.Width = this.GetStringWidth(rxImages.size);
            saveButton[i].Name = "btnSaveImage" + index;
            saveButton[i].index = i;
            saveButton[i].Height = 30;
            saveButton[i].Width = 200;
            saveButton[i].Foreground = Brushes.Gray;
            saveButton[i].Background = Brushes.White;
            saveButton[i].VerticalAlignment = VerticalAlignment.Bottom;
            saveButton[i].HorizontalAlignment = HorizontalAlignment.Center;
            saveButton[i].IsEnabled = true;
        }
    }
    
}
