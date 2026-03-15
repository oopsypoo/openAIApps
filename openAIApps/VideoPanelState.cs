using openAIApps.Data;
using static openAIApps.VideoClient;

namespace openAIApps
{
    public class VideoPanelState : ObservableObject
    {
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
    }
}