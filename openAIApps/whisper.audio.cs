using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using Microsoft.Win32;
using NAudio.MediaFoundation;
using NAudio.Wave;
using System;
using System.IO;
using System.Media;
using System.Timers;
using System.Windows;


namespace whisper.AudioTools
{
    ///let's test the MediaToolkit
    ///

    /// <summary>
    /// Records sounds. Helper class for this module. Mostly for testing, but can be handly in any situation.
    /// At the moment only wav files are handled. Easiest. The filter si for future reference
    /// </summary>
    public class AudioTools : IDisposable
    {
        /// <summary>
        /// I'm using some messageboxes in the class. Should take that away. Only in main 
        /// </summary>
        private WaveInEvent waveIn = null;
        //private WaveOutEvent waveOut;
        private WaveOutEvent waveOutDevice; //with the waveout oabject it's easy to pause and spooling

        private WaveFileWriter writer;
        private Timer progressTimer;
        private AudioFileReader audioFileReader = null;
        public Metadata InMetadata = null;
        private double audioLengthMS;
        public event EventHandler<double> ProgressChanged;
        //get metadata from current file (filename_full)
        public Metadata OutMetadata = null;
        public string filename;
        public string filename_full = "";
        ///
        public event EventHandler PlaybackStarted;
        public event EventHandler PlaybackPaused;
        public event EventHandler PlaybackStopped;



