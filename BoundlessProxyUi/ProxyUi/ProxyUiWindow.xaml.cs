using BoundlessProxyUi.ProxyManager;
using BoundlessProxyUi.ProxyManager.Components;
using BoundlessProxyUi.Util;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BoundlessProxyUi.ProxyUi
{
    /// <summary>
    /// Interaction logic for ProxyUiWindow.xaml
    /// </summary>
    public partial class ProxyUiWindow : Window
    {
        public static ProxyUiWindow Instance { get; set; }

        public ProxyUiWindow()
        {
            Instance = this;
            //ServicePointManager.ServerCertificateValidationCallback += delegate { return true; };
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;

            InitializeComponent();

            try
            {
                cmbSearchType.ItemsSource = Enum.GetValues(typeof(UserSearchType));
                DataContext = m_dc = new ProxyUiWindowViewModel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fatal error during startup:\r\n{ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private ProxyUiWindowViewModel m_dc = null;
        private bool m_dontMainSelect = false;
        private Regex numbersPattern = new Regex("[^0-9]+");

        public T GetDataContext<T>() 
            where T : class
        {
            return m_dc as T;
        }

        private void lstPackets_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var oldPacket = e.OldValue as CommPacket;

            if (oldPacket != null)
            {
                while (oldPacket.ParentPacket != null)
                {
                    oldPacket = oldPacket.ParentPacket;
                }

                if (!oldPacket.Instance.ChildPackets.Contains(oldPacket))
                {
                    TreeViewItem item = lstPackets.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
                    if (item != null)
                    {
                        item.IsSelected = true;
                        item.IsSelected = false;
                    }
                }
            }

            if (!m_dontMainSelect)
            {
                var stream = (lstPackets.SelectedItem as CommPacket)?.Stream;

                if (stream != null)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        hexMain.Stream = stream;
                    }));
                    lstSearchPackets.SelectedItem = null;
                }
            }
        }

        private void BtnAddSearch_Click(object sender, RoutedEventArgs e)
        {
            GetDataContext<ProxyUiWindowViewModel>().Searches.Add(new UserSearch
            {
                UserSearchType = (UserSearchType)cmbSearchType.SelectedItem,
                UserValue = txtSearchValue.Text,
            });
        }

        private void BtnRemoveSearch_Click(object sender, RoutedEventArgs e)
        {
            GetDataContext<ProxyUiWindowViewModel>().Searches.Remove(lstSearches.SelectedItem as UserSearch);
        }

        private void LstSearchPackets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var packet = lstSearchPackets.SelectedItem as CommPacket;

            if (packet != null)
            {
                hexMain.Stream = packet.Stream;

                new Thread(() =>
                {
                    GotoSearchPacket(packet);

                    bool retry = true;

                    while (retry)
                    {
                        retry = false;

                        Dispatcher.Invoke(new Action(() =>
                        {
                            try
                            {
                                //hexMain.SelectionStart = packet.Data.Search(packet.Data.Length, DataContext.SelectedSearch.searchBytes);
                                //hexMain.SelectionStop = hexMain.SelectionStart + DataContext.SelectedSearch.searchBytes.Length - 1;
                                hexMain.SetPosition(packet.Data.Search(packet.Data.Length, GetDataContext<ProxyUiWindowViewModel>().SelectedSearch.searchBytes), GetDataContext<ProxyUiWindowViewModel>().SelectedSearch.searchBytes.Length);
                            }
                            catch
                            {
                                retry = true;
                            }

                        }));
                    }
                }).Start();
            }
        }

        private void BtnGotoPacket_Click(object sender, RoutedEventArgs e)
        {
            var packet = lstSearchPackets.SelectedItem as CommPacket;

            if (packet != null)
            {
                GotoSearchPacket(packet);
            }
        }

        private void GotoSearchPacket(CommPacket packet)
        {
            m_dontMainSelect = true;

            Dispatcher.Invoke(new Action(() =>
            {
                lstGroups.SelectedItem = packet.Instance.Parent;
                lstInstances.SelectedItem = packet.Instance;

                ItemContainerGenerator doExpand(CommPacket curPacket)
                {
                    ItemContainerGenerator result = null;

                    if (curPacket.ParentPacket != null)
                    {
                        result = doExpand(curPacket.ParentPacket);
                    }

                    if (result == null)
                    {
                        result = lstPackets.ItemContainerGenerator;
                    }

                    var curItem = result.ContainerFromItem(curPacket) as TreeViewItem;

                    if (curItem != null)
                    {
                        curItem.IsSelected = true;
                        curItem.ExpandSubtree();
                        curItem.BringIntoView();

                        return curItem.ItemContainerGenerator;
                    }
                    else
                    {
                        return null;
                    }
                }

                doExpand(packet);
            }));

            m_dontMainSelect = false;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Instance = null;

            try
            {
                ProxyManagerWindow.Instance?.Close();
            }
            catch { }
        }

        private void PreviewNumberInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = numbersPattern.IsMatch(e.Text);
        }
    }
}
