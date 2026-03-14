using openAIApps.Data;

namespace openAIApps
{
    public class ResponsesPanelState : ObservableObject
    {
        public bool IsWebSearchOptionsEnabled => UseWebSearch;
        public bool IsImageGenerationOptionsEnabled => UseImageGeneration;

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

        private ChatMessage _selectedTurn;
        public ChatMessage SelectedTurn
        {
            get => _selectedTurn;
            set => SetProperty(ref _selectedTurn, value);
        }
        private string _selectedModel = string.Empty;
        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value ?? string.Empty);
        }

        private string _selectedReasoning = "none";
        public string SelectedReasoning
        {
            get => _selectedReasoning;
            set => SetProperty(ref _selectedReasoning, string.IsNullOrWhiteSpace(value) ? "none" : value);
        }

        private bool _useTextTool = true;
        public bool UseTextTool
        {
            get => _useTextTool;
            set => SetProperty(ref _useTextTool, value);
        }

        private bool _useWebSearch;
        public bool UseWebSearch
        {
            get => _useWebSearch;
            set => SetProperty(ref _useWebSearch, value);
        }

        private bool _useComputerUse;
        public bool UseComputerUse
        {
            get => _useComputerUse;
            set => SetProperty(ref _useComputerUse, value);
        }

        private bool _useImageGeneration;
        public bool UseImageGeneration
        {
            get => _useImageGeneration;
            set => SetProperty(ref _useImageGeneration, value);
        }

        private string _searchContextSize = "medium";
        public string SearchContextSize
        {
            get => _searchContextSize;
            set => SetProperty(ref _searchContextSize, string.IsNullOrWhiteSpace(value) ? "medium" : value);
        }

        private string _imageGenQuality = "auto";
        public string ImageGenQuality
        {
            get => _imageGenQuality;
            set => SetProperty(ref _imageGenQuality, string.IsNullOrWhiteSpace(value) ? "auto" : value);
        }

        private string _imageGenSize = "auto";
        public string ImageGenSize
        {
            get => _imageGenSize;
            set => SetProperty(ref _imageGenSize, string.IsNullOrWhiteSpace(value) ? "auto" : value);
        }
    }
}