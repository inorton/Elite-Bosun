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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Bosun
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string currentSysName { get; set; }
        string currentSysUrl { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            UpdateBrowser("Sol", "http://eddb.io/system/17072");

            MainBrowser.LoadCompleted += MainBrowser_LoadCompleted;   
        }

        void MainBrowser_LoadCompleted(object sender, NavigationEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                mshtml.HTMLDocument doc = MainBrowser.Document as mshtml.HTMLDocument;
                if (doc != null)
                {
                    var h1s = doc.getElementsByTagName("h1");
                    if (h1s != null)
                    {
                        // would like to scroll to this somehow..
                    }
                }
            }));
        }


        void UpdateBrowser(string sysname, string url)
        {
            currentSysName = sysname;
            currentSysUrl = url;
            Dispatcher.BeginInvoke((Action)(() => {
                CurrentSystemNameLabel.Content = sysname;
                CurrentSystemUrlLabel.Content = url;
                MainBrowser.Source = new Uri(url);
            }));
        }

        private void ButtonOpenSystemPage(object sender, RoutedEventArgs e)
        {
            UpdateBrowser(currentSysName, currentSysUrl);
        }
    }
}
