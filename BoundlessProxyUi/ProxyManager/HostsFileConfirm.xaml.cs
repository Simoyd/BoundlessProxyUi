using BoundlessProxyUi.ProxyManager.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

namespace BoundlessProxyUi.ProxyManager
{
    /// <summary>
    /// Interaction logic for HostsFileConfirm.xaml
    /// </summary>
    public partial class HostsFileConfirm : UserControl
    {
        public HostsFileConfirm()
        {
            InitializeComponent();
        }

        private void UpdateProcessingText(string text)
        {
            Dispatcher.Invoke(() => txtProcessingText.Text = text);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                try
                {
                    if (Process.GetProcessesByName("Boundless").FirstOrDefault() != null)
                    {
                        Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileUpdate("Boundless is running and must be closed to continue setup.", new string[] { })));
                        return;
                    }

                    List<string> errors = new List<string>();

                    ComponentEngine.Instance.ServerList.ForEach(cur =>
                    {
                        UpdateProcessingText($"Confirming lookup: {cur.Hostname}.");

                        IPAddress entry = Dns.GetHostAddresses(cur.Hostname).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork);

                        if (entry.ToString() != "127.0.0.1")
                        {
                            errors.Add($"127.0.0.1 {cur.Hostname}");
                        }
                    });

                    if (errors.Count > 0)
                    {
                        Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileUpdate(string.Empty, errors.ToArray())));
                        return;
                    }

                    if (Process.GetProcessesByName("Boundless").FirstOrDefault() != null)
                    {
                        Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileUpdate("Boundless is running and must be closed to continue setup.", new string[] { })));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileType(ex.Message)));
                    return;
                }

                Dispatcher.Invoke(() => ComponentEngine.Instance.GetComponent<HostsComponent>().IsFullyOn = true);
                ComponentEngine.Instance.Start();
            }).Start();
        }
    }
}
