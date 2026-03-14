using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openAIApps
{
    public class LogsPanelState : ObservableObject
    {
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value ?? string.Empty);
        }

        private string _typeFilter = "All";
        public string TypeFilter
        {
            get => _typeFilter;
            set => SetProperty(ref _typeFilter,
                string.IsNullOrWhiteSpace(value) ? "All" : value);
        }
    }
}