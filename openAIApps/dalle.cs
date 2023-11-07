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


public class Dalle
{
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
        public readonly int[] optImages = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        public readonly string[] optSize = { "256x256", "512x512", "1024x1024" };
        public OptionsImage()
        {
            //set default values
            noImages = 1;
            requestURL = url_image_generations;
            csize = optSize[0];
            //  responseFormat = "url";
        }

    }

    public class requestImage
    {
        public string prompt { get; set; }
        public int n { get; set; }
        public string size { get; set; }


        //public string response_format;
        /// <summary>
        /// for the moment, just set some defaults for testing
        /// </summary>
        public requestImage()
        {
            n = optImages.noImages;
            size = optImages.csize;
            //  response_format = optImages.responseFormat;
        }
        public async Task<HttpResponseMessage> PostFile(string key)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
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
                        content.Add(new StringContent(size), "size");
                        var res = await httpClient.PostAsync(url_image_variations, content);
                        return res;
                    }
                }
            }
        }
    }
    public class requestImageEdit
    {
        public string image { get; set; }
        public string mask { get; set; }
        public string prompt { get; set; }
        public int n { get; set; }
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

        public void DrawImages()
        {
            int i = 0, nImages = data.Length;
            temp = new TempWindow[nImages];
            saveButton = new SaveThisImage[nImages];
            while (i < nImages)
            {
                if (data[i].url != null)
                {
                    temp[i] = new TempWindow(i);
                    //this.AddChild(temp);
                    temp[i].index = i;
                    temp[i].bi = new BitmapImage();
                    //var bitmapImage = new BitmapImage();
                    temp[i].bi.BeginInit();
                    temp[i].bi.UriSource = new Uri(data[i].url);
                    temp[i].bi.EndInit();

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
        public string url { get; set; }
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
