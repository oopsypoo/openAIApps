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
    /// Interaction logic for rassistant.xaml
    /// This is a retreival assistant. It's main purpose is to help users of the OpenAiApps program it can do.
    /// Files that will be uploaded are source-files and and README.md file. If thisis successfull or not we will see.
    /// In my logic: If the commentary is good in the sourcefiles, maybe the help-assistant will be good.
    /// </summary>
    public partial class rassistant : Window
    {
        
        /// <summary>
        /// these properties are taken from https://platform.openai.com/docs/api-reference/assistants/createAssistant using "paste special"
        /// 
        /// </summary>
        public string id { get; set; }
        public string _object { get; set; }
        public int created_at { get; set; }
        public string name { get; set; }
        public object description { get; set; }
        public string model { get; set; }
        public string instructions { get; set; }
        /// <summary>
        /// tools is optional. But in my case it's retrieval
        /// </summary>
        public Tool[] tools { get; set; }
        /// <summary>
        /// file-id's is optional. In this case it's README.md and all source-files
        /// </summary>
        public object[] file_ids { get; set; }
        public Metadata metadata { get; set; }
        public rassistant()
        {
            InitializeComponent();
        }
    }

    public class Metadata
    {
    }

    public class Tool
    {
        public string type { get; set; }
    }

}
