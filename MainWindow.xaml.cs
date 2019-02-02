using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
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
using System.Windows.Threading;
using Path = System.IO.Path;

namespace BoundlessProxyUi
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static Dispatcher Dispatcher;

        public class MainWindowViewModel : INotifyPropertyChanged
        {
            private ObservableCollection<ConnectionGrouping> _groups = new ObservableCollection<ConnectionGrouping>();
            public ObservableCollection<ConnectionGrouping> Groups
            {
                get => _groups;
                set
                {
                    _groups = value;
                    OnPropertyChanged(nameof(Groups));
                }
            }

            private ObservableCollection<UserSearch> _searches = new ObservableCollection<UserSearch>();
            public ObservableCollection<UserSearch> Searches
            {
                get => _searches;
                set
                {
                    _searches = value;
                    OnPropertyChanged(nameof(Searches));
                }
            }

            private ConnectionGrouping _selectedGroup;
            public ConnectionGrouping SelectedGroup
            {
                get => _selectedGroup;
                set
                {
                    _selectedGroup = value;
                    OnPropertyChanged(nameof(SelectedGroup));
                }
            }

            private ConnectionInstance _selectedInstance;
            public ConnectionInstance SelectedInstance
            {
                get => _selectedInstance;
                set
                {
                    _selectedInstance = value;
                    OnPropertyChanged(nameof(SelectedInstance));
                }
            }

            private UserSearch _selectedSearch;
            public UserSearch SelectedSearch
            {
                get => _selectedSearch;
                set
                {
                    _selectedSearch = value;
                    OnPropertyChanged(nameof(SelectedSearch));
                }
            }

            private bool _captureEnabled = true;
            public bool CaptureEnabled
            {
                get => _captureEnabled;
                set
                {
                    _captureEnabled = value;
                    OnPropertyChanged(nameof(CaptureEnabled));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public MainWindow()
        {
            Dispatcher = base.Dispatcher;

            InitializeComponent();

            cmbSearchType.ItemsSource = Enum.GetValues(typeof(UserSearchType));
            DataContext = new MainWindowViewModel();
        }

        private ServerListAbstraction serverList = new ServerListAbstraction();
        private Dictionary<string, IPAddress> serverLookup;

        public new MainWindowViewModel DataContext
        {
            get
            {
                return base.DataContext as MainWindowViewModel;
            }
            set
            {
                base.DataContext = value;
            }
        }
        

        private void PrepSequence()
        {
            // prompt to clear hosts file
            MessageBox.Show(this, "Please ensure that your hosts file is clean (no uncommented lines), then click 'OK' to continue!\r\n\r\nThe usual place for the hosts file is \"C:\\Windows\\System32\\drivers\\etc\\hosts\"", "Reset Hosts File!", MessageBoxButton.OK);

            // Run IP addresses
            JObject serverListJson;

            string blah = $"https://ds.playboundless.com:8902/list-gameservers";

            using (WebResponse response = WebRequest.Create(blah).GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                serverListJson = JObject.Parse(reader.ReadToEnd());
            }

            // Persist em
            serverList.Clear();

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"127.0.0.1 ds.playboundless.com");

            serverList.AddServer(new ServerList { Hostname = "ds.playboundless.com", Ip = Dns.GetHostAddresses("ds.playboundless.com").First().ToString() });

            serverListJson.Cast<KeyValuePair<string, JToken>>().Select(cur => cur.Value.Value<JObject>()["addr"].Value<string>()).Distinct().ToList().ForEach(cur =>
            {
                sb.AppendLine($"127.0.0.1 {cur}");
                IPAddress entry = Dns.GetHostAddresses(cur).First();

                serverList.AddServer(new ServerList { Hostname = cur, Ip = entry.ToString() });
            });

            // Output to user
            new HostsFileWindow(sb.ToString()) { Owner = this }.ShowDialog();
        }

        private void HitIt()
        {
            // "load" persistance
            var serverLookup = serverList.Data.ToDictionary(cur => cur.Hostname, cur => cur.Ip);

            // open proxys
            var certificate = new X509Certificate2(txtPfx.Text, "");

            ConnectionGrouping discoveryGrouping = new ConnectionGrouping
            {
                GroupingName = "Discovery Server",
                Model = DataContext,
            };
            SslMitm discoveryMitm = new SslMitm(IPAddress.Loopback, 8902, certificate, serverLookup, discoveryGrouping)
            {
                ReplaceIpaddr = true,
            };
            DataContext.Groups.Add(discoveryGrouping);

            ConnectionGrouping websocketGrouping = new ConnectionGrouping
            {
                GroupingName = "Planet Websocket",
                Model = DataContext,
            };
            SslMitm websocketMitm = new SslMitm(IPAddress.Loopback, 443, certificate, serverLookup, websocketGrouping)
            {
                ReplaceIpaddr = false,
            };
            DataContext.Groups.Add(websocketGrouping);
        }

        private void Prep_Click(object sender, RoutedEventArgs e)
        {
            PrepSequence();
        }

        private void Go_Click(object sender, RoutedEventArgs e)
        {
            HitIt();
        }

        bool dontMainSelect = false;

        private void LstPackets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!dontMainSelect)
            {
                var stream = (lstPackets.SelectedItem as CommPacket)?.Stream;

                if (stream != null)
                {
                    hexMain.Stream = stream;
                    lstSearchPackets.SelectedItem = null;
                }
            }
        }

        private void BtnAddSearch_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Searches.Add(new UserSearch
            {
                UserSearchType = (UserSearchType)cmbSearchType.SelectedItem,
                UserValue = txtSearchValue.Text,
            });
        }

        private void BtnRemoveSearch_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Searches.Remove(lstSearches.SelectedItem as UserSearch);
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
                                hexMain.SetPosition(packet.Data.Search(packet.Data.Length, DataContext.SelectedSearch.searchBytes), DataContext.SelectedSearch.searchBytes.Length);
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
            dontMainSelect = true;

            Dispatcher.Invoke(new Action(() =>
            {
                lstGroups.SelectedItem = packet.Parent.Parent;
                lstInstances.SelectedItem = packet.Parent;
                lstPackets.SelectedItem = packet;

                lstPackets.ScrollIntoView(packet);
            }));

            dontMainSelect = false;
        }

        private void CheckCert_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.GetDirectoryName(txtBoundless.Text);
            var bundlePath = Path.Combine(path, "ca-bundle.crt");
            var bundle = File.ReadAllText(bundlePath);
            var mitm = File.ReadAllText(txtCrt.Text);

            if (bundle.Contains(mitm))
            {
                MessageBox.Show(this, "Cert looks good!", "Certificate Status", MessageBoxButton.OK);
                return;
            }

            var result = MessageBox.Show("The MITM Certificate is not present in the boundless ca-bundle.crt file. You need to add it for the MITM to work. Would you like to add it now? A backup file will be made.", "Certificate Status", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.No)
            {
                return;
            }

            var backupFile = Path.Combine(path, "ca-bundle-org.crt");
            int i = 2;

            while (File.Exists(backupFile))
            {
                backupFile = Path.Combine(path, $"ca-bundle-org({i++}).crt");
            }

            File.Copy(bundlePath, backupFile);
            File.WriteAllText(bundlePath, bundle + mitm);

            MessageBox.Show(this, "Cert has been updated!", "Certificate Status", MessageBoxButton.OK);
        }
    }
}
