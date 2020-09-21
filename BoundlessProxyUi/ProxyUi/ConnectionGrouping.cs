using BoundlessProxyUi.Mitm;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace BoundlessProxyUi.ProxyUi
{
    class ConnectionGrouping : TreeViewItem
    {
        public string GroupingName { get; set; }

        public ObservableCollection<ConnectionInstance> Instances { get; set; } = new ObservableCollection<ConnectionInstance>();
    }
}
