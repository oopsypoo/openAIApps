using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;



namespace TTS
{
    public static class SpeechSynthesis
    {
        static readonly string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
        static readonly string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
        public static string TTSChosenVoice = "en-SG-LunaNeural";
        public static bool TTSUse = false;
        public static List<VoiceDescription> NeuralVoices = new List<VoiceDescription>()
                                            {
                                            new VoiceDescription("en", "AU","en-AU-AnnetteNeural","Female"),
                                           new VoiceDescription("en", "AU","en-AU-CarlyNeural","Female"),
                                           new VoiceDescription("en", "AU","en-AU-CarlyNeural","Female"),
                                           new VoiceDescription("en", "AU","en-AU-DuncanNeural","Male"),
                                            new VoiceDescription("en", "AU","en-AU-ElsieNeural","Female"),
                                           new VoiceDescription("en",  "AU","en-AU-FreyaNeural","Female"),
                                           new VoiceDescription("en",  "AU","en-AU-JoanneNeural","Female"),
                                           new VoiceDescription("en",  "AU","en-AU-KenNeural","Male"),
                                            new VoiceDescription("en", "AU","en-AU-KimNeural","Female"),
                                            new VoiceDescription("en", "AU","en-AU-NatashaNeural","Female"),
                                            new VoiceDescription("en", "AU","en-AU-NeilNeural","Male"),
                                            new VoiceDescription("en", "AU","en-AU-TimNeural","Male"),
                                            new VoiceDescription("en", "AU","en-AU-TinaNeural","Female"),
                                            new VoiceDescription("en", "AU","en-AU-WilliamNeural","Male"),
                                            new VoiceDescription("en", "CA","en-CA-ClaraNeural","Female"),
                                            new VoiceDescription("en", "CA","en-CA-LiamNeural","Male"),
                                            new VoiceDescription("en", "GB","en-GB-AbbiNeural ", "Female"),
                                            new VoiceDescription("en", "GB","en-GB-AlfieNeural ", "Male"),
                                            new VoiceDescription("en", "GB","en-GB-BellaNeural ", "Female"),
                                            new VoiceDescription("en", "GB","en-GB-ElliotNeural ", "Male"),
                                            new VoiceDescription("en", "GB","en-GB-EthanNeural ", "Male"),
                                            new VoiceDescription("en", "GB","en-GB-HollieNeural ", "Female"),
                                            new VoiceDescription("en", "GB","en-GB-LibbyNeural ", "Female"),
                                            new VoiceDescription("en", "GB","en-GB-MaisieNeural", "Female, Child"),
                                            new VoiceDescription("en", "GB","en-GB-NoahNeural ", "Male"),
                                            new VoiceDescription("en", "GB","en-GB-OliverNeural ", "Male"),
                                            new VoiceDescription("en", "GB","en-GB-OliviaNeural ", "Female"),
                                            new VoiceDescription("en", "GB","en-GB-RyanNeural ", "Male"),
                                            new VoiceDescription("en", "GB","en-GB-SoniaNeural ", "Female"),
                                            new VoiceDescription("en", "GB","en-GB-ThomasNeural ", "Male"),
                                            new VoiceDescription("en", "HK","en-HK-SamNeural ", "Male"),
                                            new VoiceDescription("en", "HK","en-HK-YanNeural ", "Female"),
                                            new VoiceDescription("en", "IE","en-IE-ConnorNeural ", "Male"),
                                            new VoiceDescription("en", "IE","en-IE-EmilyNeural ", "Female"),
                                            new VoiceDescription("en", "IN","en-IN-NeerjaNeural ", "Female"),
                                            new VoiceDescription("en", "IN","en-IN-PrabhatNeural ", "Male"),
                                            new VoiceDescription("en", "KE","en-KE-AsiliaNeural ", "Female"),
                                            new VoiceDescription("en", "KE","en-KE-ChilembaNeural ", "Male"),
                                            new VoiceDescription("en", "NG","en-NG-AbeoNeural ", "Male"),
                                           new VoiceDescription("en",  "NG","en-NG-EzinneNeural ", "Female"),
                                            new VoiceDescription("en", "NZ","en-NZ-MitchellNeural ", "Male"),
                                           new VoiceDescription("en",  "NZ","en-NZ-MollyNeural ", "Female"),
                                           new VoiceDescription("en",  "PH","en-PH-JamesNeural ", "Male"),
                                           new VoiceDescription("en",  "PH","en-PH-RosaNeural ", "Female"),
                                           new VoiceDescription("en",  "SG","en-SG-LunaNeural ", "Female"),
                                            new VoiceDescription("en", "SG","en-SG-WayneNeural ", "Male"),
                                           new VoiceDescription("en",  "TZ","en-TZ-ElimuNeural ", "Male"),
                                           new VoiceDescription("en",  "TZ","en-TZ-ImaniNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-AIGenerate1Neural1 ", "Male"),
                                          new VoiceDescription("en",   "US","en-US-AIGenerate2Neural1 ", "Female"),
                                           new VoiceDescription("en", "US",  "en-US-AmberNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-AnaNeural", "Female, Child"),
                                            new VoiceDescription("en", "US","en-US-AriaNeural ", "Female"),
                                            new VoiceDescription("en", "US","en-US-AshleyNeural ", "Female"),
                                            new VoiceDescription("en", "US","en-US-BrandonNeural ", "Male"),
                                           new VoiceDescription("en",  "US","en-US-ChristopherNeural ", "Male"),
                                           new VoiceDescription("en",  "US","en-US-CoraNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-DavisNeural ", "Male"),
                                           new VoiceDescription("en",  "US","en-US-ElizabethNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-EricNeural ", "Male"),
                                           new VoiceDescription("en",  "US","en-US-GuyNeural ", "Male"),
                                           new VoiceDescription("en",  "US","en-US-JacobNeural ", "Male"),
                                           new VoiceDescription("en",  "US","en-US-JaneNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-JasonNeural ", "Male"),
                                           new VoiceDescription("en",  "US","en-US-JennyMultilingualNeural3 ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-JennyNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-MichelleNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-MonicaNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-NancyNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-RogerNeural ", "Male"),
                                           new VoiceDescription("en",  "US","en-US-SaraNeural ", "Female"),
                                           new VoiceDescription("en",  "US","en-US-SteffanNeural ", "Male"),
                                           new VoiceDescription("en",  "US","en-US-TonyNeural ", "Male"),
                                            new VoiceDescription("en", "ZA","en-ZA-LeahNeural ", "Female"),
                                           new VoiceDescription("en",  "ZA","en-ZA-LukeNeural ", "Male"),
                                            new VoiceDescription("nb", "NO","nb-NO-FinnNeural", "Male"),
                                            new VoiceDescription("nb", "NO","nb-NO-IselinNeural", "Female"),
                                            new VoiceDescription("nb", "NO","nb-NO-PernilleNeural", "Female"),
                                            new VoiceDescription("fil", "PH","fil-PH-AngeloNeural2", "Male"),
                                            new VoiceDescription("fil", "PH","fil-PH-BlessicaNeural2", "Female")


        };


