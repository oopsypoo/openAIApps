using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace openAIApps
{
    /// <summary>
    /// Interaction logic for CreateAssistant.xaml
    /// </summary>
    public partial class CreateAssistant : Window
    {
        //create an assistant POST path
        const string url_create_assistant = "https://api.openai.com/v1/assistants";

        public CreateAssistant()
        {
            InitializeComponent();
        }
    }
}
