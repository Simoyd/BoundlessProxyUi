using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoundlessProxyUi
{
    public class ConnectionInstance : INotifyPropertyChanged
    {
        public Guid Id { get; set; }

        private bool _isConnectionOpen = true;
        public bool IsConnectionOpen
        {
            get
            {
                return _isConnectionOpen;
            }
            set
            {
                _isConnectionOpen = value;
                OnPropertyChanged(nameof(IsConnectionOpen));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        private string _hostName = string.Empty;
        public string HostName
        {
            get
            {
                return _hostName;
            }
            set
            {
                _hostName = value;
                OnPropertyChanged(nameof(HostName));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string DisplayName => $"{HostName} {(IsConnectionOpen ? "Open" : "Closed")}";

        public SslMitmInstance SslMitmInstance { get; set; }

        public ConnectionGrouping Parent { get; set; }

        public ObservableCollection<CommPacket> Packets { get; set; } = new ObservableCollection<CommPacket>();

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
