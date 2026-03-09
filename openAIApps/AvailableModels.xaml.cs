using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace openAIApps
{
    public partial class AvailableModels : Window
    {
        private readonly List<string> _allAvailableModels;
        private readonly List<string> _selectedModels;

        private bool _filterHasError;
        private string _filterErrorMessage = string.Empty;

        public event Action<List<string>>? ModelsApplied;

        private void SortSelectedModels()
        {
            _selectedModels.Sort(StringComparer.OrdinalIgnoreCase);
        }
        public AvailableModels(IEnumerable<string> availableModels, IEnumerable<string>? currentSelectedModels = null)
        {
            InitializeComponent();
            // Normalize and sort the available models list.
            _allAvailableModels = NormalizeDistinct(availableModels)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Prefer current selected models if supplied.
            // Otherwise load the saved file into the right list.
            _selectedModels = currentSelectedModels != null
                ? NormalizeDistinct(currentSelectedModels)
                : AvailableModelsStorage.Load();
            // sort the selected models and ensure they are in the available list
            _selectedModels = _selectedModels
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            RefreshSelectedList();
            RefreshAvailableList();
        }

        private static List<string> NormalizeDistinct(IEnumerable<string>? models)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (models == null)
                return result;

            foreach (var model in models)
            {
                string value = model?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (seen.Add(value))
                    result.Add(value);
            }

            return result;
        }

        private void RefreshAll()
        {
            RefreshSelectedList();
            RefreshAvailableList();
        }

        private void RefreshSelectedList()
        {
            SortSelectedModels();

            lbSelectedModels.ItemsSource = null;
            lbSelectedModels.ItemsSource = _selectedModels;

            txtSelectedCount.Text = $"{_selectedModels.Count} model(s)";
        }

        private void RefreshAvailableList()
        {
            IEnumerable<string> available = _allAvailableModels
                .Where(m => !_selectedModels.Contains(m, StringComparer.OrdinalIgnoreCase));

            List<string> filtered = ApplyFilter(available);

            lbAvailableModels.ItemsSource = null;
            lbAvailableModels.ItemsSource = filtered;

            if (_filterHasError)
                txtAvailableCount.Text = "Invalid regex";
            else
                txtAvailableCount.Text = $"{filtered.Count} model(s)";
        }

        private List<string> ApplyFilter(IEnumerable<string> source)
        {
            _filterHasError = false;
            _filterErrorMessage = string.Empty;

            string filterText = tbFilter.Text?.Trim() ?? string.Empty;

            ClearFilterError();

            if (string.IsNullOrWhiteSpace(filterText))
                return source.OrderBy(m => m).ToList();

            if (chkUseRegex.IsChecked == true)
            {
                try
                {
                    Regex regex = new Regex(filterText, RegexOptions.IgnoreCase);

                    return source
                        .Where(m => regex.IsMatch(m))
                        .OrderBy(m => m)
                        .ToList();
                }
                catch (ArgumentException ex)
                {
                    _filterHasError = true;
                    _filterErrorMessage = ex.Message;
                    ShowFilterError(_filterErrorMessage);
                    return new List<string>();
                }
            }

            return source
                .Where(m => m.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m)
                .ToList();
        }

        private void ShowFilterError(string message)
        {
            tbFilter.BorderBrush = Brushes.OrangeRed;
            tbFilter.ToolTip = $"Invalid regular expression: {message}";
        }

        private void ClearFilterError()
        {
            tbFilter.ClearValue(BorderBrushProperty);
            tbFilter.ToolTip = "Type text to filter available models";
        }

        private void AddModelsToSelected(IEnumerable<string> modelsToAdd)
        {
            foreach (string model in modelsToAdd)
            {
                if (!_selectedModels.Contains(model, StringComparer.OrdinalIgnoreCase))
                    _selectedModels.Add(model);
            }

            SortSelectedModels();
            RefreshAll();
        }

        private void RemoveModelsFromSelected(IEnumerable<string> modelsToRemove)
        {
            HashSet<string> removeSet = new HashSet<string>(modelsToRemove, StringComparer.OrdinalIgnoreCase);

            _selectedModels.RemoveAll(m => removeSet.Contains(m));
            SortSelectedModels();
            RefreshAll();
        }

        private List<string> GetVisibleAvailableModels()
        {
            return lbAvailableModels.Items.Cast<string>().ToList();
        }

        private List<string> GetSelectedModelsFromRightList()
        {
            return lbSelectedModels.Items.Cast<string>().ToList();
        }

        private void Button_AddSelected_Click(object sender, RoutedEventArgs e)
        {
            List<string> selected = lbAvailableModels.SelectedItems.Cast<string>().ToList();

            if (selected.Count == 0)
                return;

            AddModelsToSelected(selected);
        }

        private void Button_AddAll_Click(object sender, RoutedEventArgs e)
        {
            List<string> visibleModels = GetVisibleAvailableModels();

            if (visibleModels.Count == 0)
                return;

            AddModelsToSelected(visibleModels);
        }

        private void Button_RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            List<string> selected = lbSelectedModels.SelectedItems.Cast<string>().ToList();

            if (selected.Count == 0)
                return;

            RemoveModelsFromSelected(selected);
        }

        private void Button_RemoveAll_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedModels.Count == 0)
                return;

            _selectedModels.Clear();
            RefreshAll();
        }

        private void Button_UseNow_Click(object sender, RoutedEventArgs e)
        {
            List<string> modelsToUse = GetSelectedModelsFromRightList();

            if (modelsToUse.Count == 0)
            {
                MessageBox.Show(
                    "There are no selected models to use.",
                    "Nothing to Use",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            ModelsApplied?.Invoke(modelsToUse);

            MessageBox.Show(
                $"{modelsToUse.Count} model(s) applied to the responses UI.",
                "Models Applied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Button_Save_model_list(object sender, RoutedEventArgs e)
        {
            try
            {
                List<string> modelsToSave = GetSelectedModelsFromRightList();

                if (modelsToSave.Count == 0)
                {
                    MessageBox.Show(
                        "There are no selected models to save.\nUse Delete if you want to remove the saved file.",
                        "Nothing to Save",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                AvailableModelsStorage.Save(modelsToSave);

                // Also apply immediately
                ModelsApplied?.Invoke(modelsToSave);

                MessageBox.Show(
                    $"Saved {modelsToSave.Count} model(s) to:\n{AvailableModelsStorage.FilePath}",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not save model list:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Button_Delete_model_list(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Delete the saved model list file and clear the selected list?",
                "Delete Saved List",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                AvailableModelsStorage.Delete();

                _selectedModels.Clear();
                RefreshAll();

                MessageBox.Show(
                    "The saved model list was deleted.",
                    "Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not delete the saved model list:\n\n{ex.Message}",
                    "Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void tbFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshAvailableList();
        }

        private void chkUseRegex_Checked(object sender, RoutedEventArgs e)
        {
            RefreshAvailableList();
        }

        private void chkUseRegex_Unchecked(object sender, RoutedEventArgs e)
        {
            RefreshAvailableList();
        }
    }
}