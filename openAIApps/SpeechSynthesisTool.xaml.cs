using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TTS;

namespace openAIApps
{
    /// <summary>
    /// Interaction logic for SpeechSynthesisTool.xaml
    /// </summary>
    public partial class SpeechSynthesisTool : Window
    {
        const string savepath = "D:\\Users\\frode\\Documents\\openapi\\speech.txt";
        public static void SaveControlDataToFile(StackPanel sp)
        {
            using (StreamWriter writer = new StreamWriter(savepath))
            {
                foreach(var child in sp.Children)
                {
                    if(child is ComboBox)
                    {
                        if(((ComboBox)child).SelectedValue == null)
                        {
                            writer.Close();
                            MessageBox.Show("A value cannot be null. Please choose a value from the dropdown to save", "Null value", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        writer.WriteLine(((ComboBox)child).SelectedValue);
                    }
                    else if(child is CheckBox)
                    {
                        if(((CheckBox)child).IsChecked == true)
                            writer.WriteLine(1);
                        else
                            writer.WriteLine(0);
                    }
                }
            }
            
        }
        public static void GetSavedSpeechData(StackPanel sp)
        {

            try 
            { 
                using (StreamReader reader = new StreamReader(savepath))
                {
                    foreach (var child in sp.Children)
                    {
                        if (child is ComboBox)
                        {
                            ((ComboBox)child).SelectedValue = reader.ReadLine();
                        }
                        else if (child is CheckBox)
                        {
                            if(reader.ReadLine() == "1")
                                ((CheckBox)child).IsChecked = true;
                            else
                                ((CheckBox)child).IsChecked = false;
                        }
                    }
                }
            }
            catch (IOException err) 
            {
                switch (err)
                {
                    case FileNotFoundException:
                        MessageBox.Show("File does not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    default:
                        MessageBox.Show(err.Message, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
        }
        /// <summary>
        /// Just set all controls to index=0, and set available voices based on these three
        /// </summary>
        public void Init()
        {
           //Get all unique languages from neuealvoices-list
            this.lbTTSLanguages.ItemsSource = SpeechSynthesis.VoiceDescription.GetUniqueLanguages();
            this.lbTTSLanguages.SelectedIndex = 0;
            //get all locales based on selected index = 0 
            this.lbTTSLocale.ItemsSource = SpeechSynthesis.VoiceDescription.GetUniqueLocales();
            this.lbTTSLocale.SelectedIndex = 0;
            //get all genders based on gender
            this.lbTTSGender.ItemsSource = SpeechSynthesis.VoiceDescription.GetUniqueGenders();
            this.lbTTSGender.SelectedIndex = 0;

            //select all available voices based on the above indixes
            this.lbTTVoices.ItemsSource = SpeechSynthesis.VoiceDescription.GetDisplayNames(lbTTSLanguages.SelectedValue.ToString(), lbTTSLocale.SelectedValue.ToString(), lbTTSGender.SelectedValue.ToString());

            //when finished adding values we can get saved values from file if it exists
            GetSavedSpeechData(SPData);
        }
        public SpeechSynthesisTool()
        {
            InitializeComponent();
            Init();
        }

        private void lbTTS_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbTTSLanguages.ItemsSource != null && lbTTSLocale.ItemsSource != null && lbTTSGender.ItemsSource != null)
            {
                //update Locale and gender
                if(sender == lbTTSLanguages)
                {
                    lbTTSLocale.ItemsSource = SpeechSynthesis.VoiceDescription.GetUniqueLocales(lbTTSLanguages.SelectedValue.ToString());
                    if(lbTTSLocale.SelectedValue != null)
                        lbTTSGender.ItemsSource = SpeechSynthesis.VoiceDescription.GetUniqueGenders(lbTTSLanguages.SelectedValue.ToString(), lbTTSLocale.SelectedValue.ToString());
                }
                //update Locale and gender
                else if (sender == lbTTSLocale) 
                {
                    lbTTSGender.ItemsSource = SpeechSynthesis.VoiceDescription.GetUniqueGenders(lbTTSLanguages.SelectedValue.ToString());
                    if(lbTTSLocale.SelectedValue != null)
                        lbTTSGender.ItemsSource = SpeechSynthesis.VoiceDescription.GetUniqueGenders(lbTTSLanguages.SelectedValue.ToString(), lbTTSLocale.SelectedValue.ToString());
                }
                else if(sender == lbTTSGender) 
                { 
                    //lbTTSGender.SelectedIndex = 0;
                }
                if(lbTTSLocale.SelectedValue != null && lbTTSGender != null)
                    this.lbTTVoices.ItemsSource = SpeechSynthesis.VoiceDescription.GetDisplayNames(lbTTSLanguages.SelectedValue.ToString(), lbTTSLocale.SelectedValue.ToString(), lbTTSGender.SelectedValue.ToString());
            }
        }

        private void chTTSUse_Checked(object sender, RoutedEventArgs e)
        {
            SpeechSynthesis.TTSUse = true;
        }

        private void chTTSUse_Unchecked(object sender, RoutedEventArgs e)
        {
            SpeechSynthesis.TTSUse = false;
        }

        private void lbTTVoices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(lbTTVoices.SelectedValue != null)
                SpeechSynthesis.TTSChosenVoice = this.lbTTVoices.SelectedValue.ToString();
        }
        /// <summary>
        /// save settings to file in json-format. Takes/saves values from SpeecSynthesis.VoiceDescription
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveControlDataToFile(SPData);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveControlDataToFile(SPData);
        }

    }
}
