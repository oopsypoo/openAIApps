using gpt;
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
            requestGPT rxGPT = MainWindow.RxGPT;
            tbModel.Text = "Model: " + rxGPT.model;
        }
    }
}
