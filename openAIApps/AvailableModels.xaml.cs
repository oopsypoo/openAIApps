using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace openAIApps
{
    public partial class AvailableModels : Window
    {
        private List<string> _allModels = new();

        private const string ModelsFileName = "available_models.txt";
        private string ModelsFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModelsFileName);

        public event Action<List<string>>? ModelsApplied;

        public AvailableModels(List<string> availableModels)
        {
            InitializeComponent();

            _allModels = availableModels
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            RefreshComboBox(_allModels);
        }

        private void RefreshComboBox(List<string> models)
        {
            cbModels.ItemsSource = null;
            cbModels.ItemsSource = models;

            if (models.Count > 0)
                cbModels.SelectedIndex = 0;
        }

        private List<string> GetCurrentVisibleModels()
        {
            return cbModels.Items.Cast<string>().ToList();
        }

        private List<string> FilterModels()
        {
            string pattern = tbRegEx.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(pattern))
            {
                RefreshComboBox(_allModels);
                return _allModels;
            }

            try
            {
                var filtered = _allModels
                    .Where(model => Regex.IsMatch(model, pattern, RegexOptions.IgnoreCase))
                    .ToList();

                RefreshComboBox(filtered);
                return filtered;
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(
                    $"Invalid regular expression:\n\n{ex.Message}",
                    "Regex Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return GetCurrentVisibleModels();
            }
        }

        private void Button_filter_list(object sender, RoutedEventArgs e)
        {
            FilterModels();
        }

        private void Button_UseNow_Click(object sender, RoutedEventArgs e)
        {
            var modelsToUse = GetCurrentVisibleModels();
            ModelsApplied?.Invoke(modelsToUse);
        }

        private void Button_Save_model_list(object sender, RoutedEventArgs e)
        {
            try
            {
                var modelsToSave = GetCurrentVisibleModels();

                File.WriteAllLines(ModelsFilePath, modelsToSave);

                // Also apply immediately to MainWindow
                ModelsApplied?.Invoke(modelsToSave);

                MessageBox.Show(
                    $"Saved {modelsToSave.Count} model(s) to file.",
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}