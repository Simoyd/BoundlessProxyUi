using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoundlessProxyUi
{
    public enum CommPacketDirection
    {
        ClientToServer,
        ServerToClient,
    }

    public class CommPacket
    {
        public string HostName { get; set; }
        public Guid Id { get; set; }
        public byte[] Data { get; set; }
        public CommPacketDirection Direction { get; set; }

        public ConnectionInstance Parent { get; set; }
        public ObservableCollection<UserSearch> Searches { get; set; } = new ObservableCollection<UserSearch>();

        public string DisplayName => $"{HostName} - {Direction}";

        public MemoryStream Stream
        {
            get
            {
                return new MemoryStream(Data);
            }
            set
            {
            }
        }
    }
}
