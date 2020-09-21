using BoundlessProxyUi.JsonUpload;
using BoundlessProxyUi.ProxyUi;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
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
    /// Interaction logic for Running.xaml
    /// </summary>
    public partial class Running : UserControl
    {
        public Running()
        {
            dc = ((ManagerWindowViewModel)ProxyManagerWindow.Instance.DataContext);

            InitializeComponent();
        }

        private ManagerWindowViewModel dc;

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ProxyUiWindow.Instance == null)
            {
                new ProxyUiWindow().Show();
            }

            if (JsonUploadWindow.Instance == null)
            {
                new JsonUploadWindow().Show();
            }
        }
    }
}