        public class VoiceDescription
        {
            private string Language { get; set; }
            private string Locale { get; set; }
            private string DisplayName { get; set; }
            private string Gender { get; set; }


            public VoiceDescription(string language, string locale, string displayName, string gender)
            {
                Language = language;
                Locale = locale;
                DisplayName = displayName;
                Gender = gender;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public static List<string> GetUniqueLanguages()
            {
                List<string> uniqueLanguages = NeuralVoices.Select(v => v.Language).Distinct().ToList();
                return uniqueLanguages;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public static List<string> GetUniqueGenders()
            {
                List<string> distinctGenders = NeuralVoices
                                                    .Select(v => v.Gender)
                                                    .Distinct()
                                                    .ToList();

                return distinctGenders;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public static List<string> GetUniqueGenders(string language)
            {
                List<string> distinctGenders = NeuralVoices.Where(v => v.Language == language)
                                                    .Select(v => v.Gender)
                                                    .Distinct()
                                                    .ToList();

                return distinctGenders;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public static List<string> GetUniqueGenders(string language, string locale)
            {
                List<string> distinctGenders = NeuralVoices.Where(v => v.Language == language && v.Locale == locale)
                                                    .Select(v => v.Gender)
                                                    .Distinct()
                                                    .ToList();

                return distinctGenders;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public static List<string> GetUniqueLocales()
            {
                List<string> uniqueLocales = NeuralVoices.Select(v => v.Locale).Distinct().ToList();
                return uniqueLocales;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public static List<string> GetUniqueLocales(string language)
            {
                List<string> uniqueLocales = NeuralVoices.Where(v => v.Language == language).Select(v => v.Locale).Distinct().ToList();
                return uniqueLocales;
            }
            /// <summary>
            /// returns displaynames according to language a and locale/dialect
            /// </summary>
            /// <param name="language"></param>
            /// <param name="locale"></param>
            /// <returns></returns>
            public static List<string> GetDisplayNames(string language, string locale)
            {
                List<string> displayNames = NeuralVoices
                                                .Where(v => v.Language == language && v.Locale == locale)
                                                .Select(v => v.DisplayName)
                                                .ToList();

                return displayNames;
            }
            /// <summary>
            /// returns a list of displaynames with a certain language
            /// </summary>
            /// <param name="language"></param>
            /// <returns></returns>
            public static List<string> GetDisplayNames(string language)
            {
                List<string> displayNames = NeuralVoices
                                                .Where(v => v.Language == language)
                                                .Select(v => v.DisplayName)
                                                .ToList();

                return displayNames;
            }
            /// <summary>
            /// returns displayname according to parameters below. Language cannot be null
            /// </summary>
            /// <param name="lang"></param>
            /// <param name="loc"></param>
            /// <param name="gender"></param>
            /// <returns></returns>
            public static List<string> GetDisplayNames(string language, string locale, string gender)
            {
                List<string> displayNames = NeuralVoices
                    .Where(v => v.Language == language && v.Locale == locale && v.Gender == gender)
                    .Select(v => v.DisplayName)
                    .ToList();
                return displayNames;
            }
        }


        static void OutputSpeechSynthesisResult(SpeechSynthesisResult speechSynthesisResult, string text)
        {
            switch (speechSynthesisResult.Reason)
            {
                case ResultReason.SynthesizingAudioCompleted:
                    //MessageBox.Show($"Speech synthesized for text: [{text}]");
                    break;
                case ResultReason.Canceled:
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                    MessageBox.Show($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        MessageBox.Show($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        MessageBox.Show($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                        MessageBox.Show($"CANCELED: Did you set the speech resource key and region values?");
                    }
                    break;
                default:
                    break;
            }
        }
        public static async Task TTSAsync(string input)
        {
            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            // The language of the voice that speaks.

            speechConfig.SpeechSynthesisVoiceName = TTSChosenVoice;

            using (var speechSynthesizer = new SpeechSynthesizer(speechConfig))
            {
                var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(input);
                OutputSpeechSynthesisResult(speechSynthesisResult, input);
            }
        }
    }
}
