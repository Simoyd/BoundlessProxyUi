using BoundlessProxyUi.ProxyManager.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    /// Interaction logic for HostsFileType.xaml
    /// </summary>
    public partial class HostsFileType : UserControl
    {
        public HostsFileType(string message)
        {
            InitializeComponent();

            txtMessage.Text = message;
        }

        private void BtnAutomatic_Click(object sender, RoutedEventArgs e)
        {
            ((ManagerWindowViewModel)ProxyManagerWindow.Instance.DataContext).UserAuthorizedHostFile = true;

            if (!ComponentBase.IsAdministrator())
            {
                if (MessageBox.Show("This app will now be restarted with Administrator Elevation.", "Restarting", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
                {
                    return;
                }

                try
                {
                    Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetEntryAssembly().Location)
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                    });
                }
                catch { }

                Process.GetCurrentProcess().Kill();
            }

            btnAutomatic.IsEnabled = false;
            btnManual.IsEnabled = false;
            ComponentEngine.Instance.Start();
        }

        private void BtnManual_Click(object sender, RoutedEventArgs e)
        {
            ((ManagerWindowViewModel)ProxyManagerWindow.Instance.DataContext).UserAuthorizedHostFile = true;

            btnAutomatic.IsEnabled = false;
            btnManual.IsEnabled = false;
            ProxyManagerWindow.Instance.FadeControl(new HostsFileLookups());
        }
    }
}
