using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.RightsManagement;

namespace BoundlessProxyUi.ProxyUi
{
    /// <summary>
    /// Enumeration to indicate the direction of a packet
    /// </summary>
    enum CommPacketDirection
    {
        /// <summary>
        /// Client to server
        /// </summary>
        ClientToServer,

        /// <summary>
        /// Server to client
        /// </summary>
        ServerToClient,
    }

    /// <summary>
    /// Class to store a data packet
    /// </summary>
    class CommPacket
    {
        /// <summary>
        /// The direction of a packet
        /// </summary>
        public CommPacketDirection Direction { get; set; }

        /// <summary>
        /// The remote host
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// A unique identifier for this packet, for cross-lookups regarding search functionality
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The raw data contained in the packet
        /// </summary>
        public byte[] Data { get; set; }

        public int TotalLength
        {
            get
            {
                return Data.Length + ChildPackets.Select(cur=> cur.TotalLength).Sum();
            }
        }

        /// <summary>
        /// The TCP connection that this packet came/went through
        /// </summary>
        public ConnectionInstance Instance { get; set; }

        /// <summary>
        /// The parent packet that this is part of
        /// </summary>
        public CommPacket ParentPacket { get; set; }

        /// <summary>
        /// Child parts of the structure of this packet
        /// </summary>
        public ObservableCollection<CommPacket> ChildPackets { get; set; } = new ObservableCollection<CommPacket>();

        /// <summary>
        /// User searches that this packet applies to
        /// </summary>
        public ObservableCollection<UserSearch> Searches { get; set; } = new ObservableCollection<UserSearch>();

        /// <summary>
        /// Display name for this packet in tree view
        /// </summary>
        public string DisplayName => $"{(Direction == CommPacketDirection.ServerToClient ? "RECV" : "SEND")}: {Header} - {Data.Length}{(Data.Length != TotalLength ? $"({TotalLength})" : string.Empty)} bytes";

        /// <summary>
        /// Memory stream for hex viewer
        /// </summary>
        public MemoryStream Stream => new MemoryStream(Data);
    }
}
