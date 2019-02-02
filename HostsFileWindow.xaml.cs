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

namespace BoundlessProxyUi
{
    /// <summary>
    /// Interaction logic for HostsFileWindow.xaml
    /// </summary>
    public partial class HostsFileWindow : Window
    {
        public HostsFileWindow(string content)
        {
            InitializeComponent();

            txtMain.Text = content;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
