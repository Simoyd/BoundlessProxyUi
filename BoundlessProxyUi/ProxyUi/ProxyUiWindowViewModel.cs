using BoundlessProxyUi.ProxyManager;
using BoundlessProxyUi.ProxyManager.Components;
using BoundlessProxyUi.Util;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace BoundlessProxyUi.ProxyUi
{
    /// <summary>
    /// View model used by the ProxyUiWindow
    /// </summary>
    class ProxyUiWindowViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Connection groups (this collection does not change after startup)
        /// </summary>
        public ObservableCollection<ConnectionGrouping> Groups
        {
            get => ComponentEngine.Instance.GetComponent<TcpComponent>().groups;
        }

        /// <summary>
        /// The connection group that the user has selected
        /// </summary>
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

        /// <summary>
        /// The connection instance within the group that the user has selected
        /// </summary>
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

        /// <summary>
        /// Collection of searches that the user has added
        /// </summary>
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

        /// <summary>
        /// The search currently selected by the user
        /// </summary>
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

        /// <summary>
        /// Flag to indicate if packet capturing is enabled
        /// </summary>
        bool _captureEnabled = true;
        public bool CaptureEnabled
        {
            get => _captureEnabled;
            set
            {
                _captureEnabled = value;
                OnPropertyChanged(nameof(CaptureEnabled));
            }
        }

        public int PacketsPerInstance
        {
            get
            {
                return Config.GetSetting(nameof(PacketsPerInstance), 500);
            }
            set
            {
                Config.SetSetting(nameof(PacketsPerInstance), value);
                OnPropertyChanged(nameof(PacketsPerInstance));
            }
        }

        public int DeathTimeout
        {
            get
            {
                return Config.GetSetting(nameof(DeathTimeout), 60);
            }
            set
            {
                Config.SetSetting(nameof(DeathTimeout), value);
                OnPropertyChanged(nameof(DeathTimeout));
            }
        }

        /// <summary>
        /// Occurs when a property value changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called when a property value changes
        /// </summary>
        /// <param name="propertyName">The name  of the property that changed</param>
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SendBytesToUi(ConnectionInstance connectionInstance, CommPacket packet)
        {
            Task.Run(() =>
            {
                ProxyManagerWindow.Instance.Dispatcher.BeginInvoke(new Action(() =>
                {
                    connectionInstance.ChildPackets.Add(packet);

                    var windowInstance = ProxyUiWindow.Instance;

                    if (windowInstance == null)
                    {
                        return;
                    }

                    var maxPackets = windowInstance.GetDataContext<ProxyUiWindowViewModel>().PacketsPerInstance;

                    while (connectionInstance.ChildPackets.Count > maxPackets)
                    {
                        var curPacket = connectionInstance.ChildPackets[0];

                        connectionInstance.ChildPackets.RemoveAt(0);

                        curPacket.Searches.ToList().ForEach(cur => cur.Packets.Remove(curPacket));
                    }

                    void recursiveCheck(CommPacket innerPacket)
                    {
                        foreach (var curSearch in windowInstance.GetDataContext<ProxyUiWindowViewModel>().Searches)
                        {
                            if (innerPacket.Data.Search(innerPacket.Data.Length, curSearch.searchBytes) > -1)
                            {
                                innerPacket.Searches.Add(curSearch);
                                curSearch.Packets.Add(innerPacket);
                            }
                        }

                        foreach (var subPacket in innerPacket.ChildPackets)
                        {
                            recursiveCheck(subPacket);
                        }
                    }

                    recursiveCheck(packet);
                }));
            });
        }
    }
}
