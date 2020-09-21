using BoundlessProxyUi.ProxyManager.Components;
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
    /// Interaction logic for WelcomePage.xaml
    /// </summary>
    public partial class WelcomePage : UserControl
    {
        public WelcomePage()
        {
            InitializeComponent();
        }

        private void BtnAcceept_Click(object sender, RoutedEventArgs e)
        {
            btnAcceept.IsEnabled = false;
            btnCancel.IsEnabled = false;
            ComponentEngine.Instance.Start();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            btnAcceept.IsEnabled = false;
            btnCancel.IsEnabled = false;
            ProxyManagerWindow.Instance.Close();
        }
    }
}
