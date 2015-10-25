using BosunCore;
using LogMonitor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

        FirstMate Mate { get; set; }

        public void AttachMate(FirstMate mate)
        {
            Mate = mate;

            Mate.StarSystemEntered += Mate_StarSystemEntered;
        }

        void Mate_StarSystemEntered(string name, long eddbid, string eddburl)
        {
            UpdateBrowser(name, eddburl);
            BosunConfiguration.Set("lastsystem", name);
        }

        public MainWindow()
        {
            InitializeComponent();

            MainBrowser.LoadCompleted += MainBrowser_LoadCompleted;
            this.Closing += MainWindow_Closing;

            var sl = new ShipLocator();
            Mate = new FirstMate(sl);

            Mate.Start();

            ThreadPool.QueueUserWorkItem((x) =>
            {
                var last_system = BosunConfiguration.Read("lastsystem", "Sol");
                var last_url = Mate.GetEDDBSystemUrl(last_system);
                UpdateBrowser(last_system, last_url);
            });
        }        

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            BosunConfiguration.Save();
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
                        foreach (var ele in h1s)
                        {
                            int scrolly = 0;
                            var h1 = ele as mshtml.HTMLHeadElement;
                            if (h1 != null)
                            {
                                scrolly = h1.offsetTop;
                                doc.parentWindow.scrollTo(0, scrolly);
                                // would like to scroll to this somehow..
                            }
                            break;
                        }
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
                if (url != null)
                {
                    MainBrowser.Source = new Uri(url);
                }
            }));
        }

        private void ButtonOpenSystemPage(object sender, RoutedEventArgs e)
        {
            UpdateBrowser(currentSysName, currentSysUrl);
        }
    }
}
