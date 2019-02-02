using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BoundlessProxyUi
{
    public class ConnectionGrouping : TreeViewItem
    {
        public string GroupingName { get; set; }

        public SslMitm SslMitm { get; set; }

        public ObservableCollection<ConnectionInstance> Instances { get; set; } = new ObservableCollection<ConnectionInstance>();

        public MainWindow.MainWindowViewModel Model { get; set; }
    }
}
