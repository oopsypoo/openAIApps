using gpt35Turbo;
using System.Windows;


namespace openAIApps
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            requestGPT35 rxGPT35 = MainWindow.RxGPT35;
            tbModel.Text = "Model: " + rxGPT35.model;
        }
    }
}
