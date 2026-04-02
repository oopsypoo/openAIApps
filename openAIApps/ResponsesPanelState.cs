using openAIApps.Data;
using System;

namespace openAIApps
{
    public class ResponsesPanelState : ObservableObject
    {
        public bool IsWebSearchOptionsEnabled => UseWebSearch;
        public bool IsImageGenerationOptionsEnabled => UseImageGeneration;
        public bool IsImageGenerationOptionsVisible => UseImageGeneration;

        private string _promptText = string.Empty;
        public string PromptText
        {
            get => _promptText;
            set => SetProperty(ref _promptText, value ?? string.Empty);
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
            set
            {
                if (SetProperty(ref _useWebSearch, value))
                    OnPropertyChanged(nameof(IsWebSearchOptionsEnabled));
            }
        }

        private bool _useImageGeneration;
        public bool UseImageGeneration
        {
            get => _useImageGeneration;
            set
            {
                if (_useImageGeneration != value)
                {
                    _useImageGeneration = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsImageGenerationOptionsEnabled));
                    OnPropertyChanged(nameof(IsImageGenerationOptionsVisible));
                    OnPropertyChanged(nameof(IsOutputCompressionEnabled));
                    OnPropertyChanged(nameof(IsTransparentBackgroundAllowed));
                }
            }
        }

        private bool _useComputerUse;
        public bool UseComputerUse
        {
            get => _useComputerUse;
            set => SetProperty(ref _useComputerUse, value);
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
        private string _imageGenOutputFormat = "jpeg";
        public string ImageGenOutputFormat
        {
            get => _imageGenOutputFormat;
            set
            {
                if (_imageGenOutputFormat != value)
                {
                    _imageGenOutputFormat = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsOutputCompressionEnabled));
                    OnPropertyChanged(nameof(IsTransparentBackgroundAllowed));
                }
            }
        }

        private int _imageGenOutputCompression = 85;
        public int ImageGenOutputCompression
        {
            get => _imageGenOutputCompression;
            set
            {
                if (_imageGenOutputCompression != value)
                {
                    _imageGenOutputCompression = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _imageGenBackground = "auto";
        public string ImageGenBackground
        {
            get => _imageGenBackground;
            set
            {
                if (_imageGenBackground != value)
                {
                    _imageGenBackground = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _imageGenInputFidelity = "high";
        public string ImageGenInputFidelity
        {
            get => _imageGenInputFidelity;
            set
            {
                if (_imageGenInputFidelity != value)
                {
                    _imageGenInputFidelity = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsOutputCompressionEnabled =>
            string.Equals(ImageGenOutputFormat, "jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ImageGenOutputFormat, "webp", StringComparison.OrdinalIgnoreCase);

        public bool IsTransparentBackgroundAllowed =>
            UseImageGeneration &&
            (string.Equals(ImageGenOutputFormat, "png", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(ImageGenOutputFormat, "webp", StringComparison.OrdinalIgnoreCase));

        //Developer functions-controls
        private bool _useDeveloperTools;
        public bool UseDeveloperTools
        {
            get => _useDeveloperTools;
            set
            {
                if (_useDeveloperTools == value) return;
                _useDeveloperTools = value;
                OnPropertyChanged(nameof(UseDeveloperTools));
                OnPropertyChanged(nameof(IsDeveloperToolsOptionsVisible));
            }
        }

        private string _developerRepositoryRoot = string.Empty;
        public string DeveloperRepositoryRoot
        {
            get => _developerRepositoryRoot;
            set
            {
                if (_developerRepositoryRoot == value) return;
                _developerRepositoryRoot = value;
                OnPropertyChanged(nameof(DeveloperRepositoryRoot));
            }
        }

        private string _developerScope = "repository";
        public string DeveloperScope
        {
            get => _developerScope;
            set
            {
                if (_developerScope == value) return;
                _developerScope = value;
                OnPropertyChanged(nameof(DeveloperScope));
            }
        }

        private bool _developerAllowReadOnlyOnly = true;
        public bool DeveloperAllowReadOnlyOnly
        {
            get => _developerAllowReadOnlyOnly;
            set
            {
                if (_developerAllowReadOnlyOnly == value) return;
                _developerAllowReadOnlyOnly = value;
                OnPropertyChanged(nameof(DeveloperAllowReadOnlyOnly));
            }
        }

        private bool _developerRequireConfirmation;
        public bool DeveloperRequireConfirmation
        {
            get => _developerRequireConfirmation;
            set
            {
                if (_developerRequireConfirmation == value) return;
                _developerRequireConfirmation = value;
                OnPropertyChanged(nameof(DeveloperRequireConfirmation));
            }
        }

        private bool _developerShowToolLogs = true;
        public bool DeveloperShowToolLogs
        {
            get => _developerShowToolLogs;
            set
            {
                if (_developerShowToolLogs == value) return;
                _developerShowToolLogs = value;
                OnPropertyChanged(nameof(DeveloperShowToolLogs));
            }
        }

        private bool _developerToolSearchProjectText = true;
        public bool DeveloperToolSearchProjectText
        {
            get => _developerToolSearchProjectText;
            set
            {
                if (_developerToolSearchProjectText == value) return;
                _developerToolSearchProjectText = value;
                OnPropertyChanged(nameof(DeveloperToolSearchProjectText));
            }
        }

        private bool _developerToolReadProjectFile = true;
        public bool DeveloperToolReadProjectFile
        {
            get => _developerToolReadProjectFile;
            set
            {
                if (_developerToolReadProjectFile == value) return;
                _developerToolReadProjectFile = value;
                OnPropertyChanged(nameof(DeveloperToolReadProjectFile));
            }
        }

        private bool _developerToolListProjectFiles;
        public bool DeveloperToolListProjectFiles
        {
            get => _developerToolListProjectFiles;
            set
            {
                if (_developerToolListProjectFiles == value) return;
                _developerToolListProjectFiles = value;
                OnPropertyChanged(nameof(DeveloperToolListProjectFiles));
            }
        }

        private bool _developerToolRunDiagnostics;
        public bool DeveloperToolRunDiagnostics
        {
            get => _developerToolRunDiagnostics;
            set
            {
                if (_developerToolRunDiagnostics == value) return;
                _developerToolRunDiagnostics = value;
                OnPropertyChanged(nameof(DeveloperToolRunDiagnostics));
            }
        }

        private string _developerAllowedExtensionsCsv = ".cs,.xaml,.csproj,.sln,.json,.xml,.md,.config,.props,.targets";
        public string DeveloperAllowedExtensionsCsv
        {
            get => _developerAllowedExtensionsCsv;
            set
            {
                if (_developerAllowedExtensionsCsv == value) return;
                _developerAllowedExtensionsCsv = value;
                OnPropertyChanged(nameof(DeveloperAllowedExtensionsCsv));
            }
        }

        public bool IsDeveloperToolsOptionsVisible => UseDeveloperTools;
        private bool _isRequestInProgress;
        public bool IsRequestInProgress
        {
            get => _isRequestInProgress;
            set
            {
                if (_isRequestInProgress != value)
                {
                    _isRequestInProgress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AreRequestEditingControlsEnabled));
                }
            }
        }
        public bool AreRequestEditingControlsEnabled => !IsRequestInProgress;
    }

}