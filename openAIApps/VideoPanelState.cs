using openAIApps.Data;
using System.Collections.ObjectModel;
using static openAIApps.VideoClient;

namespace openAIApps
{
    public class VideoPanelState : ObservableObject
    {
        public ObservableCollection<VideoProviderOption> AvailableProviders { get; } = new();
        public ObservableCollection<VideoModelOption> AvailableModels { get; } = new();
        public ObservableCollection<string> AvailableLengths { get; } = new();
        public ObservableCollection<string> AvailableSizes { get; } = new();
        public ObservableCollection<int> AvailableFpsValues { get; } = new();
        public ObservableCollection<string> AvailableCameraMotions { get; } = new();

        private string _promptText = string.Empty;
        public string PromptText
        {
            get => _promptText;
            set => SetProperty(ref _promptText, value ?? string.Empty);
        }

        private string _responseText = string.Empty;
        public string ResponseText
        {
            get => _responseText;
            set => SetProperty(ref _responseText, value ?? string.Empty);
        }

        private VideoProviderType _selectedProvider = VideoProviderType.OpenAI;
        public VideoProviderType SelectedProvider
        {
            get => _selectedProvider;
            set => SetSelectedProvider(value);
        }

        private string _selectedModel = "sora-2";
        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel,
                string.IsNullOrWhiteSpace(value) ? "sora-2" : value);
        }

        private string _selectedLength = "4";
        public string SelectedLength
        {
            get => _selectedLength;
            set => SetProperty(ref _selectedLength,
                string.IsNullOrWhiteSpace(value) ? "4" : value);
        }

        private string _selectedSize = "720x1280";
        public string SelectedSize
        {
            get => _selectedSize;
            set => SetProperty(ref _selectedSize,
                string.IsNullOrWhiteSpace(value) ? "720x1280" : value);
        }

        private int? _selectedFps;
        public int? SelectedFps
        {
            get => _selectedFps;
            set => SetProperty(ref _selectedFps, value);
        }

        private string _selectedCameraMotion = string.Empty;
        public string SelectedCameraMotion
        {
            get => _selectedCameraMotion;
            set => SetProperty(ref _selectedCameraMotion, value ?? string.Empty);
        }

        private bool _generateAudio = true;
        public bool GenerateAudio
        {
            get => _generateAudio;
            set => SetProperty(ref _generateAudio, value);
        }

        private bool _supportsFps;
        public bool SupportsFps
        {
            get => _supportsFps;
            set => SetProperty(ref _supportsFps, value);
        }

        private bool _supportsCameraMotion;
        public bool SupportsCameraMotion
        {
            get => _supportsCameraMotion;
            set => SetProperty(ref _supportsCameraMotion, value);
        }

        private bool _supportsGenerateAudio;
        public bool SupportsGenerateAudio
        {
            get => _supportsGenerateAudio;
            set => SetProperty(ref _supportsGenerateAudio, value);
        }

        private bool _supportsRemix = true;
        public bool SupportsRemix
        {
            get => _supportsRemix;
            set => SetProperty(ref _supportsRemix, value);
        }

        private bool _supportsReferenceImage = true;
        public bool SupportsReferenceImage
        {
            get => _supportsReferenceImage;
            set => SetProperty(ref _supportsReferenceImage, value);
        }

        public bool IsProviderOpenAi => SelectedProvider == VideoProviderType.OpenAI;
        public bool IsProviderLtx => SelectedProvider == VideoProviderType.Ltx;

        private bool _isRemix;
        public bool IsRemix
        {
            get => _isRemix;
            set => SetProperty(ref _isRemix, value);
        }

        private VideoListItem _selectedLibraryVideo;
        public VideoListItem SelectedLibraryVideo
        {
            get => _selectedLibraryVideo;
            set => SetProperty(ref _selectedLibraryVideo, value);
        }

        private ChatMessage _selectedSessionTurn;
        public ChatMessage SelectedSessionTurn
        {
            get => _selectedSessionTurn;
            set => SetProperty(ref _selectedSessionTurn, value);
        }

        private bool SetSelectedProvider(VideoProviderType value)
        {
            if (!SetProperty(ref _selectedProvider, value))
                return false;

            OnPropertyChanged(nameof(IsProviderOpenAi));
            OnPropertyChanged(nameof(IsProviderLtx));
            return true;
        }
    }
}
