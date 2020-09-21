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

namespace BoundlessProxyUi.ProxyManager
{
    /// <summary>
    /// Interaction logic for HostsFileClear.xaml
    /// </summary>
    public partial class HostsFileClear : UserControl
    {
        public HostsFileClear(string[] badEntries)
        {
            InitializeComponent();
            txtHostsFileLocation.Text += "\r\n" + System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\drivers\etc\hosts");
            lstClearItems.ItemsSource = badEntries;
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = false;
            ProxyManagerWindow.Instance.FadeControl(new HostsFileLookups());
        }
    }
}
