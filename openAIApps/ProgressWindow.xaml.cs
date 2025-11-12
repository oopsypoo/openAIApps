using System;
using System.Windows;

namespace openAIApps
{
    public partial class ProgressWindow : Window
    {
        public event EventHandler? Canceled;

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Canceled?.Invoke(this, EventArgs.Empty);
        }
        public ProgressWindow(string title)
        {
            InitializeComponent();
            txtTitle.Text = title;
        }

        public void UpdateProgress(double value)
        {
            pbProgress.Value = value;
            txtProgress.Text = $"{value:F0}%";
        }
    }
}