        /// <summary>
        /// There's no quick solution for playing wav-files using the NAudio-library. Either I have to play through it until the end using threading
        /// or I have to use the "fire-and-forget" solution(https://markheath.net/post/fire-and-forget-audio-playback-with). Which is not any better.
        /// There are problems with reinitializing/disposing and blah blah.
        /// Therefore I'm using the windows.media library for playing wav-files.
        ///windows_media_resource tells us if we want to use the internal SoundPlayer or not
        /// </summary>
        private bool windows_media_resource = false;
        private SoundPlayer wavPlayer = null;
        /// <summary>
        /// Saves to file, taking initial directory as a param. It does not write anyting. Only creates a file, ready for writing.
        /// </summary>
        /// <param name="initial_directory">Initial directory to open dialog-box. Can be null</param>
        /// <param name="extension">extension of the filename </param>
        public string SaveToFile(string initial_directory, string extension)
        {
            SaveFileDialog sfd = new();
            sfd.InitialDirectory = initial_directory;
            //start with this. Default filename
            sfd.Title = "Create a filename";
            sfd.AddExtension = true;
            sfd.DefaultExt = extension;
            //sfd.Filter = "Sound Files(*.wav; *.mp3;)|*.wav; *.mp3";
            sfd.Filter = extension + " Files(*." + extension + ";)|*." + extension + ";";
            if (string.IsNullOrEmpty(filename))
            {
                sfd.FileName = "sample." + extension;
            }
            else
            {
                sfd.FileName = Path.GetFileNameWithoutExtension(filename);
            }
            if (sfd.ShowDialog() == true)
            {
                filename = sfd.SafeFileName;
                filename_full = sfd.FileName;
                File.Create(filename_full).Close();

                return filename_full;
            }
            return null;
        }
        /// <summary>
        /// the Metaobj is supposed to be a glkobal object that you want to set. Filename should be fullpath and name
        /// </summary>
        /// <param name="Metaobj"></param>
        /// <param name="filename"></param>
        public void SetMetaData(Metadata Metaobj, string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return;
            }
            else
            {
                var inputFile = new MediaFile { Filename = filename };
                // Initialize a new engine instance
                var engine = new Engine();
                engine.GetMetadata(inputFile);
                if (Metaobj == null)
                {
                    this.InMetadata = inputFile.Metadata;
                }
                else
                {
                    Metaobj = inputFile.Metadata;
                }
            }
        }
        /// <summary>
        /// Records and saves to the given file filename as a wav-file
        /// </summary>
        /// <param name="fileName"></param>
        public void StartRecording(string fileName)
        {
            waveIn = new WaveInEvent();
            waveIn.WaveFormat = new WaveFormat(44100, WaveIn.GetCapabilities(0).Channels);
            waveIn.DataAvailable += WaveIn_DataAvailable;

            writer = new WaveFileWriter(fileName, waveIn.WaveFormat);

            waveIn.StartRecording();
        }
        public bool ConvertWavFile(string wav_file, string savefile)
        {
            if (string.IsNullOrEmpty(wav_file) || string.IsNullOrEmpty(savefile))
            {
                MessageBox.Show("Missing full path of filename to convert", "Missing file", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            else
            {
                var inputFile = new MediaFile { Filename = wav_file };

                // Initialize a new engine instance
                var engine = new Engine();
                // Convert the WAV file to WebM format using the engine

                var outputOptions = new ConversionOptions { AudioSampleRate = AudioSampleRate.Default };
                var outputMedia = new MediaFile { Filename = savefile };
                engine.Convert(inputFile, outputMedia, outputOptions);
                engine.GetMetadata(outputMedia);
                OutMetadata = outputMedia.Metadata;
            }
            return true;
        }
        /// <summary>
        /// converts a wav file to MP3, AAC or WMA format. Uses the current selected file. This is mainly to make the upload smaller
        /// </summary>
        /// <param name="path"></param>
        public bool ConvertWav(string wav_file, string savefile)
        {
            if (string.IsNullOrEmpty(wav_file) || string.IsNullOrEmpty(savefile))
            {
                MessageBox.Show("Missing full path of filename to convert", "Missing file", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            else
            {

                MediaFoundationApi.Startup();
                using (var reader = new WaveFileReader(wav_file))
                {
                    try
                    {
                        var extension = Path.GetExtension(savefile).ToLower();
                        switch (extension)
                        {
                            case ".mp3":
                                MediaFoundationEncoder.EncodeToMp3(reader, savefile);
                                break;
                            case ".aac":
                                MediaFoundationEncoder.EncodeToAac(reader, savefile);
                                break;
                            case ".wma":
                                MediaFoundationEncoder.EncodeToWma(reader, savefile);
                                break;
                            default:
                                MessageBox.Show(extension + " is not supported. Only files of type \"mp3\", \"aac\" or \"wma\" are supported", "Extension error", MessageBoxButton.OK, MessageBoxImage.Error);
                                throw new ArgumentException();

                        }
                        reader.Close();
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw (ex);
                    }
                }
            }
            return true;
        }
        public void StopRecording()
        {
            waveIn.StopRecording();
            waveIn.Dispose();
            waveIn = null;
            writer.Close();
            writer.Dispose();
            writer = null;
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            writer.Write(e.Buffer, 0, e.BytesRecorded);
        }
        public void PlayFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found!", filePath);
            }


            waveOutDevice = new WaveOutEvent();
            if (Path.GetExtension(filePath).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            {
                this.windows_media_resource = true;
                wavPlayer = new SoundPlayer(filePath);
                wavPlayer.Load();
                wavPlayer.Play();
                ///This is the only code that I can make work for wav-files using NAudio. The problem is Thread.Sleep. I can't stop the playback. Not easily at least
                ///Therefore I'm using the windows.media library to play this. 
                /*    using (var waveFileReader = new WaveFileReader(filePath))
                    {
                        waveOutDevice.Init(waveFileReader);
                        waveOutDevice.Play();
                        // Subscribe to the PlaybackStopped event
                        while(_waveOutEvent.PlaybackState == PlaybackState.Playing)
                        {
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                */
            }
            else
            {
                this.windows_media_resource = false;

                audioFileReader = new AudioFileReader(filePath);
                waveOutDevice.Init(audioFileReader);
                waveOutDevice.Play();

                progressTimer = new Timer(1); // set timer interval to 1 msecond
                progressTimer.Elapsed += OnProgressTimerTick;
                progressTimer.Start();
                // Subscribe to the PlaybackStopped event
                waveOutDevice.PlaybackStopped += OnPlaybackStopped;

            }
        }
        private void OnProgressTimerTick(object sender, ElapsedEventArgs e)
        {
            audioLengthMS = audioFileReader.CurrentTime.TotalMilliseconds;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressChanged?.Invoke(this, audioLengthMS);
            });
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Dispose the WaveOutEvent object to release system resources
            waveOutDevice?.Stop();
            waveOutDevice?.Dispose();
            progressTimer?.Stop();
            progressTimer?.Dispose();
        }
        public void StopPlaying()
        {
            switch (windows_media_resource)
            {
                case true:
                    if (wavPlayer != null)
                        wavPlayer.Stop();
                    break;
                case false:
                    if (waveOutDevice != null)
                    {
                        waveOutDevice?.Stop();
                    }
                    break;
            }
            windows_media_resource = false;
        }
        public void PausePlaying(ref bool Paused)
        {
            if (waveOutDevice != null)
            {
                if (progressTimer.Enabled == true)
                {
                    waveOutDevice.Pause();
                    progressTimer.Stop();
                    Paused = true;
                }
                else
                {
                    waveOutDevice.Play();
                    progressTimer.Start();
                    Paused = false;
                }
            }
        }
        public void Dispose()
        {
            if (wavPlayer != null)
            {
                wavPlayer.Stop();
                wavPlayer = null;
            }
            if (waveOutDevice != null)
            {
                waveOutDevice?.Stop();
                waveOutDevice = null;
            }
            if (audioFileReader != null)
            {
                audioFileReader.Close();
                //audioFileReader.Dispose();
            }
            //this.Dispose();
        }
        public void Start()
        {
            waveOutDevice?.Play();
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
        }
        public void Pause()
        {
            waveOutDevice?.Pause();
            progressTimer?.Stop();
            PlaybackPaused?.Invoke(this, EventArgs.Empty);
        }
        public void Stop()
        {
            waveOutDevice.Pause();
            audioFileReader.Close();
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>
        /// Spools forwards or bakcwards according to parameter milliseconds
        /// </summary>
        /// <param name="milliseconds"></param>
        public void Spool(int milliseconds)
        {
            //just check
            if (waveOutDevice.PlaybackState != PlaybackState.Paused)
            {
                waveOutDevice.Pause();
                progressTimer?.Stop();
            }
            TimeSpan newPosition = TimeSpan.FromMilliseconds(milliseconds);
            audioFileReader.CurrentTime = newPosition;
            waveOutDevice.Play();
            progressTimer.Start();
        }

    }

}